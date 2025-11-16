using System.Collections.Generic;
using System.Linq;

public static class Extensions
{
	public static T GetRandom<T>(this IEnumerable<T> list)
	{
		int randomIndex = UnityEngine.Random.Range(0, list.Count());
		return list.ElementAt(randomIndex);
	}

	public static SymbolDefinition[] ToSymbolDefinitions(this GameSymbol[] symbols)
	{
		List<SymbolDefinition> definitions = new List<SymbolDefinition>();

		foreach (GameSymbol s in symbols)
		{
			definitions.Add(s.Definition);
		}

		return definitions.ToArray();
	}
}
