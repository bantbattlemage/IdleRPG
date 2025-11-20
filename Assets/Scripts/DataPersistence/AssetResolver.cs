using System;
using UnityEngine;
#if ENABLE_ADDRESSABLES || UNITY_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

// Resolver that prefers Addressables, falls back to other lookup methods.
public static class AssetResolver
{
	public static Sprite ResolveSprite(string key)
	{
		if (string.IsNullOrEmpty(key)) return null;

		// 1) Addressables (synchronous wait) if available
#if ENABLE_ADDRESSABLES || UNITY_ADDRESSABLES
		try
		{
			AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(key);
			Sprite result = handle.WaitForCompletion();
			if (result != null)
			{
				try { Addressables.Release(handle); } catch { }
				return result;
			}
		}
		catch (Exception) { /* ignore addressable failures and fallback */ }
#endif

		// 2) Resources
		Sprite res = Resources.Load<Sprite>(key);
		if (res != null) return res;

		// 3) Try to find matching SymbolDefinition ScriptableObject and return its sprite (reflection-safe)
		try
		{
			var scriptableObjects = Resources.FindObjectsOfTypeAll<ScriptableObject>();
			for (int i = 0; i < scriptableObjects.Length; i++)
			{
				var obj = scriptableObjects[i];
				var t = obj.GetType();
				if (t.Name == "SymbolDefinition")
				{
					var nameProp = t.GetProperty("SymbolName");
					if (nameProp != null)
					{
						var val = nameProp.GetValue(obj) as string;
						if (val == key)
						{
							var spriteProp = t.GetProperty("SymbolSprite");
							if (spriteProp != null)
							{
								var sprite = spriteProp.GetValue(obj) as Sprite;
								if (sprite != null) return sprite;
							}
						}
					}
				}
			}
		}
		catch (Exception) { /* ignore reflection failures */ }

		// 4) fallback: search all loaded sprites
		var all = Resources.FindObjectsOfTypeAll<Sprite>();
		for (int i = 0; i < all.Length; i++)
		{
			if (all[i].name == key) return all[i];
		}

		return null;
	}
}
