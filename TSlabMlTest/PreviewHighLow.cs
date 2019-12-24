using System;
using System.Collections.Generic;
using TSLab.Script;
using TSLab.Script.Handlers;

// ReSharper disable UnusedMember.Global

namespace TSLab.ML.Net
{
    public abstract class PreviewBase : BasePeriodIndicatorHandler, IContextUses
    {
        public IContext Context { get; set; }

        public static IList<Double2> MakeHighLowCache(IContext context, ISecurity source, int period)
        {
            return context.GetData("PreviewBase", new[] { source.CacheName, period.ToString() }, delegate
                  {
                      var bars = source.Bars;
                      var count = bars.Count;
                      var res = context.GetArray<Double2>(count) ?? new Double2[count];

                      for (var i = 0; i < count - period - 1; i++)
                      {
                          var curh = 0.0;
                          var curl = 0.0;
                          var close = bars[i].Close;
                          for (int j = i + 1; j < i + period + 1; j++)
                          {
                              curh = Math.Max(curh, (bars[j].High - close));
                              curl = Math.Min(curl, (bars[j].Low - close));
                          }

                          res[i] = new Double2(curh, curl);
                      }

                      return res;
                  });
        }

        protected IList<double> Make<T>(Func<T, int, IMemoryContext, IList<double>> func, T data, int period)
        {
            return Context.GetData(func.Method.Name, new[] { period.ToString() },
                () => func(data, period, Context));
        }
    }

    [HandlerCategory("ML.Net")]
    public sealed class PreviewGoodEntryLong : PreviewBase, IBar2DoubleHandler
    {
        [HandlerParameter(true, "1", Min = "0.1", Max = "5", Step = "0.1", EditorMin = "0")]
        public double MinProfitPct { get; set; } = 1;

        [HandlerParameter(true, "0.2", Min = "0.1", Max = "5", Step = "0.1", EditorMin = "0")]
        public double MaxLossPct { get; set; } = 1;

        public IList<double> Execute(ISecurity source)
        {
            var bars = source.Bars;
            var count = bars.Count;
            var period = Period;
            var data = MakeHighLowCache(Context, source, period);
            var res = Context?.GetArray<double>(data.Count) ?? new double[data.Count];
            var lastRes = 0.0;
            var lastResNum = 0;
            for (var i = 0; i < count - period - 1; i++)
            {
                if (lastResNum + period < i)
                    lastRes = 0;
                var openPrice = bars[i + 1].Open;
                var testPrice = openPrice;
                var newRes = data[i].V1;
                for (var j = i + 1; j < i + period + 1; j++)
                {
                    if ((testPrice - bars[j].Low) / testPrice * 100 > MaxLossPct) // check for loss
                    {
                        newRes = 0;
                        break;
                    }

                    if (testPrice < bars[j].Close) // shift for stop
                        testPrice = bars[j].Close;
                    if ((bars[j].High - openPrice) / openPrice * 100 > MinProfitPct) // check for destination profit
                    {
                        break;
                    }
                }

                if (newRes <= lastRes)
                {
                    var low = Math.Abs(data[i].V2);
                    if (newRes <= low & low / bars[i].Close * 100 > MinProfitPct)
                    {
                        res[i] = -1;
                    }
                    continue;
                }

                // if (lastResNum > 0 && i - lastResNum < period)
                //     res[lastResNum] = 0;
                lastRes = newRes;
                lastResNum = i;
                res[i] = 1;
            }

            return res;
        }
    }

    [HandlerCategory("ML.Net")]
    public sealed class PreviewGoodEntryShort : PreviewBase, IBar2DoubleHandler
    {
        [HandlerParameter(true, "1", Min = "0.1", Max = "5", Step = "0.1", EditorMin = "0")]
        public double MinProfitPct { get; set; } = 1;

        [HandlerParameter(true, "0.2", Min = "0.1", Max = "5", Step = "0.1", EditorMin = "0")]
        public double MaxLossPct { get; set; } = 1;

        public IList<double> Execute(ISecurity source)
        {
            var bars = source.Bars;
            var count = bars.Count;
            var period = Period;
            var data = MakeHighLowCache(Context, source, period);
            var res = Context?.GetArray<double>(data.Count) ?? new double[data.Count];
            var lastRes = 0.0;
            var lastResNum = 0;
            for (var i = 0; i < count - period - 1; i++)
            {
                if (lastResNum + period < i)
                    lastRes = 0;
                var openPrice = bars[i + 1].Open;
                var testPrice = openPrice;
                var newRes = -data[i].V2;
                for (var j = i + 1; j < i + period + 1; j++)
                {
                    if ((bars[j].High - testPrice) / testPrice * 100 > MaxLossPct) // check for loss
                    {
                        newRes = 0;
                        break;
                    }

                    if (testPrice > bars[j].Close) // shift for stop
                        testPrice = bars[j].Close;
                    if ((openPrice - bars[j].Low) / openPrice * 100 > MinProfitPct) // check for destination profit
                    {
                        break;
                    }
                }

                if (newRes <= lastRes)
                {
                    var high = data[i].V1;
                    if (newRes <= high & high / bars[i].Close * 100 > MinProfitPct)
                    {
                        res[i] = -1;
                    }
                    continue;
                }

                // if (lastResNum > 0 && i - lastResNum < period)
                //     res[lastResNum] = 0;
                lastRes = newRes;
                lastResNum = i;
                res[i] = 1;
            }

            return res;
        }
    }

    [HandlerCategory("ML.Net")]
    public sealed class PreviewHigh  : PreviewBase, IBar2DoubleHandler
    {
        public IList<double> Execute(ISecurity source)
        {
            var data = MakeHighLowCache(Context, source, Period);
            var res = Context?.GetArray<double>(data.Count) ?? new double[data.Count];
            for (var i = 0; i < res.Length; i++)
            {
                res[i] = data[i].V1;
            }
            return res;
        }
    }

    [HandlerCategory("ML.Net")]
    public sealed class PreviewLow : PreviewBase, IBar2DoubleHandler
    {
        public IList<double> Execute(ISecurity source)
        {
            var data = MakeHighLowCache(Context, source, Period);
            var res = Context?.GetArray<double>(data.Count) ?? new double[data.Count];
            for (var i = 0; i < res.Length; i++)
            {
                res[i] = data[i].V2;
            }
            return res;
        }
    }
}
