using System;
using System.Collections.Generic;
using TSLab.DataSource;
using TSLab.Script;
using TSLab.Script.Handlers;
using TSLab.Script.Helpers;
using TSLab.Utils;

// ReSharper disable UnusedMember.Global

namespace Dron.Indicators
{
    [HandlerCategory("Dron.Indicators")]
    public class ATRM : BasePeriodIndicatorHandler, IContextUses, IBar2DoubleHandler
    {
        public IContext Context { get; set; }

        [HandlerParameter(true, "true")]
        public bool PositiveSide { get; set; }

        /// <summary>
        /// Calculate ATRM indicator for bars list
        /// </summary>
        public static IList<double> CalcATRM(IContext context, IReadOnlyList<IDataBar> bars, int period,
                                             Interval roundInterval, bool positive)
        {
            var count = bars.Count;
            var atrs = context.GetArray<double>(count) ?? new double[count];
            if (count == 0)
                return atrs;

            double high = 0, low = 0;
            double kr = 0, no = 0;

            var date = roundInterval.AlignDate(bars[0].Date);
            for (var i = 0; i < period; i++)
            {
                atrs[i] = 0;
            }

            double prevATR = 0;
            var first = true;
            for (var i = period; i < count; i++)
            {
                prevATR = Series.AverageTrueRange(bars, i, period, prevATR);
                var kirPer = prevATR;
                prevATR = kirPer;
                if (DoubleUtil.IsZero(kirPer))
                {
                    kirPer = 1e-10;
                }

                var date2 = roundInterval.AlignDate(bars[i].Date);
                var cur = bars[i].Close;
                if (first || date < date2)
                {
                    first = false;
                    date = date2;
                    high = low = cur;
                    kr = no = 0;
                }

                if (cur > high + kirPer)
                {
                    var kk = Math.Floor((cur - (high + kirPer)) / kirPer) + 1;
                    high = cur;
                    low = cur - kirPer;
                    kr = kr + kk;
                    no = 0;
                }

                if (cur < low - kirPer)
                {
                    var kn = Math.Floor((low - kirPer - cur) / kirPer) + 1;
                    low = cur;
                    high = cur + kirPer;
                    no = no + kn;
                    kr = 0;
                }

                var lowValue = -no;
                var highValue = kr;
                var value = highValue + lowValue;
                if (positive && value < 0)
                {
                    value = 0;
                }

                if (!positive && value > 0)
                {
                    value = 0;
                }

                atrs[i] = value;
            }

            return atrs;
        }

        public virtual IList<double> Execute(ISecurity source)
        {
            return CalcATRM(Context, source.Bars, Period, new Interval(1, DataIntervals.DAYS), PositiveSide);
        }
    }

    [HandlerCategory("ML.Net")]
    public class CompressedATRM : ATRM
    {
        [HandlerParameter(true, "5", Min = "1", Max = "10", Step = "1", EditorMin = "1")]
        public int Interval { get; set; } = 1;

        public override IList<double> Execute(ISecurity source)
        {
            var sec2 = source.CompressTo(new Interval(Interval, source.IntervalBase));
            return Context.GetData("ATRM", new[] { Period.ToString(), Interval.ToString(), PositiveSide.ToString() },
                () => sec2.Decompress(CalcATRM(Context, sec2.Bars, Period, new Interval(1, DataIntervals.DAYS),
                    PositiveSide)));
        }
    }
}