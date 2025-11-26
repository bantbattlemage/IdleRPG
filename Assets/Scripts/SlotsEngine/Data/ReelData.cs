using System;
using System.Collections.Generic;
using UnityEngine;
using EvaluatorCore;

[Serializable]
public class ReelData : Data
{
	[SerializeField] private float reelSpinDuration = 0.5f;
	[SerializeField] private int symbolCount;
	[SerializeField] private int symbolSize = 170;
	[SerializeField] private int symbolSpacing = 15;
	[SerializeField] private string defaultReelStripKey;
	[SerializeField] private string baseDefinitionKey;
	[SerializeField] private string[] currentSymbolKeys;
	[SerializeField] private int[] currentSymbolAccessorIds; // NEW: persistent accessor id references for symbols

	[System.NonSerialized] private ReelStripData currentReelStrip;
	[System.NonSerialized] private ReelDefinition baseDefinition;
	[System.NonSerialized] private List<SymbolData> currentSymbolData;

	public float ReelSpinDuration => reelSpinDuration;
	public int SymbolCount => symbolCount;
	public int SymbolSize => symbolSize;
	public int SymbolSpacing => symbolSpacing;
	public ReelStripData CurrentReelStrip
	{
		get { EnsureResolved(); return currentReelStrip; }
	}

	// Compatibility: previous code referenced DefaultReelStrip. Return the resolved current strip.
	public ReelStripData DefaultReelStrip => CurrentReelStrip;

	public ReelDefinition BaseDefinition
	{
		get { EnsureResolved(); return baseDefinition; }
	}
	public List<SymbolData> CurrentSymbolData
	{
		get { EnsureResolved(); return currentSymbolData; }
	}

	public ReelData(float duration, int count, ReelStripDefinition defaultStrip, ReelDefinition def, ReelStripData existingStripData = null)
	{
		reelSpinDuration = duration;
		symbolCount = count;
		baseDefinition = def;
		baseDefinitionKey = def != null ? def.name : null;
		currentSymbolData = new List<SymbolData>();

		if (existingStripData != null)
		{
			SetReelStrip(existingStripData);
		}
		else
		{
			SetReelStrip(defaultStrip.CreateInstance());
		}
	}

	public void SetReelStrip(ReelStripData reelStrip)
	{
		currentReelStrip = reelStrip;
		if (reelStrip != null)
		{
			defaultReelStripKey = reelStrip.Definition != null ? reelStrip.Definition.name : null;
		}
	}

	public void SetCurrentSymbolData(List<SymbolData> symbolDatas)
	{
		currentSymbolData = symbolDatas;
		if (symbolDatas != null)
		{
			currentSymbolKeys = new string[symbolDatas.Count];
			currentSymbolAccessorIds = new int[symbolDatas.Count];
			for (int i = 0; i < symbolDatas.Count; i++)
			{
				var sd = symbolDatas[i];
				currentSymbolKeys[i] = sd != null ? sd.Name : null;
				// Persist accessor id when available; use -1 for missing
				currentSymbolAccessorIds[i] = sd != null ? sd.AccessorId : -1;
			}
		}
	}

	public void SetSymbolSize(float size, float spacing)
	{
		symbolSize = (int)size;
		symbolSpacing = (int)spacing;
	}

	// Allow changing the number of symbols (rows) on a reel at runtime.
	public void SetSymbolCount(int count)
	{
		if (count < 1) count = 1;
		symbolCount = count;
		// Note: visual update is handled by GameReel which owns the spawned GameObjects.
	}

