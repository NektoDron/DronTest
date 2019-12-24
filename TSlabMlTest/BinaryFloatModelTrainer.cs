using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.ML;
using Microsoft.ML.Data;
using TSLab.Script.Handlers;
using TSLab.Utils;
// ReSharper disable UnusedMember.Global

namespace TSLab.ML.Net
{
    [HandlerCategory("ML.Net")]
    [InputsCount(2, 6)]
    [Input(0, TemplateTypes.DOUBLE, Name = "Preview")]
    [Input(1, TemplateTypes.DOUBLE, Name = "Value1")]
    [Input(2, TemplateTypes.DOUBLE, Name = "Value2")]
    [Input(3, TemplateTypes.DOUBLE, Name = "Value3")]
    [Input(4, TemplateTypes.DOUBLE, Name = "Value4")]
    [Input(5, TemplateTypes.DOUBLE, Name = "Value5")]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    public sealed class BinaryFloatModelTrainer : BinaryModelBase<float>, IStreamHandler, INeedVariableName
    {
        public string VariableName { get; set; }

        [HandlerParameter(true, "3", Min = "1", Max = "5", Step = "1", EditorMin = "1")]
        public int NumberOfIterations { get; set; } = 1;

        [HandlerParameter(true, "3", Min = "1", Max = "6", Step = "1", EditorMin = "1")]
        public int TypeOfTrainer { get; set; } = 1;

        [HandlerParameter(true, "0.3", Min = "0", Max = "0.5", Step = "0.1", EditorMin = "0")]
        public double TestSize { get; set; } = 0.2;

        public IList<double> Execute(IList<double> source1, IList<double> source2)
        {
            return Execute(source1, new[] { source2 });
        }

        public IList<double> Execute(IList<double> source1, IList<double> source2, IList<double> source3)
        {
            return Execute(source1, new[] { source2, source3 });
        }

        public IList<double> Execute(IList<double> source1, IList<double> source2, IList<double> source3,
                                     IList<double> source4)
        {
            return Execute(source1, new[] { source2, source3, source4 });
        }

        public IList<double> Execute(IList<double> source1, IList<double> source2, IList<double> source3,
                                     IList<double> source4, IList<double> source5)
        {
            return Execute(source1, new[] { source2, source3, source4, source5 });
        }

        public IList<double> Execute(IList<double> source1, IList<double> source2, IList<double> source3,
                                     IList<double> source4, IList<double> source5, IList<double> source6)
        {
            return Execute(source1, new[] { source2, source3, source4, source5, source6 });
        }

        private PredictionData MakePredictionData(IList<double> preview, IList<double>[] sources, int i, int size)
        {
            var values = Context.GetArray<float>(size) ?? new float[size];
            if (HistoryBarsBack <= i)
            {
                for (var j = 0; j < HistoryBarsBack; j++)
                {
                    var shift = j * sources.Length;
                    for (var k = 0; k < sources.Length; k++)
                    {
                        values[shift + k] = (float)sources[k][i - j];
                    }
                }
            }

            return new PredictionData { Features = new VBuffer<float>(size, values), Preview = preview[i] > 0 };
        }

        public IList<double> Execute(IList<double> preview, IList<double>[] sources)
        {
            var count = preview.Count;
            if (count == 0)
                return new double[0];
            var context = Context;
            Contract.Assert(context != null);

            var size = HistoryBarsBack * sources.Length;
            var forTrain = (int)(count * (1 - Math.Min(1, TestSize)));

            var trainingData = new List<PredictionData>(forTrain);
            for (var i = 0; i < forTrain; i++)
            {
                if (i < HistoryBarsBack)
                    continue;
                var item = MakePredictionData(preview, sources, i, size);
                item.Probability = DoubleUtil.IsZero(preview[i]) ? 0.5f : 1;
                trainingData.Add(item);
            }

            var testData = new List<PredictionData>(count - forTrain);
            for (var i = forTrain; i < count; i++)
            {
                testData.Add(MakePredictionData(preview, sources, i, size));
            }

            var scheme = GetSchemaDefinition(size);

            var mlContext = MakeMlContext();

            var testDataView = mlContext.Data.LoadFromEnumerable(testData, scheme);
            var trainingDataView = mlContext.Data.LoadFromEnumerable(trainingData, scheme);

            var trainer = GetEstimator(mlContext, TypeOfTrainer, NumberOfIterations);

            var dataProcessPipeline = mlContext.Transforms.CopyColumns("Preview", "Label");
            var trainingPipeline = dataProcessPipeline.Append(trainer);
            var trainedModel = trainingPipeline.Fit(trainingDataView);
            var predictions = trainedModel.Transform(testDataView);
            var metrics = mlContext.BinaryClassification.Evaluate(data: predictions);
            ConsoleHelper.PrintBinaryClassificationMetrics(trainer.ToString(), metrics);

            if (!context.IsOptimization && !string.IsNullOrEmpty(ModelPath))
            {
                mlContext.Model.Save(trainedModel, trainingDataView.Schema, ModelPath);
            }

            var predictionEngine =
                mlContext.Model.CreatePredictionEngine<PredictionData, PredictionResult>(trainedModel,
                    inputSchemaDefinition: scheme);
            var res = context.GetArray<double>(count) ?? new double[count];
            for (var i = forTrain; i < count; i++)
            {
                var predictedPreview = predictionEngine.Predict(testData[i - forTrain]);
                res[i] = predictedPreview.Score;
            }

            return res;
        }
    }
}
