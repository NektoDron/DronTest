using System;
using System.Collections.Generic;
using System.Linq;
using TSLab.DataSource;
using TSLab.Script;
using TSLab.Script.Handlers;
using TSLab.Script.Helpers;
using TSLab.Script.Optimization;
using TrailStop = Dron.Indicators.TrailStop;

namespace TSLab.ML.Net
{
    public class Test : IExternalScript
    {
        public OptimProperty BtLongStep { get; set; } = new OptimProperty(4.75, 0, 6, 0.25);

        public OptimProperty BtShortStep { get; set; } = new OptimProperty(4.75, 0, 6, 0.25);

        public OptimProperty BtInterval { get; set; } = new OptimProperty(1440, 600, 2000, 60);

        public OptimProperty BtPeriod { get; set; } = new OptimProperty(5, 1, 10, 1);

        public OptimProperty GdLongLow { get; set; } = new OptimProperty(5, 2, 20, 1);

        public OptimProperty GdShortHigh { get; set; } = new OptimProperty(5, 2, 20, 1);

        public OptimProperty LongExitPeriod { get; set; } = new OptimProperty(60, 20, 120, 10);

        public OptimProperty LongExitCoef { get; set; } = new OptimProperty(1.5, 0.5, 14, 0.5);

        public OptimProperty ShortExitPeriod { get; set; } = new OptimProperty(60, 20, 120, 10);

        public OptimProperty ShortExitCoef { get; set; } = new OptimProperty(1.5, 0.5, 14, 0.5);

        public OptimProperty GLExitPeriod { get; set; } = new OptimProperty(60, 20, 120, 10);

        public OptimProperty GLExitCoef { get; set; } = new OptimProperty(3.5, 0.1, 16, 0.5);

        public OptimProperty GSExitPeriod { get; set; } = new OptimProperty(60, 20, 120, 10);

        public OptimProperty GSExitCoef { get; set; } = new OptimProperty(3.5, 0.1, 16, 0.5);

        public OptimProperty NtLongLow { get; set; } = new OptimProperty(25, 10, 100, 5);

        public OptimProperty NtLongHigh { get; set; } = new OptimProperty(25, 10, 100, 5);

        public OptimProperty NtShortLow { get; set; } = new OptimProperty(25, 10, 100, 5);

        public OptimProperty NtShortHigh { get; set; } = new OptimProperty(25, 10, 100, 5);

        //public OptimProperty NtLongStop { get; set; } = new OptimProperty(0.1, 0.0, 0.5, 0.025);
        //public OptimProperty NtShortStop { get; set; } = new OptimProperty(0.1, 0.0, 0.5, 0.025);

        public OptimProperty LsTrailEnable { get; set; } = new OptimProperty(0.1, 0.0, 0.5, 0.025);

        public OptimProperty LsTrailStop { get; set; } = new OptimProperty(0.1, 0.0, 0.5, 0.025);

        public OptimProperty LsStop { get; set; } = new OptimProperty(0.1, 0.0, 0.5, 0.025);
        //public OptimProperty LsAtrPeriod { get; set; } = new OptimProperty(10, 2, 20, 1);
        //public OptimProperty LsAtrCoef { get; set; } = new OptimProperty(1, 0.5, 5.5, 0.25);

        public OptimProperty SsTrailEnable { get; set; } = new OptimProperty(0.1, 0.0, 0.5, 0.025);

        public OptimProperty SsTrailStop { get; set; } = new OptimProperty(0.1, 0.0, 0.5, 0.025);

        public OptimProperty SsStop { get; set; } = new OptimProperty(0.1, 0.0, 0.5, 0.025);


        public OptimProperty PosStep { get; set; } = new OptimProperty(1, 1, 10, 1);

        public OptimProperty PosMax { get; set; } = new OptimProperty(9, 5, 15, 1);

        public enum TrendState
        {
            NoTrend = 0,
            GoodLong = 2,
            Long = 1,
            Short = -1,
            GoodShort = -2
        }

