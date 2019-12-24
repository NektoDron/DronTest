using System;
using System.Collections.Generic;
using System.Linq;
using TSLab.Script;
using TSLab.Script.Handlers;
using TSLab.Script.Handlers.Options;
using TSLab.Script.Helpers;
using TSLab.Script.Options;
using DayOfWeek = System.DayOfWeek;
// ReSharper disable UnusedMember.Global

namespace Dron.Indicators
{
    public enum Expiration
    {
        Week = 'W',
        Month = 'M',
        Quarter = 'Q'
    }

    public static class ExpirationUtil
    {
        public static Expiration FromChar(char exp)
        {
            switch (exp)
            {
                case 'W':
                    return Expiration.Week;
                case 'M':
                    return Expiration.Month;
                default:
                    return Expiration.Quarter;
            }
        }

        public static double GetDt(this DateTime ct, Expiration expiration)
        {
            var et = ct.GetExpirationDate(expiration);
            var dT = OptionUtils.YearsBetweenDates(et, ct);
            return dT;
        }

        public static DateTime GetExpirationDate(this DateTime ct, Expiration expiration)
        {
            var et = new DateTime(ct.Year, ct.Month, ct.Day, 8, 0, 0);
            while (et < ct || et.DayOfWeek != DayOfWeek.Friday || expiration != Expiration.Week && et.Day < 23
                   || expiration == Expiration.Quarter && (et.Month - 1) % 4 == 0)
                et = et.AddDays(1);
            return et;
        }

        public static double GetPositionLastPrice(this IPosition position, int barNum)
        {
            var changeInfos = position.ChangeInfos;
            for (var i = changeInfos.Count - 1; i >= 0; i--)
            {
                var changeInfo = changeInfos[i];
                if (changeInfo.EntryBarNum >= 0)
                    return changeInfo.EntryPrice;
            }

            return position.EntryPrice;
        }

        public static double GetOptPx(this double strikePrice, double strikeStep, bool isCall, double price, double dT, double sigma)
        {
            var optPx = FinMath.GetOptionPrice(price, strikePrice, dT, sigma, 0, isCall);
            optPx /= price;
            optPx = Math.Truncate(optPx / strikeStep + 1) * strikeStep;
            return Math.Max(optPx, strikeStep);
        }

        public static IList<double> CalcVolatility(this ISecurity sec, IMemoryContext context)
        {
            var bars = sec.Bars;
            var count = bars.Count;
            var res = context?.GetArray<double>(count) ?? new double[count];
            for (var i = 1; i < count; i++)
            {
                res[i] = Math.Log(bars[i].Close) - Math.Log(bars[i - 1].Close);
            }

            var barsPerDay = 1440 / (int)sec.IntervalInstance.ToMinutes();
            var stDev = Series.StDev(res, barsPerDay, context);
            var coef = Math.Sqrt(1.0 / (barsPerDay * 365));
            for (var i = 1; i < count; i++)
            {
                res[i] = stDev[i] / coef;
            }

            return res;
        }
    }

    public abstract class DronOptionBase : IContextUses
    {
        public IContext Context { get; set; }

        private IList<double> m_volatility;

        public IList<double> GetVolatility(ISecurity sec)
        {
            return m_volatility ?? (m_volatility = OptionVolatility.GetVolatility(sec, Context));
        }
    }

    public abstract class BuyVirtualBase : DronOptionBase
    {
        [HandlerParameter(true, "0.1", Min = "0.1", Max = "5", Step = "0.1", EditorMin = "0.1")]
        public double MaxShares { get; set; }

        [HandlerParameter(true, "Week")]
        public Expiration Expiration { get; set; } = Expiration.Week;

        public char ExpPrefix => (char)Expiration;

        public double MakeDeal(ISecurity source, IOptionStrike strike, ISecurity priceSec, double strikePrice,
                               bool signal, int barNum, bool isCall)
        {
            var optionLot = strike.LotTick;
            var strikeStep = strike.Tick;
            var sigma = GetVolatility(source)[barNum];
            var ct = source.Bars[barNum].Date;
            var dT = ct.GetDt(Expiration);
            var price = priceSec.Bars[barNum].Close;
            var optPx = strikePrice.GetOptPx(strikeStep, isCall, price, dT, sigma);
            if (!signal)
                return optPx;
            var prefix = isCall ? 'C' : 'P';
            var name = $"{prefix}{ExpPrefix}{strikePrice:0}";
            var oldPos = source.Positions.GetLastActiveForSignal(name, barNum);
            if (oldPos == null)
            {
                source.Positions.MakeVirtualPosition(barNum + 1, optionLot, optPx, name);
            }
            else
            {
                var shares = oldPos.GetShares(barNum);
                if (shares < MaxShares && oldPos.GetPositionLastPrice(barNum) > optPx)
                {
                    oldPos.VirtualChange(barNum + 1, optPx, shares + optionLot, $"C-{name}");
                }
            }

            return optPx;
        }
    }

