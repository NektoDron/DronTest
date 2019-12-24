using System;
using System.Collections.Generic;
using System.Globalization;
using TSLab.Script;
using TSLab.Script.Handlers;
using TSLab.Script.Helpers;

namespace Dron.Indicators
{
    [HandlerCategory("Dron.Indicators")]
    public class NATR : BasePeriodIndicatorHandler, IContextUses, IBar2DoubleHandler
    {
        public IContext Context { get; set; }

        [HandlerParameter(true, "5", Min = "1", Max = "10", Step = "1", EditorMin = "1")]
        public int Interval { get; set; } = 1;

        public static IList<double> NRTR(IContext context, ISecurity sec, IList<double> atrs, double parameter)
        {
            int count = sec.Bars.Count;
            var reverseSeries = context.GetArray<double>(count) ?? new double[count];

            int trend = 0;
            double lPrice = 0;
            double hPrice = 0;

            for (int i = 0; i < count; i++)
            {
                double k = atrs[i] * parameter; //NRTR_ATR parameter
                double close = sec.Bars[i].Close;
                double reverse;
                if (trend >= 0)
                {
                    hPrice = Math.Max(hPrice, close);
                    reverse = hPrice - k;
                    if (close <= reverse)
                    {
                        trend = -1;
                        lPrice = close;
                        reverse = lPrice + k;
                    }
                }
                else
                {
                    lPrice = Math.Min(lPrice, close);
                    reverse = lPrice + k;
                    if (close >= reverse)
                    {
                        trend = 1;
                        hPrice = close;
                        reverse = hPrice - k;
                    }
                }

                reverseSeries[i] = reverse;
            }

            return reverseSeries;
        }

        [HandlerParameter(true, "2", Min = "2", Max = "30", Step = "1", EditorMin = "1")]
        public double Coefficient { get; set; } = 2;

        public virtual IList<double> Execute(ISecurity sec)
        {
            var sec2 = sec.CompressTo(Interval);
            var atrs = Context.GetData("ATR", new[] { Period.ToString(), Interval.ToString() },
                () => Series.AverageTrueRange(sec2.Bars, Period, Context));
            return Context.GetData("NRTR",
                new[] { Period.ToString(), Coefficient.ToString(CultureInfo.InvariantCulture), Interval.ToString() },
                () => sec2.Decompress(NRTR(Context, sec2, atrs, Coefficient)));
        }
    }
}
