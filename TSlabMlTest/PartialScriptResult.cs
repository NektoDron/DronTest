using System;
using System.Collections.Generic;
using TSLab.Script;
using TSLab.Script.Handlers;
using TSLab.ScriptEngine;

namespace TSLab.ML.Net
{
    [HandlerCategory("ML.Net")]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY)]
    [OutputsCount(0)]
    public class PartialScriptResult : IOneSourceHandler, IValuesHandler, ISecurityInputs, IDoubleReturns, IContextUses
    {
        [HandlerParameter(true, "0.8", Min = "0.5", Max = "1", Step = "0.1", EditorMin = "0")]
        public double TestCoefficient { get; set; } = 0.8;

        public double Execute(ISecurity sec, int barNum)
        {
            var testBar = (int)(sec.Bars.Count * TestCoefficient);
            if (!Context.IsOptimization || barNum != sec.Bars.Count - 1)
                return 0;
            var perf = new Perfomance(new[] { (ISecurity2)sec }, sec.InitDeposit);
            try
            {
                var positions = new List<IPosition>();
                foreach (var position in sec.Positions)
                {
                    if (position.EntryBarNum < testBar)
                        continue;
                    perf.AddPosition(position);
                    positions.Add(position);
                }

                var value = perf.RecoveryFactor;
                Context.ScriptResult = value;
                positions.ForEach(p => ((PositionsList)sec.Positions).Remove(p));
                return value;
            }
            finally
            {
                perf.RemoveTempData();
            }
        }

        public IContext Context { get; set; }
    }
}
