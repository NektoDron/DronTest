using System;
using System.Collections.Generic;
using System.ComponentModel;
using TSLab.Script;
using TSLab.Script.Handlers;
using TSLab.Script.Handlers.Options;
using TSLab.Script.Helpers;

// ReSharper disable CommentTypo

namespace Dron.Indicators
{
    public class TrailStop : IContextUses, IPosition2Double
    {
        /// <summary>
        /// \~english Initial stop loss
        /// \~russian Начальный уровень стоплосса
        /// </summary>
        [HelperName("Stop loss", Constants.En)]
        [HelperName("Стоплосс", Constants.Ru)]
        [Description("Начальный уровень стоплосса")]
        [HelperDescription("Initial stop loss", Constants.En)]
        [HandlerParameter(true, "0.5", Min = "0", Max = "1", Step = "0.1", Name = "Stop Loss")]
        public double StopLoss { get; set; }

        /// <summary>
        /// \~english Where to start actual trailing
        /// \~russian На каком уровне начинать двигать стоп
        /// </summary>
        [HelperName("Trail enable", Constants.En)]
        [HelperName("Включение трейла", Constants.Ru)]
        [Description("На каком уровне начинать двигать стоп")]
        [HelperDescription("Where to start actual trailing", Constants.En)]
        [HandlerParameter(true, "0.1", Min = "0", Max = "0.5", Step = "0.1", Name = "Trail Enable")]
        public double TrailEnable { get; set; }

        /// <summary>
        /// \~english Trail loss
        /// \~russian Сколько должна пройти цена, чтобы стоп передвинулся
        /// </summary>
        [HelperName("Trail loss", Constants.En)]
        [HelperName("Подтягивать стоп", Constants.Ru)]
        [Description("Сколько должна пройти цена, чтобы стоп передвинулся")]
        [HelperDescription("Trail loss", Constants.En)]
        [HandlerParameter(true, "0.5", Min = "0.1", Max = "1", Step = "0.1", Name = "Trail Loss")]
        public double TrailLoss { get; set; }

        public IContext Context { get; set; }

        public virtual double Execute(IPosition pos, int barNum)
        {
            if (pos == null)
                return 0;

            var curProfit = pos.OpenMFE(barNum);
            var entryPrice = pos.GetBalancePrice(barNum);
            curProfit *= 100 / entryPrice * pos.Security.Margin;

            double stop;
            if (curProfit > TrailEnable)
            {
                var shift = (curProfit - TrailLoss) / 100;
                stop = entryPrice * (1 + (pos.IsLong ? shift : -shift));
                var last = pos.GetStop(barNum - 1);
                if (last > 0)
                    stop = pos.IsLong ? Math.Max(stop, last) : Math.Min(stop, last);
            }
            else
            {
                var shift = (0 - StopLoss) / 100;
                stop = entryPrice * (1 + (pos.IsLong ? shift : -shift));
            }
            return stop;
        }
    }

    [HandlerCategory("Dron.Indicators")]
    public class ATRTrailStop : TrailStop
    {
        private IList<double> m_atrs;

        [HandlerParameter(true, "3", Min = "1", Max = "10", Step = "0.5", Name = "Размер трейла(в ATR)")]
        public double ATRcoef { get; set; }

        [HandlerParameter(true, "10", Min = "2", Max = "10", Step = "1", Name = "Период расч. ATR")]
        public int ATRPeriod { get; set; }

        public override double Execute(IPosition pos, int barNum)
        {
            if (pos == null)
                return 0;

            if (m_atrs == null)
            {
                MakeAtrs(pos.Security);
            }

            var curProfit = pos.OpenMFE(barNum);
            var entryPrice = pos.GetBalancePrice(barNum);
            curProfit *= 100 / entryPrice * pos.Security.Margin;

            double stop;
            if (curProfit > TrailEnable)
            {
                var shift = (curProfit - TrailLoss + m_atrs[barNum] * ATRcoef) / 100;
                stop = entryPrice * (1 + (pos.IsLong ? shift : -shift));
                var last = pos.GetStop(barNum - 1);
                if (last > 0)
                    stop = pos.IsLong ? Math.Max(stop, last) : Math.Min(stop, last);
            }
            else
            {
                var shift = (0 - StopLoss) / 100;
                stop = entryPrice * (1 + (pos.IsLong ? shift : -shift));
            }
            return stop;
        }

        private void MakeAtrs(ISecurity security)
        {
            m_atrs = Context.GetData("ATRTrailStop$ATR",
                new[] { ATRPeriod.ToString(), security.Symbol, security.Interval.ToString() },
                () => Series.AverageTrueRange(security.Bars, ATRPeriod, Context));
        }
    }
}