    public class OptionVolatility : IBar2DoubleHandler, IContextUses
    {
        public IList<double> Execute(ISecurity source)
        {
            throw new NotImplementedException();
        }

        public static IList<double> GetVolatility(ISecurity sec, IContext context)
        {
            return context.GetData("$OptionVolatility", new[] { sec.CacheName, sec.IntervalInstance.ToString() },
                () => sec.CalcVolatility(context));
        }

        public IContext Context { get; set; }
    }

    [HandlerCategory("Dron.Options")]
    [HelperName("Buy Virtual Put", Language = Constants.En)]
    [InputsCount(5)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [Input(1, TemplateTypes.OPTION, Name = Constants.OptionSource)]
    [Input(2, TemplateTypes.SECURITY, Name = "TestPriceSource")]
    [Input(3, TemplateTypes.DOUBLE, Name = Constants.Strike)]
    [Input(4, TemplateTypes.BOOL, Name = "Signal")]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    public class BuyVirtualPut : BuyVirtualBase, IValuesHandlerWithNumber
    {
        public double Execute(ISecurity source, IOption option, ISecurity priceSec, double strikePrice, bool signal, int barNum)
        {
            var expDate = source.Bars.Last().Date.GetExpirationDate(Expiration);
            var series = option.GetSeries().First(s => s.ExpirationDate == expDate);
            series.TryGetStrikePair(strikePrice, out var strikePair);
            var strike = strikePair.Put;
            return MakeDeal(source, strike, priceSec, strikePrice, signal, barNum, false);
        }
    }

    [HandlerCategory("Dron.Options")]
    [HelperName("Buy Virtual Call", Language = Constants.En)]
    [InputsCount(5)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [Input(1, TemplateTypes.OPTION, Name = Constants.OptionSource)]
    [Input(2, TemplateTypes.SECURITY, Name = "TestPriceSource")]
    [Input(3, TemplateTypes.DOUBLE, Name = Constants.Strike)]
    [Input(4, TemplateTypes.BOOL, Name = "Signal")]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    public class BuyVirtualCall : BuyVirtualBase, IValuesHandlerWithNumber
    {
        public double Execute(ISecurity source, IOption option, ISecurity priceSec, double strikePrice, bool signal, int barNum)
        {
            var expDate = source.Bars.Last().Date.GetExpirationDate(Expiration);
            var series = option.GetSeries().First(s => s.ExpirationDate == expDate);
            series.TryGetStrikePair(strikePrice, out var strikePair);
            var strike = strikePair.Call;
            return MakeDeal(source, strike, priceSec, strikePrice, signal, barNum, true);
        }
    }


    [HandlerCategory("Dron.Options")]
    [HelperName("Test Virtual Options", Language = Constants.En)]
    [InputsCount(3)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [Input(1, TemplateTypes.OPTION, Name = Constants.OptionSource)]
    [Input(2, TemplateTypes.SECURITY, Name = "TestPrice")]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    public class TestOptionsExpiration : DronOptionBase, IValuesHandlerWithNumber
    {
        [HandlerParameter(true, "0.01", Min = "0.001", Max = "0.1", Step = "0.001", EditorMin = "0")]
        public double MinProfit { get; set; }

        public double Execute(ISecurity source, IOption option, ISecurity zeroPricesSec, int barNum)
        {
            if (barNum < 1)
                return 0;

            var strikeStep = option.GetSeries().First().GetStrikes().First().LotTick;

            var sigma = GetVolatility(source)[barNum];

            var isLast = barNum == source.Bars.Count - 1;
            var ct = source.Bars[barNum].Date;
            var price = zeroPricesSec.Bars[barNum].Close;
            var hasExpirations = false;

            foreach (var pos in source.Positions.GetActiveForBar(barNum).ToArray())
            {
                var name = pos.EntrySignalName;
                if (name.Length < 4) continue;

                var isCall = name.StartsWith("C");
                var expiration = ExpirationUtil.FromChar(name[1]);
                var expDate = pos.EntryBar.Date.GetExpirationDate(expiration);
                var isExp = ct >= expDate;
                hasExpirations |= isExp;
                var strikePrice = double.Parse(name.Substring(2));
                if (isExp || isLast)
                {
                    var closePrice = 0.0;
                    if (!isCall && price < strikePrice)
                    {
                        closePrice = (strikePrice - price) / price;
                    }
                    else if (isCall && price > strikePrice)
                    {
                        closePrice = (price - strikePrice) / price;
                    }

                    pos.VirtualChange(barNum + 1, closePrice, 0, $"Exp-{name}");
                }
                else
                {
                    var dT = ct.GetDt(expiration);
                    // it makes sell more low
                    var optPx = strikePrice.GetOptPx(strikeStep, isCall, price, dT, sigma * 0.9);
                    if (optPx >= pos.EntryPrice + MinProfit)
                    {
                        pos.VirtualChange(barNum + 1, optPx, 0, $"X-{name}");
                    }
                }
            }

            return hasExpirations ? 1 : 0;
        }
    }
}
