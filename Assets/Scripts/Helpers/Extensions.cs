using System.Collections.Generic;
using System.Linq;

public static class Extensions
{
	public static T GetRandom<T>(this IEnumerable<T> list)
	{
		int count = list.Count();
		if (count == 0) return default;
		int randomIndex = RNGManager.Range(0, count);
		return list.ElementAt(randomIndex);
	}

	public static SymbolData[] ToSymbolDatas(this GameSymbol[] symbols)
	{
		if (symbols == null) return new SymbolData[0];

		SymbolData[] result = new SymbolData[symbols.Length];
		for (int i = 0; i < symbols.Length; i++)
		{
			var s = symbols[i];
			result[i] = s != null ? s.CurrentSymbolData : null;
		}

		return result;
	}
}
