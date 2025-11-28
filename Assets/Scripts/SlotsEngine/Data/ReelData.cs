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
	[SerializeField] private int[] currentSymbolAccessorIds;
	// NEW: persist accessor id for associated ReelStripData runtime instance
	[SerializeField] private int currentReelStripAccessorId;

	[System.NonSerialized] private ReelStripData currentReelStrip;
	[System.NonSerialized] private ReelDefinition baseDefinition;
	[System.NonSerialized] private List<SymbolData> currentSymbolData;

	public float ReelSpinDuration => reelSpinDuration;
	public int SymbolCount => symbolCount;
	public int SymbolSize => symbolSize;
	public int SymbolSpacing => symbolSpacing;
	public ReelStripData CurrentReelStrip { get { EnsureResolved(); return currentReelStrip; } }
	public ReelStripData DefaultReelStrip => CurrentReelStrip;
	public ReelDefinition BaseDefinition { get { EnsureResolved(); return baseDefinition; } }
	public List<SymbolData> CurrentSymbolData { get { EnsureResolved(); return currentSymbolData; } }

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
		else if (defaultStrip != null)
		{
			var rs = defaultStrip.CreateInstance();
			SetReelStrip(rs);
		}

		var strip = CurrentReelStrip;
		if (strip != null && strip.RuntimeSymbols != null && strip.RuntimeSymbols.Count > 0 && currentSymbolData.Count == 0)
		{
			for (int i = 0; i < Math.Min(symbolCount, strip.RuntimeSymbols.Count); i++)
			{
				var sym = strip.RuntimeSymbols[i];
				if (sym != null) RegisterSymbolForPersistence(sym);
				currentSymbolData.Add(sym);
			}
			SyncSymbolPersistenceArrays();
		}
	}

	public void SetReelStrip(ReelStripData reelStrip)
	{
		currentReelStrip = reelStrip;
		if (reelStrip != null)
		{
			defaultReelStripKey = reelStrip.Definition != null ? reelStrip.Definition.name : defaultReelStripKey;
			if (reelStrip.AccessorId == 0 && ReelStripDataManager.Instance != null)
			{
				ReelStripDataManager.Instance.AddNewData(reelStrip);
			}
			currentReelStripAccessorId = reelStrip.AccessorId;

			// Sync currentSymbolData from the strip's runtime symbols so the reel reflects runtime edits immediately
			if (reelStrip.RuntimeSymbols != null)
			{
				if (currentSymbolData == null) currentSymbolData = new List<SymbolData>();
				currentSymbolData.Clear();
				int countToCopy = Math.Min(symbolCount, reelStrip.RuntimeSymbols.Count);
				for (int i = 0; i < countToCopy; i++)
				{
					var s = reelStrip.RuntimeSymbols[i];
					if (s != null) RegisterSymbolForPersistence(s);
					currentSymbolData.Add(s);
				}
				// Ensure persistence arrays updated
				SyncSymbolPersistenceArrays();
			}
		}
	}

	public void SetCurrentSymbolData(List<SymbolData> symbolDatas)
	{
		currentSymbolData = symbolDatas;
		SyncSymbolPersistenceArrays();
	}

	public void SetSymbolSize(float size, float spacing)
	{
		symbolSize = (int)size;
		symbolSpacing = (int)spacing;
	}

	public void SetSymbolCount(int count)
	{
		if (count < 1) count = 1;
		symbolCount = count;
	}

	private void EnsureResolved()
	{
		if (baseDefinition == null && !string.IsNullOrEmpty(baseDefinitionKey))
		{
			baseDefinition = DefinitionResolver.Resolve<ReelDefinition>(baseDefinitionKey);
		}

		// Resolve existing runtime strip by accessor id first (preserves instance GUID)
		if (currentReelStrip == null && currentReelStripAccessorId > 0 && ReelStripDataManager.Instance != null)
		{
			if (ReelStripDataManager.Instance.TryGetData(currentReelStripAccessorId, out var rs))
			{
				currentReelStrip = rs;
			}
		}

		// Fallback: create from definition if no persisted runtime instance
		if (currentReelStrip == null && !string.IsNullOrEmpty(defaultReelStripKey))
		{
			var def = DefinitionResolver.Resolve<ReelStripDefinition>(defaultReelStripKey);
			if (def != null)
			{
				var rs = def.CreateInstance();
				SetReelStrip(rs);
			}
		}

		int len = 0;
		if (currentSymbolAccessorIds != null) len = Mathf.Max(len, currentSymbolAccessorIds.Length);
		if (currentSymbolKeys != null) len = Mathf.Max(len, currentSymbolKeys.Length);
		if ((currentSymbolData != null) && currentSymbolData.Count > len) len = currentSymbolData.Count;
		if (len == 0)
		{
			if (currentSymbolData == null) currentSymbolData = new List<SymbolData>();
			return;
		}
		if (currentSymbolKeys == null) currentSymbolKeys = new string[len];
		if (currentSymbolAccessorIds == null) currentSymbolAccessorIds = new int[len];

		var result = new List<SymbolData>(len);
		for (int i = 0; i < len; i++)
		{
			SymbolData resolved = null;
			if (currentSymbolAccessorIds != null && i < currentSymbolAccessorIds.Length)
			{
				int accessor = currentSymbolAccessorIds[i];
				if (accessor > 0 && SymbolDataManager.Instance != null && SymbolDataManager.Instance.TryGetData(accessor, out var fromManager))
				{
					resolved = fromManager;
					if (resolved != null && resolved.Sprite == null)
					{
						string nameKey = null;
						if (currentSymbolKeys != null && i < currentSymbolKeys.Length && !string.IsNullOrEmpty(currentSymbolKeys[i])) nameKey = currentSymbolKeys[i];
						if (string.IsNullOrEmpty(nameKey) && !string.IsNullOrEmpty(resolved.Name)) nameKey = resolved.Name;
						if (!string.IsNullOrEmpty(nameKey))
						{
							var resolvedSprite = AssetResolver.ResolveSprite(nameKey);
							if (resolvedSprite == null)
							{
								throw new InvalidOperationException($"ReelData.EnsureResolved: failed to resolve sprite for key '{nameKey}' while restoring persisted SymbolData (accessorId={resolved.AccessorId}, name='{resolved.Name}'). Persisted data must reference a valid sprite key.");
							}
							resolved.Sprite = resolvedSprite;
							if (!string.IsNullOrEmpty(nameKey)) currentSymbolKeys[i] = nameKey;
						}
						else
						{
							throw new InvalidOperationException($"ReelData.EnsureResolved: persisted SymbolData (accessorId={resolved.AccessorId}) has no Sprite and no key to resolve it. Data is invalid.");
						}
					}
				}
			}
			if (resolved == null && currentSymbolData != null && i < currentSymbolData.Count)
			{
				resolved = currentSymbolData[i];
			}
			if (resolved == null && currentSymbolKeys != null && i < currentSymbolKeys.Length)
			{
				string key = currentSymbolKeys[i];
				if (string.IsNullOrEmpty(key))
				{
					// Be tolerant of missing persisted keys introduced by runtime edits.
					// Prefer to fallback to any in-memory currentSymbolData entry or the associated runtime strip symbol at the same index.
					if (currentSymbolData != null && i < currentSymbolData.Count && currentSymbolData[i] != null)
					{
						resolved = currentSymbolData[i];
					}
					else if (currentReelStrip != null && currentReelStrip.RuntimeSymbols != null && i < currentReelStrip.RuntimeSymbols.Count && currentReelStrip.RuntimeSymbols[i] != null)
					{
						resolved = currentReelStrip.RuntimeSymbols[i];
						// Ensure persisted references updated when we can reconstruct from strip
						RegisterSymbolForPersistence(resolved);
						currentSymbolAccessorIds[i] = resolved.AccessorId;
						currentSymbolKeys[i] = resolved.SpriteKey ?? resolved.Name;
					}
					else
					{
						// No fallback available: allow a null entry rather than throwing so caller can handle missing data.
						resolved = null;
					}
				}
				else
				{
					// Try to resolve sprite and find a matching persisted SymbolData or an authoring definition.
					Sprite sprite = AssetResolver.ResolveSprite(key);
					if (sprite == null)
					{
						throw new InvalidOperationException($"ReelData.EnsureResolved: failed to resolve Sprite for key '{key}' while restoring ReelData. Persisted symbol keys must resolve to valid Sprite assets.");
					}

					// First preference: find an existing persisted SymbolData with the same SpriteKey
					SymbolData persisted = null;
					if (SymbolDataManager.Instance != null)
					{
						var all = SymbolDataManager.Instance.GetAllData();
						for (int j = 0; j < all.Count; j++) { var s = all[j]; if (s == null) continue; if (!string.IsNullOrEmpty(s.SpriteKey) && string.Equals(s.SpriteKey, key, StringComparison.OrdinalIgnoreCase)) { persisted = s; break; } }
					}

					if (persisted != null)
					{
						resolved = persisted;
						if (resolved != null && resolved.Sprite == null)
						{
							resolved.Sprite = sprite;
						}
						currentSymbolAccessorIds[i] = resolved.AccessorId;
						currentSymbolKeys[i] = key;
					}
					else
					{
						// Next, try to find a matching authoring-time SymbolDefinition
						SymbolDefinition def = null;
						if (SymbolDefinitionManager.Instance != null)
						{
							SymbolDefinitionManager.Instance.TryGetDefinition(key, out def);
						}

						if (def != null)
						{
							// Create runtime SymbolData from definition (enforces proper naming and match-group semantics)
							var runtimeSymbol = def.CreateInstance();
							if (runtimeSymbol.Sprite == null)
							{
								runtimeSymbol.Sprite = sprite;
							}
							resolved = runtimeSymbol;
							RegisterSymbolForPersistence(resolved);
							currentSymbolAccessorIds[i] = resolved.AccessorId;
							currentSymbolKeys[i] = key;
						}
						else
						{
							// No matching persisted SymbolData and no authoring definition for this sprite key: treat as an error.
							throw new InvalidOperationException($"ReelData.EnsureResolved: failed to resolve SymbolData for persisted key '{key}'. No matching SymbolDefinition or existing SymbolData found. Persisted data must reference a valid definition or SymbolData accessor.");
						}
					}
				}
			}
			result.Add(resolved);
		}
		currentSymbolData = result;

		// Adjust currentSymbolData list if runtime strip symbol count changed
		if (currentReelStrip != null && currentReelStrip.RuntimeSymbols != null)
		{
			int runtimeCount = currentReelStrip.RuntimeSymbols.Count;
			if (currentSymbolData != null)
			{
				if (currentSymbolData.Count > runtimeCount)
				{
					currentSymbolData.RemoveRange(runtimeCount, currentSymbolData.Count - runtimeCount);
					SyncSymbolPersistenceArrays();
				}
				else if (currentSymbolData.Count < runtimeCount && currentSymbolData.Count < symbolCount)
				{
					for (int i = currentSymbolData.Count; i < Math.Min(runtimeCount, symbolCount); i++)
					{
						var rs = currentReelStrip.RuntimeSymbols[i];
						currentSymbolData.Add(rs);
					}
					SyncSymbolPersistenceArrays();
				}
			}
		}
	}

	private void RegisterSymbolForPersistence(SymbolData symbol)
	{
		if (symbol == null) return;
		if (SymbolDataManager.Instance != null && symbol.AccessorId == 0)
		{
			SymbolDataManager.Instance.AddNewData(symbol);
		}
	}

	private void SyncSymbolPersistenceArrays()
	{
		if (currentSymbolData == null) return;
		int len = currentSymbolData.Count;
		currentSymbolKeys = new string[len];
		currentSymbolAccessorIds = new int[len];
		for (int i = 0; i < len; i++)
		{
			var sd = currentSymbolData[i];
			// Persist the sprite key (asset key) rather than the display name so we can reliably resolve sprites on load
			currentSymbolKeys[i] = sd != null ? sd.SpriteKey : null;
			currentSymbolAccessorIds[i] = sd != null ? sd.AccessorId : -1;
		}
	}
}