        public void Execute(IContext ctx, ISecurity sec)
        {
            var count = sec.Bars.Count;
            var tradeFrom = Math.Max(500, BtInterval * BtPeriod * 2);

            var dcTrend = Trend.CalcTrend(ctx, sec, BtInterval, BtPeriod, out var dcHigh, out var dcLow);

            ///////

            var ntLongHigh = ctx.GetData("ntLongHigh", new[] { NtLongHigh.ToString() },
                () => Series.Highest(sec.HighPrices, NtLongHigh));
            var ntShortHigh = ctx.GetData("ntShortHigh", new[] { NtShortHigh.ToString() },
                () => Series.Highest(sec.HighPrices, NtShortHigh));
            var ntShortLow = ctx.GetData("ntShortLow", new[] { NtShortLow.ToString() },
                () => Series.Lowest(sec.LowPrices, NtShortLow));
            var ntLongLow = ctx.GetData("ntLongLow", new[] { NtLongLow.ToString() },
                () => Series.Lowest(sec.LowPrices, NtLongLow));

            ///////

            var gdLongLow = ctx.GetData("gdLongLow", new[] { GdLongLow.ToString() },
                () => Series.Lowest(sec.LowPrices, GdLongLow));
            var gdShortHigh = ctx.GetData("gdShortHigh", new[] { GdShortHigh.ToString() },
                () => Series.Highest(sec.HighPrices, GdShortHigh));

            ///////

            var longExit = ctx.GetData("longExit", new[] { LongExitPeriod.ToString(), LongExitCoef.ToString() },
                () => Series.BollingerBands(sec.HighPrices, LongExitPeriod, LongExitCoef, true));
            var glExit = ctx.GetData("glExit", new[] { GLExitPeriod.ToString(), GLExitCoef.ToString() },
                () => Series.BollingerBands(sec.HighPrices, GLExitPeriod, GLExitCoef, true));
            var shortExit = ctx.GetData("shortExit", new[] { ShortExitPeriod.ToString(), ShortExitCoef.ToString() },
                () => Series.BollingerBands(sec.LowPrices, ShortExitPeriod, ShortExitCoef, false));
            var gsExit = ctx.GetData("gsExit", new[] { GSExitPeriod.ToString(), GSExitCoef.ToString() },
                () => Series.BollingerBands(sec.LowPrices, GSExitPeriod, GSExitCoef, false));

            ///////

            var trendState = ctx.GetArray<TrendState>(count);

            var lStop = new TrailStop
                            {
                                Context = ctx, TrailEnable = LsTrailEnable, TrailLoss = LsTrailStop, StopLoss = LsStop
                            };
            var sStop = new TrailStop
                            {
                                Context = ctx, TrailEnable = SsTrailEnable, TrailLoss = SsTrailStop, StopLoss = SsStop
                            };

            var maxShares = PosMax * PosStep;
            for (var bar = tradeFrom; bar < count; bar++)
            {
                var b = sec.Bars[bar];
                var d = dcTrend[bar];
                var tr = (d / dcTrend[bar - 1] - 1) * 1e6;
                TrendState ts;
                if (tr > BtLongStep)
                    ts = b.Close > dcHigh[bar] ? TrendState.GoodLong : TrendState.Long;
                else if (tr < -BtShortStep)
                    ts = b.Close < dcLow[bar] ? TrendState.GoodShort : TrendState.Short;
                else
                    ts = TrendState.NoTrend;
                trendState[bar] = ts;

                var le = sec.Positions.GetLastLongPositionActive(bar);
                var se = sec.Positions.GetLastShortPositionActive(bar);

                #region Long

                if (le == null)
                {
                    if (ts == TrendState.GoodLong)
                        sec.Positions.BuyAtPrice(bar + 1, PosStep, gdLongLow[bar], "LE");
                    //if (ts == TrendState.NoTrend /*|| ts == TrendState.Short*/)
                    //    sec.Positions.BuyAtPrice(bar + 1, PosStep, ntLongLow[bar], "LE");
                }
                else
                {
                    var price = le.GetBalancePrice(bar);
                    var shares = le.GetShares(bar);
                    if (shares < maxShares)
                    {
                        if (ts == TrendState.GoodLong)
                        {
                            le.ChangeAtPrice(bar + 1, gdLongLow[bar], shares + PosStep, "LC-GD");
                        }
                        else
                        {
                            if (ntLongLow[bar] < price)
                                le.ChangeAtPrice(bar + 1, ntLongLow[bar], shares + PosStep, "LC-NT");
                        }
                    }

                    if (ts == TrendState.GoodLong)
                        le.ChangeAtProfit(bar + 1, glExit[bar], Math.Max(0, shares - PosStep), "LGX");
                    else if (ts == TrendState.Short)
                        le.ChangeAtProfit(bar + 1, ntLongHigh[bar], Math.Max(0, shares - PosStep), "LSX");
                    else
                        le.ChangeAtProfit(bar + 1, longExit[bar], Math.Max(0, shares - PosStep), "LX");

                    var stop = lStop.Execute(le, bar);
                    le.CloseAtStop(bar + 1, stop, "LS");
                }

                #endregion

                #region Short

                if (se == null)
                {
                    if (ts == TrendState.GoodShort)
                        sec.Positions.SellAtPrice(bar + 1, PosStep, gdShortHigh[bar], "SE");
                    //if (ts == TrendState.NoTrend /*|| ts == TrendState.Short*/)
                    //    sec.Positions.SellAtPrice(bar + 1, PosStep, ntLongLow[bar], "SE");
                }
                else
                {
                    var price = se.GetBalancePrice(bar);
                    var shares = se.GetShares(bar);
                    if (shares > -maxShares)
                    {
                        if (ts == TrendState.GoodShort)
                        {
                            se.ChangeAtPrice(bar + 1, gdShortHigh[bar], shares - PosStep, "SC-GD");
                        }
                        else
                        {
                            if (ntShortHigh[bar] > price)
                                se.ChangeAtPrice(bar + 1, ntShortHigh[bar], shares - PosStep, "SC-NT");
                        }
                    }

                    if (ts == TrendState.GoodShort)
                        se.ChangeAtProfit(bar + 1, gsExit[bar], Math.Max(0, shares + PosStep), "SGX");
                    else if (ts == TrendState.Long)
                        se.ChangeAtProfit(bar + 1, ntShortLow[bar], Math.Max(0, shares + PosStep), "SSX");
                    else
                        se.ChangeAtProfit(bar + 1, shortExit[bar], Math.Max(0, shares + PosStep), "SX");

                    //var stop = price * (1 + NtShortStop / 100.0);
                    var stop = sStop.Execute(se, bar);
                    se.CloseAtStop(bar + 1, stop, "SS");
                }

                #endregion
            }

            if (ctx.IsOptimization)
                return;

            var mainPane = ctx.Panes.OfType<IGraphPane>().FirstOrDefault()
                           ?? ctx.CreateGraphPane("MainWindow", "Main Window");
            var barsList = mainPane.GetLists().OfType<IBarsGraphList>().FirstOrDefault() ?? mainPane.AddList("Bars",
                               sec, CandleStyles.BAR_CANDLE, new Color(255, 255, 255), PaneSides.RIGHT);

            mainPane.AddList("btHigh", "btHigh", dcHigh, ListStyles.LINE, new Color(0, 0, 155), LineStyles.SOLID,
                PaneSides.RIGHT);
            mainPane.AddList("btLow", "btLow", dcLow, ListStyles.LINE, new Color(155, 0, 0), LineStyles.SOLID,
                PaneSides.RIGHT);
            // mainPane.AddList("Middle", "Middle", btSec.Decompress(btMiddle), ListStyles.LINE, new Color(255, 0, 0), LineStyles.SOLID,
            //     PaneSides.RIGHT);
            mainPane.AddList(nameof(dcTrend), "Trend", dcTrend, ListStyles.LINE, new Color(0, 255, 0), LineStyles.SOLID,
                PaneSides.RIGHT);

            for (var bar = tradeFrom; bar < sec.Bars.Count; bar++)
            {
                Color barColor;
                switch (trendState[bar])
                {
                    case TrendState.GoodLong:
                        barColor = new Color(0, 0, 200);
                        break;
                    case TrendState.Long:
                        barColor = new Color(0, 0, 100);
                        break;
                    case TrendState.GoodShort:
                        barColor = new Color(200, 0, 0);
                        break;
                    case TrendState.Short:
                        barColor = new Color(100, 0, 0);
                        break;
                    default:
                        barColor = new Color(0, 200, 0);
                        break;
                }

                barsList.SetColor(bar, barColor);
            }
        }
    }

