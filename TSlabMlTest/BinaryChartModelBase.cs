using System;
using System.Collections.Generic;
using Microsoft.ML.Data;
using TSLab.Script;
using TSLab.Script.Handlers;

namespace TSLab.ML.Net
{
    public abstract class BinaryChartModelBase<T> : BinaryModelBase<T>
        where T : struct
    {
        [HandlerParameter(true, "100", Min = "5", Max = "200", Step = "10", EditorMin = "5")]
        public int HorizontalLines { get; set; } = 20;

        [HandlerParameter(true, "1", Min = "0", Max = "2", Step = "0.25", EditorMin = "0")]
        public double MinHighLowRangePct { get; set; } = 1;

        protected PredictionData MakePredictionData(IList<T[]> charts, IList<double> preview, int i, int size)
        {
            return new PredictionData { Features = new VBuffer<T>(size, charts[i]), Preview = preview[i] > 0 };
        }

        protected T[] MakeChart(ISecurity source, int i, int size, T fillValue)
        {
            var values = Context.GetArray<T>(size) ?? new T[size];
            if (i < HistoryBarsBack)
                return values;
            var high = double.MinValue;
            var low = double.MaxValue;
            for (var j = 0; j < HistoryBarsBack; j++)
            {
                var b = source.Bars[i - j];
                high = Math.Max(high, b.High);
                low = Math.Min(low, b.Low);
            }

            var range = (high - low) / source.Bars[i].Close * 100;
            if (range < MinHighLowRangePct)
            {
                var d = (MinHighLowRangePct - range)/200;
                high *= 1 + d;
                low *= 1 - d;
            }

            var step = (high - low) / HorizontalLines;

            for (var j = 0; j < HistoryBarsBack; j++)
            {
                var b = source.Bars[i - j];
                var index = j * HorizontalLines;
                var ih = (int)((b.High - low) / step);
                var il = (int)((b.Low - low) / step);
                for (var k = il + index; k < ih + index; k++)
                    values[k] = fillValue;
            }

            return values;
        }
    }
}