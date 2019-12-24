using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.ML;
using TSLab.Script;
using TSLab.Script.Handlers;
using TSLab.Script.Handlers.Options;
using TSLab.Utils;
// ReSharper disable UnusedMember.Global

namespace TSLab.ML.Net
{
    [HandlerCategory("ML.Net")]
    [InputsCount(2)]
    [Input(0, TemplateTypes.DOUBLE, Name = "Preview")]
    [Input(1, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    public sealed class BinaryChartModelTrainer : BinaryChartModelBase<float>, IStreamHandler, INeedVariableName
    {
        public string VariableName { get; set; }

        [HandlerParameter(true, "3", Min = "1", Max = "5", Step = "1", EditorMin = "1")]
        public int NumberOfIterations { get; set; } = 1;

        [HandlerParameter(true, "3", Min = "1", Max = "6", Step = "1", EditorMin = "1")]
        public int TypeOfTrainer { get; set; } = 1;

        [HandlerParameter(true, "0.3", Min = "0", Max = "0.5", Step = "0.1", EditorMin = "0")]
        public double TestSize { get; set; } = 0.2;

        public IList<double> Execute(IList<double> preview, ISecurity source)
        {
            var count = source.Bars.Count;
            if (count == 0)
                return new double[0];
            var context = Context;
            Contract.Assert(context != null);

            var size = HistoryBarsBack * HorizontalLines;
            var forTrain = (int)(TestSize < 1 ? count * (1 - Math.Min(1, TestSize)) : TestSize);

            var chartKey = $"{source.CacheName}.{HistoryBarsBack}.{HorizontalLines}.{count}.{MinHighLowRangePct}";
            var baseKey = $"{VariableName}.{chartKey}.{TestSize}.{NumberOfIterations}.{TypeOfTrainer}";

            return context.LoadObject(baseKey, Make);

            IList<float[]> MakeChartList()
            {
                var chart = context.GetArray<float[]>(count) ?? new float[count][];
                for (var i = 0; i < count; i++)
                    chart[i] = MakeChart(source, i, size, 1);
                return chart;
            }

            IList<double> Make()
            {
                var charts = context.LoadObject($"ChartData.{chartKey}", MakeChartList);

                var trainingData = new List<PredictionData>(forTrain);
                for (var i = 0; i < forTrain; i++)
                {
                    if (i < HistoryBarsBack)
                        continue;
                    var item = MakePredictionData(charts, preview, i, size);
                    item.Probability = DoubleUtil.IsZero(preview[i]) ? 0.5f : 1;
                    trainingData.Add(item);
                }

                var testData = new List<PredictionData>(count - forTrain);
                for (var i = forTrain; i < count; i++)
                {
                    testData.Add(MakePredictionData(charts, preview, i, size));
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

                if (!context.IsOptimization && !source.IsRealtime && !string.IsNullOrEmpty(ModelPath))
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
}