    [HandlerCategory("ML.Net")]
    public class TrendFlags : Trend
    {
        [HandlerParameter(true, "4.75", Min = "0", Max = "6", Step = "0.25", Name = "Long Step")]
        public double BtLongStep { get; set; } = 4.75;

        [HandlerParameter(true, "4.75", Min = "0", Max = "6", Step = "0.25", Name = "Short Step")]
        public double BtShortStep { get; set; } = 4.75;

        public override IList<double> Execute(ISecurity source)
        {
            var count = source.Bars.Count;
            var dcTrend = CalcTrend(Context, source, BtInterval, BtPeriod, out var dcHigh, out var dcLow);
            var trendState = Context.GetArray<double>(count);
            for (var bar = 1; bar < count; bar++)
            {
                var b = source.Bars[bar];
                var d = dcTrend[bar];
                var tr = (d / dcTrend[bar - 1] - 1) * 1e6;
                int ts;
                if (tr > BtLongStep)
                    ts = b.Close > dcHigh[bar] ? 2 : 1;
                else if (tr < -BtShortStep)
                    ts = b.Close < dcLow[bar] ? -2 : -1;
                else
                    ts = 0;
                trendState[bar] = ts;
            }

            return trendState;
        }
    }

    [HandlerCategory("ML.Net")]
    public class Trend : IContextUses, IBar2DoubleHandler
    {
        [HandlerParameter(true, "1440", Min = "300", Max = "1440", Step = "60", Name = "Interval")]
        public int BtInterval { get; set; } = 1440;