	private void EnsureResolved()
	{
		if (baseDefinition == null && !string.IsNullOrEmpty(baseDefinitionKey))
		{
			baseDefinition = DefinitionResolver.Resolve<ReelDefinition>(baseDefinitionKey);
		}

		if (currentReelStrip == null && !string.IsNullOrEmpty(defaultReelStripKey))
		{
			currentReelStrip = DefinitionResolver.Resolve<ReelStripDefinition>(defaultReelStripKey)?.CreateInstance();
		}

		// Build a target length for symbol lists using available persisted arrays
		int len = 0;
		if (currentSymbolAccessorIds != null) len = Mathf.Max(len, currentSymbolAccessorIds.Length);
		if (currentSymbolKeys != null) len = Mathf.Max(len, currentSymbolKeys.Length);
		if ((currentSymbolData != null) && currentSymbolData.Count > len) len = currentSymbolData.Count;

		// If there is nothing to resolve, keep current state
		if (len == 0)
		{
			return;
		}

		// Ensure backing arrays exist for persistence consistency
		if (currentSymbolKeys == null) currentSymbolKeys = new string[len];
		if (currentSymbolAccessorIds == null) currentSymbolAccessorIds = new int[len];

		var result = new List<SymbolData>(len);
		for (int i = 0; i < len; i++)
		{
			SymbolData resolved = null;

			// 1) Try resolve by persisted accessor id if present and positive
			if (currentSymbolAccessorIds != null && i < currentSymbolAccessorIds.Length)
			{
				int accessor = currentSymbolAccessorIds[i];
				if (accessor > 0)
				{
					if (SymbolDataManager.Instance != null && SymbolDataManager.Instance.TryGetData(accessor, out var fromManager))
					{
						resolved = fromManager;
						// if manager-provided symbol lacks a sprite/key, try to resolve using persisted key or name
						if (resolved != null && resolved.Sprite == null)
						{
							string nameKey = null;
							if (currentSymbolKeys != null && i < currentSymbolKeys.Length && !string.IsNullOrEmpty(currentSymbolKeys[i])) nameKey = currentSymbolKeys[i];
							if (string.IsNullOrEmpty(nameKey) && !string.IsNullOrEmpty(resolved.Name)) nameKey = resolved.Name;
							if (!string.IsNullOrEmpty(nameKey))
							{
								resolved.Sprite = AssetResolver.ResolveSprite(nameKey);
								// ensure persisted key is populated so future resolution doesn't rely solely on accessor
								if (!string.IsNullOrEmpty(nameKey))
								{
									if (currentSymbolKeys == null || i >= currentSymbolKeys.Length)
									{
										var newKeys = new string[len];
										if (currentSymbolKeys != null) Array.Copy(currentSymbolKeys, newKeys, Math.Min(currentSymbolKeys.Length, newKeys.Length));
										currentSymbolKeys = newKeys;
									}
									currentSymbolKeys[i] = nameKey;
								}
							}
						}
					}
				}
			}

			// 2) If still unresolved, check if current in-memory data exists and reuse it
			if (resolved == null && currentSymbolData != null && i < currentSymbolData.Count)
			{
				resolved = currentSymbolData[i];
			}

			// 3) Fallback: resolve by key/name and create a runtime SymbolData
			if (resolved == null && currentSymbolKeys != null && i < currentSymbolKeys.Length)
			{
				string key = currentSymbolKeys[i];
				Sprite sprite = AssetResolver.ResolveSprite(key);
				resolved = new SymbolData(key, sprite, 0, -1, 1f, PayScaling.DepthSquared, false, true);

				// If we can persist this runtime-created symbol into the SymbolDataManager, do so so accessor ids are available later
				if (SymbolDataManager.Instance != null && resolved != null && resolved.AccessorId == 0)
				{
					SymbolDataManager.Instance.AddNewData(resolved);
					// record assigned accessor id for future resolution
					if (currentSymbolAccessorIds == null || i >= currentSymbolAccessorIds.Length)
					{
						var newArr = new int[len];
						if (currentSymbolAccessorIds != null) Array.Copy(currentSymbolAccessorIds, newArr, Math.Min(currentSymbolAccessorIds.Length, newArr.Length));
						currentSymbolAccessorIds = newArr;
					}
					currentSymbolAccessorIds[i] = resolved.AccessorId;
				}
			}

			result.Add(resolved);
		}

		currentSymbolData = result;
	}
}
