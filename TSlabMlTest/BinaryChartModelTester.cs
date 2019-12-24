using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.ML.Data;
using TSLab.Script;
using TSLab.Script.Handlers;
using TSLab.Script.Handlers.Options;

namespace TSLab.ML.Net
{
    [HandlerCategory("ML.Net")]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    public sealed class BinaryChartModelTester : BinaryChartModelBase<float>, IBar2DoubleHandler
    {
        [HandlerParameter(true, NotOptimized = true)]
        public int TestStartBar { get; set; } = 20;

        public IList<double> Execute(ISecurity source)
        {
            var count = source.Bars.Count;
            if (count == 0)
                return new double[0];
            var size = HistoryBarsBack * HorizontalLines;

            var scheme = GetSchemaDefinition(size);
            var mlContext = MakeMlContext();

            var file = new FileInfo(ModelPath);
            if (!file.Exists)
                throw new ScriptException($"File '{ModelPath}' isn't found!");
            var key = $"{file.LastWriteTime}:{ModelPath}";

            var trainedModel = Context.LoadGlobalObject(key, () => mlContext.Model.Load(ModelPath, out _));

            var predictionEngine =
                mlContext.Model.CreatePredictionEngine<PredictionData, PredictionResult>(trainedModel,
                    inputSchemaDefinition: scheme);
            var res = Context.GetArray<double>(count) ?? new double[count];
            for (var i = TestStartBar; i < count; i++)
            {
                var chart = MakeChart(source, i, size, 1);
                var data = new PredictionData { Features = new VBuffer<float>(size, chart) };
                var predictedPreview = predictionEngine.Predict(data);
                res[i] = predictedPreview.Score;
            }

            return res;
        }
    }
}