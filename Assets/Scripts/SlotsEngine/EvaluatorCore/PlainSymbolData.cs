using System;

namespace EvaluatorCore
{
    public enum PayScaling { DepthSquared = 0, PerSymbol = 1 }
    public enum SymbolWinMode { LineMatch = 0, SingleOnReel = 1, TotalCount = 2 }

    public class PlainSymbolData
    {
        public string Name;
        public int BaseValue;
        public int MinWinDepth;
        public bool IsWild;
        public bool AllowWildMatch = true;
        public SymbolWinMode WinMode = SymbolWinMode.LineMatch;
        public int TotalCountTrigger = -1;
        public int MatchGroupId = -1;
        public PayScaling PayScaling = PayScaling.DepthSquared;

        public PlainSymbolData(string name, int baseValue = 0, int minWinDepth = -1, bool isWild = false, SymbolWinMode winMode = SymbolWinMode.LineMatch, PayScaling scaling = PayScaling.DepthSquared, int totalCountTrigger = -1, int matchGroupId = -1, bool allowWild = true)
        {
            Name = name; BaseValue = baseValue; MinWinDepth = minWinDepth; IsWild = isWild; WinMode = winMode; PayScaling = scaling; TotalCountTrigger = totalCountTrigger; MatchGroupId = matchGroupId; AllowWildMatch = allowWild;
        }

        public bool Matches(PlainSymbolData other)
        {
            if (other == null) return false;
            if (this.IsWild && other.IsWild) return true;
            if (this.MatchGroupId > 0 && other.MatchGroupId > 0 && this.MatchGroupId == other.MatchGroupId) return true;
            if (this.IsWild && other.AllowWildMatch) return true;
            if (other.IsWild && this.AllowWildMatch) return true;
            if (!string.IsNullOrEmpty(this.Name) && !string.IsNullOrEmpty(other.Name) && this.Name == other.Name) return true;
            return false;
        }
    }
}
