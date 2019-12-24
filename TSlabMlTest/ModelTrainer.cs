using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Common.Logging;
using Microsoft.ML.Data;
using TSLab.DataSource;
using TSLab.Script;
using TSLab.Script.Handlers;
using TSLab.Script.Handlers.Options;
using TSLab.Script.Helpers;
#pragma warning disable 612

// ReSharper disable ConditionIsAlwaysTrueOrFalse

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming
namespace TSLab.ML.Net
{
    [HandlerCategory("ML.Net")]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    public sealed class ModelTrainer : PreviewBase, IBar2DoubleHandler
    {
        private static readonly ILog s_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        [HandlerParameter(true, "20", Min = "5", Max = "50", Step = "5", EditorMin = "4")]
        public int HistoryBarsBack { get; set; } = 20;

        [HandlerParameter(true, "5", Min = "1", Max = "24", Step = "1", EditorMin = "1")]
        public int CalcLastNumModels { get; set; } = 5;

        [HandlerParameter(true, "5", Min = "1", Max = "24", Step = "1", EditorMin = "1")]
        public int ChangeMinutes { get; set; } = 5;

        [HandlerParameter(true, "20", Min = "1", Max = "50", Step = "1", EditorMin = "1")]
        public int TrainDays { get; set; } = 20;

        [HandlerParameter(true, "true", NotOptimized = true)]
        public bool HighOrLow { get; set; } = true;

        public class PredictionData
        {
            public VBuffer<float> Features;

            public float Preview;
        }

        public class PredictionResult
        {
            [ColumnName("Score")]
            public float PredictedPreview;
        }

        public IList<double> Execute(ISecurity source)
        {
            var count = source.Bars.Count;
            if (count == 0)
                return new double[0];
            var context = Context;
            Contract.Assert(context != null);

            /*            var usedVars = 1;
                        if (UseHigh)
                            usedVars++;
                        if (UseLow)
                            usedVars++;
                        if (UseVolume)
                            usedVars++;

                        var size = HistoryBarsBack * usedVars;*/
            var key = $"PredictionData.{source.CacheName}.{HighOrLow}.{Period}.{HistoryBarsBack}";
            var data = context.LoadObject<PredictionData[]>(key, () => null);
            if (data?.Length != count)
                data = null;

            int size = 6;
            if (data == null)
            {
                var preview = MakeHighLowCache(context, source, Period);
                var closes = source.ClosePrices;

                var rsi = Make(Series.RSI, closes, HistoryBarsBack);
                var cci = Make(Series.CCI, source.Bars, HistoryBarsBack);
                var stDev = Make(Series.StDev, closes, HistoryBarsBack);
                var highs = Make(Series.Highest, source.HighPrices, HistoryBarsBack);
                var lows = Make(Series.Lowest, source.LowPrices, HistoryBarsBack);
                var ema = Make(Series.EMA, closes, HistoryBarsBack);
                var ema2 = Make(Series.EMA, closes, HistoryBarsBack / 2);
                var ema3 = Make(Series.EMA, closes, HistoryBarsBack * 2);

                data = context.GetArray<PredictionData>(count) ?? new PredictionData[count];
                for (var i = 0; i < data.Length; i++)
                {
                    var label = (float)(HighOrLow ? preview[i].V1 : -preview[i].V2);
                    var values = new[]
                                     {
                                         (float)(ema[i] - closes[i]), (float)(ema3[i] - closes[i]),
                                         (float)(ema3[i] - closes[i]), (float)rsi[i], (float)(ema[i] - ema2[i]),
                                         (float)cci[i], (float)stDev[i], (float)(highs[i] - closes[i]),
                                         (float)(lows[i] - closes[i])
                                     };
                    size = values.Length;

                    data[i] = new PredictionData
                                  {
                                      Features = new VBuffer<float>(values.Length, values), Preview = label
                                  };
                }

                context.StoreObject(key, data);
            }
            else
            {
                size = data[0].Features.Length;
            }

            var splitInterval = new Interval(ChangeMinutes, DataIntervals.MINUTE);
            var helper = new SeparatedModelHelper<PredictionData, PredictionResult>(context, TrainDays,
                splitInterval, CalcLastNumModels);
            return helper.Build(key, source.Bars, data, size, Period,
                res => res.PredictedPreview * (HighOrLow ? 1 : -1));
        }

    }
}
