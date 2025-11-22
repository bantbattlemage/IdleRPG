using System;
using UnityEngine;
#if ENABLE_ADDRESSABLES || UNITY_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

// Resolver that prefers Addressables, falls back to other lookup methods (no runtime reflection).
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

		// 3) fallback: search all loaded sprites by name
		var all = Resources.FindObjectsOfTypeAll<Sprite>();
		for (int i = 0; i < all.Length; i++)
		{
			if (all[i].name == key) return all[i];
		}

		return null;
	}
}
