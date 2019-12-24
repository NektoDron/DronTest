using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.TimeSeries;
using TSLab.Script.Handlers;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace TSLab.ML.Net
{
    [HandlerCategory("ML.Net")]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    public class SsaPreview : IContextUses, IDouble2DoubleHandler
    {
        [HandlerParameter(true, "1", Min = "0", Max = "3", Step = "1", EditorMin = "0")]
        public int OutputNumber { get; set; }

        [HandlerParameter(true, "95", Min = "0", Max = "100", Step = "5", EditorMin = "0")]
        public int Confidence { get; set; }

        sealed class ChangePointPrediction
        {
            [VectorType(4)]
            public double[] Prediction { get; set; }
        }

        public class PredictionData
        {
            public float Value;

            public static PredictionData MakePredictionData(double b)
            {
                return new PredictionData { Value = (float)b };
            }
        }

        public IContext Context { get; set; }

        public IList<double> Execute(IList<double> source)
        {
            var count = source.Count;
            if (count == 0)
                return new double[0];

            var res = new double[count];

            var context = Context;
            Contract.Assert(context != null);

            var size = count / 2;
            var data = source.Take(size).Select(PredictionData.MakePredictionData);

            var ml = new MLContext();
            var dataView = ml.Data.LoadFromEnumerable(data);

            const string outputColumnName = "Prediction";
            const string inputColumnName = "Value";
            var args = new SsaChangePointDetector.Arguments()
                           {
                               Source = inputColumnName,
                               Name = outputColumnName,
                               Confidence = Confidence,        // The confidence for spike detection in the range [0, 100]
                               ChangeHistoryLength = size / 4, // The length of the sliding window on p-values for computing the martingale score. 
                               TrainingWindowSize = size / 2,  // The number of points from the beginning of the sequence used for training.
                               SeasonalWindowSize = size / 8,  // An upper bound on the largest relevant seasonality in the input time - series."
                           };

            // Train the change point detector.
            ITransformer model = new SsaChangePointEstimator(ml, args).Fit(dataView);

            // Create a prediction engine from the model for feeding new data.
            var engine = model.CreateTimeSeriesPredictionFunction<PredictionData, ChangePointPrediction>(ml);

            for (var i = size; i < count; i++)
            {
                var prediction = engine.Predict(PredictionData.MakePredictionData(source[i]));
                res[i] = prediction.Prediction[OutputNumber];
            }

            return res;
        }
    }
}
