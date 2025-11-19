using System.Collections.Generic;
using System.Linq;

public static class Extensions
{
	public static T GetRandom<T>(this IEnumerable<T> list)
	{
		int randomIndex = UnityEngine.Random.Range(0, list.Count());
		return list.ElementAt(randomIndex);
	}

	public static SymbolData[] ToSymbolDatas(this GameSymbol[] symbols)
	{
		List<SymbolData> symbolData = new List<SymbolData>();

		foreach (GameSymbol s in symbols)
		{
			if(s == null) continue;

			symbolData.Add(s.CurrentSymbolData);
		}

		return symbolData.ToArray();
	}
}
