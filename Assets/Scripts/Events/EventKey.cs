using System;
using System.Collections.Generic;

public static class EventKey
{
	// Two-level cache: Type -> (value+suffixKey) -> composed string
	private static readonly Dictionary<Type, Dictionary<string, string>> cache = new Dictionary<Type, Dictionary<string, string>>();
	private static readonly object cacheLock = new object();

	public static string Compose(Enum baseEnum, string suffix = null)
	{
		if (baseEnum == null) throw new ArgumentNullException(nameof(baseEnum));

		Type t = baseEnum.GetType();
		// value as long to handle any underlying enum type
		long v = Convert.ToInt64(baseEnum);
		string innerKey = v.ToString();
		if (!string.IsNullOrEmpty(suffix)) innerKey = innerKey + ":" + suffix;

		lock (cacheLock)
		{
			if (!cache.TryGetValue(t, out var typeDict))
			{
				typeDict = new Dictionary<string, string>();
				cache[t] = typeDict;
			}

			if (!typeDict.TryGetValue(innerKey, out var composed))
			{
				// Build once and cache. Use type name + '.' + enum name for readability.
				string basePart = t.Name + "." + baseEnum.ToString();
				if (string.IsNullOrEmpty(suffix)) composed = basePart;
				else composed = basePart + "." + suffix;
				typeDict[innerKey] = composed;
			}

			return composed;
		}
	}
}