        [HandlerParameter(true, "5", Min = "1", Max = "10", Step = "1", Name = "Period")]
        public int BtPeriod { get; set; } = 5;

        public IContext Context { get; set; }

        public virtual IList<double> Execute(ISecurity source)
        {
            return CalcTrend(Context, source, BtInterval, BtPeriod, out _, out _);
        }

        public static IList<double> CalcTrend(IContext ctx, ISecurity sec, int btInterval, int btPeriod,
                                        out IList<double> dcHigh, out IList<double> dcLow)
        {
            var btSec = sec.CompressTo(new Interval(btInterval, sec.IntervalBase));

            var btParameters = new[] { sec.CacheName, btInterval.ToString(), btPeriod.ToString() };
            var btHigh = ctx.GetData("$btHigh", btParameters, () => Series.Highest(btSec.HighPrices, btPeriod));
            var btLow = ctx.GetData("$btLow", btParameters, () => Series.Lowest(btSec.LowPrices, btPeriod));
            var btMiddle = ctx.GetData("$btMiddle", btParameters,
                () => btHigh.Zip(btLow, (h, l) => (h + l) / 2).ToArray());

            var btTrend = (IReadOnlyList<double>)ctx.GetData("$btTrend", btParameters, () => btMiddle.Difference(ctx));

            IList<double> DcTrendMaker() =>
                Extensions.Decompress(sec.Bars, sec.IntervalInstance, (IReadOnlyList<double>)btMiddle, btTrend,
                    btSec.IntervalInstance, btSec.Bars, ctx);

            var dcTrend = ctx.GetData("$dcTrend", btParameters, DcTrendMaker);

            dcHigh = ctx.GetData("$dcHigh", btParameters, () => btSec.Decompress(btHigh));
            dcLow = ctx.GetData("$dcLow", btParameters, () => btSec.Decompress(btLow));
            return dcTrend;
        }
    }

    public static class Extensions
    {
        public static IList<double> Difference(this IList<double> data, IMemoryManagement mm = null)
        {
            var count = data.Count;
            var res = mm?.GetArray<double>(count) ?? new double[count];
            for (var i = 1; i < count; i++)
                res[i] = data[i] - data[i - 1];
            return res;
        }

        // public static IList<double> Decompress(this ISecurity sec, IReadOnlyList<double> data, IMemoryManagement mm = null,
        //                                         IReadOnlyList<double> increment = null)
        // {
        //     return Decompress(sec.P)
        // }

        public static IList<double> Decompress<T>(IReadOnlyList<T> originalBars, Interval curInterval,
                                                   IReadOnlyList<double> data, IReadOnlyList<double> increment,
                                                   Interval dataInterval, IReadOnlyList<T> compressedBars = null,
                                                   IMemoryManagement mm = null)
            where T : class, IBaseBar
        {
            if (originalBars == null)
                throw new ArgumentNullException(nameof(originalBars));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var count = originalBars.Count;
            if (compressedBars == null)
            {
                compressedBars = BarUtils.CompressTo(originalBars, dataInterval, curInterval);
            }

            var newBars = mm?.GetArray<double>(count) ?? new double[count];
            var dataIntervalShift = dataInterval.Shift;
            var shift = (dataIntervalShift - curInterval.Shift).Ticks;
            var step = curInterval.Shift.Ticks;
            var steps = dataIntervalShift.Ticks / step;

            var k = -1;
            var ticks = 0L;
            for (var i = 0; i < originalBars.Count; i++)
            {
                var bar = originalBars[i];
                var t = bar.Ticks;
                while (t >= ticks + shift && k < data.Count - 1)
                {
                    var index = Math.Min(++k + 1, compressedBars.Count - 1);
                    ticks = compressedBars[index].Ticks;
                }

                var curStep = (ticks - t - shift) / step + 1;
                var ci = Math.Max(k, 0);
                newBars[i] = data[ci] - increment[ci] * ((double)curStep / steps + 2);
            }

            return newBars;
        }
    }
}
