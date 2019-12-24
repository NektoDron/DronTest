using System;
using System.Collections.Generic;
using System.Linq;
using Common.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace TSLab.ML.Net
{
    public static class ConsoleHelper
    {
        private static readonly ILog s_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static void PrintPrediction(string prediction)
        {
            s_log.Debug($"*************************************************");
            s_log.Debug($"Predicted : {prediction}");
            s_log.Debug($"*************************************************");
        }

        public static void PrintRegressionPredictionVersusObserved(string predictionCount, string observedCount)
        {
            s_log.Debug($"-------------------------------------------------");
            s_log.Debug($"Predicted : {predictionCount}");
            s_log.Debug($"Actual:     {observedCount}");
            s_log.Debug($"-------------------------------------------------");
        }


        public static void PrintBinaryClassificationMetrics(string name, CalibratedBinaryClassificationMetrics metrics)
        {
            s_log.Debug($"************************************************************");
            s_log.Debug($"*       Metrics for {name} binary classification model      ");
            s_log.Debug($"*-----------------------------------------------------------");
            s_log.Debug($"*       Accuracy: {metrics.Accuracy:P2}");
            s_log.Debug($"*       Area Under Curve:      {metrics.AreaUnderRocCurve:P2}");
            s_log.Debug($"*       Area under Precision recall Curve:  {metrics.AreaUnderPrecisionRecallCurve:P2}");
            s_log.Debug($"*       F1Score:  {metrics.F1Score:P2}");
            s_log.Debug($"*       LogLoss:  {metrics.LogLoss:#.##}");
            s_log.Debug($"*       LogLossReduction:  {metrics.LogLossReduction:#.##}");
            s_log.Debug($"*       PositivePrecision:  {metrics.PositivePrecision:#.##}");
            s_log.Debug($"*       PositiveRecall:  {metrics.PositiveRecall:#.##}");
            s_log.Debug($"*       NegativePrecision:  {metrics.NegativePrecision:#.##}");
            s_log.Debug($"*       NegativeRecall:  {metrics.NegativeRecall:P2}");
            s_log.Debug($"************************************************************");
        }

        public static void PrintRegressionMetrics(string name, RegressionMetrics metrics)
        {
            s_log.Debug($"*************************************************");
            s_log.Debug($"*       Metrics for {name} regression model      ");
            s_log.Debug($"*------------------------------------------------");
            s_log.Debug($"*       LossFn:        {metrics.LossFunction:0.##}");
            s_log.Debug($"*       R2 Score:      {metrics.RSquared:0.##}");
            s_log.Debug($"*       Absolute loss: {metrics.MeanAbsoluteError:#.##}");
            s_log.Debug($"*       Squared loss:  {metrics.MeanSquaredError:#.##}");
            s_log.Debug($"*       RMS loss:      {metrics.RootMeanSquaredError:#.##}");
            s_log.Debug($"*************************************************");
        }

        public static double CalculateStandardDeviation(IEnumerable<double> values)
        {
            double average = values.Average();
            double sumOfSquaresOfDifferences = values.Select(val => (val - average) * (val - average)).Sum();
            double standardDeviation = Math.Sqrt(sumOfSquaresOfDifferences / (values.Count() - 1));
            return standardDeviation;
        }

        public static double CalculateConfidenceInterval95(IEnumerable<double> values)
        {
            double confidenceInterval95 = 1.96 * CalculateStandardDeviation(values) / Math.Sqrt((values.Count() - 1));
            return confidenceInterval95;
        }

        public static List<float[]> PeekVectorColumnDataInConsole(MLContext mlContext, string columnName, IDataView dataView, IEstimator<ITransformer> pipeline, int numberOfRows = 4)
        {
            string msg = $"Peek data in DataView: : Show {numberOfRows} rows with just the '{columnName}' column";
            ConsoleWriteHeader(msg);

            var transformer = pipeline.Fit(dataView);
            var transformedData = transformer.Transform(dataView);

            // Extract the 'Features' column.
            var someColumnData = transformedData.GetColumn<float[]>(columnName).Take(numberOfRows).ToList();

            // print to console the peeked rows
            someColumnData.ForEach(row => {
                var concatColumn = string.Empty;
                foreach (float f in row)
                {
                    concatColumn += f.ToString();
                }
                s_log.Debug(concatColumn);
            });

            return someColumnData;
        }

        public static void ConsoleWriteHeader(params string[] lines)
        {
            s_log.Debug(" ");
            foreach (var line in lines)
            {
                s_log.Debug(line);
            }
            var maxLength = lines.Select(x => x.Length).Max();
            s_log.Debug(new string('#', maxLength));
        }

        public static void ConsoleWriterSection(params string[] lines)
        {
            s_log.Debug(" ");
            foreach (var line in lines)
            {
                s_log.Debug(line);
            }
            var maxLength = lines.Select(x => x.Length).Max();
            s_log.Debug(new string('-', maxLength));
        }
    }
}
