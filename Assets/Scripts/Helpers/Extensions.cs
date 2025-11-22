using System.Collections.Generic;
using System.Linq;

public static class Extensions
{
	public static T GetRandom<T>(this IList<T> list)
	{
		if (list == null || list.Count == 0) return default;
		int randomIndex = RNGManager.Range(0, list.Count);
		return list[randomIndex];
	}

	public static T GetRandom<T>(this IEnumerable<T> list)
	{
		if (list == null) return default;
		// Fast path for IList<T>
		if (list is IList<T> il) return il.GetRandom();

		// Fallback: materialize once to avoid multiple enumerations
		T[] arr = list as T[] ?? list.ToArray();
		if (arr.Length == 0) return default;
		int randomIndex = RNGManager.Range(0, arr.Length);
		return arr[randomIndex];
	}

	public static SymbolData[] ToSymbolDatas(this GameSymbol[] symbols)
	{
		if (symbols == null) return System.Array.Empty<SymbolData>();

		SymbolData[] result = new SymbolData[symbols.Length];
		for (int i = 0; i < symbols.Length; i++)
		{
			var s = symbols[i];
			result[i] = s != null ? s.CurrentSymbolData : null;
		}

		return result;
	}
}
