using System.Collections.Generic;
using UnityEngine;

public static class DefinitionResolver
{
	private static Dictionary<string, ScriptableObject> cache = new Dictionary<string, ScriptableObject>();

	public static T Resolve<T>(string key) where T : ScriptableObject
	{
		if (string.IsNullOrEmpty(key)) return null;

		if (cache.TryGetValue(key, out var cached))
		{
			return cached as T;
		}

		// try Resources
		T res = Resources.Load<T>(key);
		if (res != null)
		{
			cache[key] = res;
			return res;
		}

		// fallback to loaded assets
		T[] all = Resources.FindObjectsOfTypeAll<T>();
		for (int i = 0; i < all.Length; i++)
		{
			if (all[i].name == key)
			{
				cache[key] = all[i];
				return all[i];
			}
		}

		return null;
	}

	public static void ClearCache()
	{
		cache.Clear();
	}
}
