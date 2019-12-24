using System;
using System.Linq;
using TSLab.Script;
using TSLab.Script.Handlers;

namespace TSLab.ML.Net
{
    [HandlerCategory("ML.Net")]
    public sealed class BarsFromLastChange : IPosition2Double
    {
        public double Execute(IPosition pos, int barNum)
        {
            if (pos == null || pos.EntryBarNum >= barNum)
            {
                return 0;
            }
            if (pos.IsActiveForBar(barNum))
            {
                return barNum - (pos.ChangeInfos.LastOrDefault(FindLastChange)?.EntryBarNum ?? pos.EntryBarNum);
            }
            return 0;
        }

        private static bool FindLastChange(IPositionInfo p)
        {
            return p.EntryBarNum > 0;
        }
    }
}

