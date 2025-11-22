using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class ReelStripData : Data
{
	[SerializeField] private int stripSize;
	[SerializeField] private string[] symbolDefinitionKeys;
	[SerializeField] private string definitionKey;

	[System.NonSerialized] private SymbolDefinition[] symbolDefinitions;
	[System.NonSerialized] private ReelStripDefinition definition;

	public int StripSize => stripSize;
	public SymbolDefinition[] SymbolDefinitions
	{
		get { EnsureResolved(); return symbolDefinitions; }
	}

	public ReelStripDefinition Definition { get { EnsureResolved(); return definition; } }

	public ReelStripData(ReelStripDefinition def, int size, SymbolDefinition[] syms)
	{
		stripSize = size;
		definition = def;
		symbolDefinitions = syms;
		definitionKey = def != null ? def.name : null;
		if (syms != null) { symbolDefinitionKeys = new string[syms.Length]; for (int i = 0; i < syms.Length; i++) symbolDefinitionKeys[i] = syms[i].name; }
	}

	private void EnsureResolved()
	{
		if (definition == null && !string.IsNullOrEmpty(definitionKey)) definition = DefinitionResolver.Resolve<ReelStripDefinition>(definitionKey);
		if ((symbolDefinitions == null || symbolDefinitions.Length == 0) && symbolDefinitionKeys != null) { symbolDefinitions = new SymbolDefinition[symbolDefinitionKeys.Length]; for (int i = 0; i < symbolDefinitionKeys.Length; i++) symbolDefinitions[i] = DefinitionResolver.Resolve<SymbolDefinition>(symbolDefinitionKeys[i]); }
	}

	/// <summary>
	/// Pick a SymbolDefinition from the configured pool, respecting optional counts in the definition
	/// and the already-selected symbols in existingSelections. If existingSelections is null, counts are ignored
	/// and a weight-only pick is performed.
	/// </summary>
	private SymbolDefinition PickFromPool(List<SymbolData> existingSelections)
	{
		EnsureResolved();
		SymbolDefinition[] pool = null;
		if (definition != null && definition.RandomPool != null && definition.RandomPool.Length > 0) pool = definition.RandomPool;
		else if (definition != null && definition.Symbols != null && definition.Symbols.Length > 0) pool = definition.Symbols;
		else pool = Resources.LoadAll<SymbolDefinition>("SlotsEngine/SymbolDefinitions");

		if (pool == null || pool.Length == 0) return null;

		int[] counts = definition != null ? definition.RandomPoolCounts : null;

		// Build candidates considering counts when existingSelections provided
		var candidates = new List<(SymbolDefinition def, float weight)>();
		for (int i = 0; i < pool.Length; i++)
		{
			var sd = pool[i];
			if (sd == null) continue;

			float w = sd.Weight; if (w < 0f) w = 0f;

			if (existingSelections != null)
			{
				// Count already-picked occurrences for this symbol (prefer MatchGroupId grouping when available)
				int alreadyForThis = 0;
				for (int j = 0; j < existingSelections.Count; j++)
				{
					var s = existingSelections[j];
					if (s == null) continue;
					if (sd.MatchGroupId != 0 && s.MatchGroupId == sd.MatchGroupId) alreadyForThis++; else if (sd.MatchGroupId == 0 && s.Name == sd.SymbolName) alreadyForThis++;
				}

				bool excluded = false;
				float effectiveWeight = w;

				// Apply definition.RandomPoolCounts depletion if provided
				if (counts != null && i < counts.Length && counts[i] >= 0)
				{
					int declared = counts[i];
					int remaining = declared - alreadyForThis;
					if (remaining <= 0)
					{
						excluded = true;
					}
					else
					{
						effectiveWeight = effectiveWeight * remaining;
					}
				}

				// Apply SymbolDefinition.MaxPerReel if set
				if (!excluded && sd.MaxPerReel >= 0)
				{
					int remainingMax = sd.MaxPerReel - alreadyForThis;
					if (remainingMax <= 0)
					{
						excluded = true;
					}
					else
					{
						effectiveWeight = effectiveWeight * remainingMax;
					}
				}

				if (!excluded && effectiveWeight > 0f)
				{
					candidates.Add((sd, effectiveWeight));
				}
				else if (!excluded && effectiveWeight <= 0f)
				{
					// if weight is zero, still consider as candidate with zero weight to allow fallback behavior
					candidates.Add((sd, 0f));
				}
			}
			else
			{
				// unlimited or no counts specified: weight-only
				candidates.Add((sd, w));
			}
		}

		if (candidates.Count == 0)
		{
			// All limited and exhausted: fall back to weight-only among pool
			var fallback = new List<(SymbolDefinition def, float weight)>();
			for (int i = 0; i < pool.Length; i++)
			{
				var sd = pool[i];
				if (sd == null) continue;
				float w = sd.Weight; if (w < 0f) w = 0f;
				fallback.Add((sd, w));
			}
			if (fallback.Count == 0) return null;
			float tot = fallback.Sum(e => e.weight);
			if (tot <= 0f) return fallback[RNGManager.Range(0, fallback.Count)].def;
			return WeightedRandom.Pick(fallback.ToArray());
		}

		float total = candidates.Sum(e => e.weight);
		if (total <= 0f) return candidates[RNGManager.Range(0, candidates.Count)].def;
		return WeightedRandom.Pick(candidates.ToArray());
	}

	/// <summary>
	/// Returns the SymbolDefinition for a given slot index in the strip. If the strip has a deterministic
	/// symbol at that slot (Definition.SlotSymbols) it is returned; otherwise pick from the configured random pool.
	/// existingSelections may be provided to allow depletion-aware picks from the pool.
	/// </summary>
	public SymbolDefinition GetSymbolDefinitionForSlot(int slotIndex, List<SymbolData> existingSelections = null)
	{
		EnsureResolved();
		if (definition == null) return null;

		var slotArr = definition.SlotSymbols;
		if (slotArr != null && slotIndex >= 0 && slotIndex < slotArr.Length)
		{
			var sdef = slotArr[slotIndex];
			if (sdef != null) return sdef; // deterministic slot
		}

		// placeholder: pick from random pool, consider existingSelections for depletion
		return PickFromPool(existingSelections);
	}

	/// <summary>
	/// Get a random symbol from this strip. For slot-based model this will choose a random slot index
	/// and return that slot's resolved symbol (either deterministic or random-picked placeholder).
	/// </summary>
	public SymbolData GetWeightedSymbol()
	{
		EnsureResolved();
		int size = StripSize > 0 ? StripSize : (definition != null && definition.SlotSymbols != null ? definition.SlotSymbols.Length : 0);
		if (size <= 0 && symbolDefinitions != null) size = symbolDefinitions.Length;
		if (size <= 0) return null;

		int slot = RNGManager.Range(0, size);
		var def = GetSymbolDefinitionForSlot(slot, null);
		if (def == null) return null;
		
		// CRITICAL: Ensure sprite is resolved BEFORE creating instance
		// This prevents the defensive sprite-fixup logic from running after creation
		// which can cause timing issues with sprite resolution
		Sprite resolvedSprite = def.SymbolSprite;
		if (resolvedSprite == null)
		{
			// Fallback: try to resolve by name if definition sprite is missing
			resolvedSprite = AssetResolver.ResolveSprite(def.SymbolName);
		}
		
		// Create instance with the pre-resolved sprite
		var sd = new SymbolData(
			def.SymbolName, 
			resolvedSprite, 
			def.BaseValue, 
			def.MinWinDepth, 
			def.Weight, 
			def.PayScaling, 
			def.IsWild, 
			def.AllowWildMatch, 
			def.WinMode, 
			def.TotalCountTrigger, 
			def.MaxPerReel, 
			def.MatchGroupId
		);

		// DEFENSIVE: Force the sprite getter to resolve NOW to populate the internal sprite field.
		// This ensures that any lazy resolution happens immediately, and subsequent reads of sd.Sprite
		// return the cached value rather than re-resolving, which prevents sprite flickering when
		// SymbolData is accessed from different contexts (ApplySymbol, presentation, etc.)
		var _ = sd.Sprite;

		return sd;
	}

	/// <summary>
	/// Multi-pick aware version used when generating multiple symbols for the same reel. For the slot model,
	/// deterministic slots remain deterministic; random placeholders pick from the pool and respect RandomPoolCounts
	/// by consulting existingSelections which should include prior picks for this reel.
	/// </summary>
	public SymbolData GetWeightedSymbol(System.Collections.Generic.List<SymbolData> existingSelections)
	{
		EnsureResolved();
		int size = StripSize > 0 ? StripSize : (definition != null && definition.SlotSymbols != null ? definition.SlotSymbols.Length : 0);
		if (size <= 0 && symbolDefinitions != null) size = symbolDefinitions.Length;
		if (size <= 0) return null;

		int slot = RNGManager.Range(0, size);
		var def = GetSymbolDefinitionForSlot(slot, existingSelections);
		if (def == null) return null;

		// CRITICAL: Ensure sprite is resolved BEFORE creating instance
		// This prevents the defensive sprite-fixup logic from running after creation
		// which can cause timing issues with sprite resolution
		Sprite resolvedSprite = def.SymbolSprite;
		if (resolvedSprite == null)
		{
			// Fallback: try to resolve by name if definition sprite is missing
			resolvedSprite = AssetResolver.ResolveSprite(def.SymbolName);
		}
		
		// Create instance with the pre-resolved sprite
		var sd = new SymbolData(
			def.SymbolName, 
			resolvedSprite, 
			def.BaseValue, 
			def.MinWinDepth, 
			def.Weight, 
			def.PayScaling, 
			def.IsWild, 
			def.AllowWildMatch, 
			def.WinMode, 
			def.TotalCountTrigger, 
			def.MaxPerReel, 
			def.MatchGroupId
		);

		// DEFENSIVE: Force the sprite getter to resolve NOW to populate the internal sprite field.
		// This ensures that any lazy resolution happens immediately, and subsequent reads of sd.Sprite
		// return the cached value rather than re-resolving, which prevents sprite flickering when
		// SymbolData is accessed from different contexts (ApplySymbol, presentation, etc.)
		var _ = sd.Sprite;

		return sd;
	}
}
