using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;
using TSLab.DataSource;
using TSLab.Script.Handlers;
// ReSharper disable RedundantStringInterpolation

namespace TSLab.ML.Net
{
    internal sealed class SeparatedModelHelper<TInput, TResult> where TInput : class where TResult : class, new()
    {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly ILog s_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private bool UseDisk => false;

        private const string LabelName = "Label";

        [Serializable]
        public class Model
        {
            public int DatasCount { get; set; }

            public byte[] Data { get; set; }
        }

        private Interval ModelChangeInterval { get; }

        private int TrainDays { get; }

        private int CalcLastNumModels { get; }

        private IContext Context { get; }

        private readonly Dictionary<DateTime, PredictionEngine<TInput, TResult>> m_engines =
            new Dictionary<DateTime, PredictionEngine<TInput, TResult>>();

        public SeparatedModelHelper(IContext context, int trainDays, Interval modelChangeInterval,
                                    int calcLastNumModels)
        {
            Context = context;
            TrainDays = trainDays;
            ModelChangeInterval = modelChangeInterval;
            CalcLastNumModels = calcLastNumModels;
        }

        private void MakePredictionEngine(IReadOnlyList<KeyValuePair<DateTime, List<TInput>>> separatedData, int i,
                                          string key, SchemaDefinition scheme)
        {
            var dataCount = Math.Max(1, TrainDays * 24 * 60 / ModelChangeInterval.Shift.TotalMinutes);
            var curDt = separatedData[i].Key;
            var splitKey = $"{key}.{ModelChangeInterval}.{TrainDays}.{curDt}";
            var predictionData = new List<TInput>(separatedData[i].Value);
            for (var k = 1; k < dataCount && i - k >= 0 && (curDt - separatedData[i - k].Key).Days < TrainDays; k++)
                predictionData.AddRange(separatedData[i - k].Value);

            var mlContext = new MLContext(0);
            ITransformer trainedModel = null;
            var model = (Model)Context.LoadGlobalObject(splitKey, UseDisk);
            DataViewSchema trainedScheme;
            if (model != null && model.DatasCount == predictionData.Count)
            {
                trainedModel = mlContext.Model.Load(new MemoryStream(model.Data), out trainedScheme);
            }

            var testDataView = mlContext.Data.LoadFromEnumerable(separatedData[i + 1].Value, scheme);
            if (trainedModel == null)
            {
#if DEBUG
                s_log.Debug($"Make model : {splitKey}.{separatedData.Count}");
#endif
                var trainDataView = mlContext.Data.LoadFromEnumerable(predictionData, scheme);
                var trainer = mlContext.Regression.Trainers.FastTreeTweedie(numberOfLeaves: 30, numberOfTrees: 200, learningRate: 0.3);
                //var trainer = mlContext.Regression.Trainers.PoissonRegression(memorySize: 200);

                var dataProcessPipeline = mlContext.Transforms.CopyColumns("Preview", LabelName);
                var modelBuilder = new ModelBuilder<TInput, TResult>(mlContext, dataProcessPipeline);
                modelBuilder.AddTrainer(trainer);
                modelBuilder.Train(trainDataView);
                trainedModel = modelBuilder.TrainedModel;
                trainedScheme = trainDataView.Schema;

                using (var stream = new MemoryStream())
                {
                    mlContext.Model.Save(trainedModel, trainedScheme, stream);
                    model = new Model { DatasCount = predictionData.Count, Data = stream.ToArray() };
                }

                Context.StoreGlobalObject(splitKey, model, UseDisk);
#if DEBUG
                var metrics = modelBuilder.EvaluateRegressionModel(testDataView, LabelName, "Score");
                ConsoleHelper.PrintRegressionMetrics(trainer.ToString(), metrics);
#endif
            }

            var func = mlContext.Model.CreatePredictionEngine<TInput, TResult>(trainedModel,
                inputSchemaDefinition: scheme);
            m_engines[curDt] = func;
        }

        public delegate double MakeResultDelegate(TResult res);

        internal IList<double> Build(string key, IReadOnlyList<IDataBar> bars, TInput[] data, int size,
                                     int futureViewBarsCount, MakeResultDelegate makeRes)
        {
            var separatedData = new List<KeyValuePair<DateTime, List<TInput>>>();
            var curDate = ModelChangeInterval.AlignDate(bars[0].Date);
            List<TInput> curList = null;
            for (var i = 0; i < bars.Count; i++)
            {
                var date = ModelChangeInterval.AlignDate(bars[i].Date);
                if (curDate != date)
                {
                    if (curList != null)
                        separatedData.Add(new KeyValuePair<DateTime, List<TInput>>(curDate, curList));
                    curList = null;
                }

                if (curList == null)
                {
                    curDate = date;
                    curList = new List<TInput>();
                }

                curList.Add(data[i]);
            }

            if (curList != null)
                separatedData.Add(new KeyValuePair<DateTime, List<TInput>>(curDate, curList));

#if DEBUG
            s_log.Debug($"*************************************************");
            s_log.Debug($"Start prediction : {key}.{ModelChangeInterval}.{TrainDays}.{separatedData.Count}");
            s_log.Debug($"*************************************************");
#endif

            var scheme = SchemaDefinition.Create(typeof(ModelTrainer.PredictionData));
            scheme["Preview"].ColumnType = NumberDataViewType.Single;
            scheme["Preview"].ColumnName = LabelName;
            scheme["Features"].ColumnType = new VectorDataViewType(NumberDataViewType.Single, size);

            var res = new List<double>(bars.Count);
            res.AddRange(new double[separatedData[0].Value.Count]);
            var numModels = separatedData.Count - 1;
            for (var i = 0; i < numModels; i++)
            {
                if (i + CalcLastNumModels < numModels)
                {
                    // filling by zero
                    res.AddRange(new double[separatedData[i + 1].Value.Count]);
                    continue;
                }

                MakePredictionEngine(separatedData, i, key, scheme);

                var testData = separatedData[i + 1].Value;
                foreach (var pdData in testData)
                {
                    var modelDate = ModelChangeInterval.AlignDate(bars[res.Count - futureViewBarsCount].Date)
                                                 .Add(ModelChangeInterval.Shift);
                    var predictionEngine =
                        m_engines.OrderByDescending(v => v.Key).FirstOrDefault(v => v.Key <= modelDate).Value;
                    if (predictionEngine == null)
                    {
                        res.Add(0);
                        continue;
                    }

                    var predictedPreview = predictionEngine.Predict(pdData);
                    res.Add(makeRes(predictedPreview));
                }
            }

#if DEBUG
            s_log.Debug($"*************************************************");
            s_log.Debug($"Stop  prediction : {key}.{ModelChangeInterval}.{TrainDays}.{separatedData.Count}");
            s_log.Debug($"*************************************************");
#endif
            return res;
        }
    }
}
