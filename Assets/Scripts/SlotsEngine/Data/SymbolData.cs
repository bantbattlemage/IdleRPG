using System;
using UnityEngine;
using EvaluatorCore;

[Serializable]
public class SymbolData : Data
{
	[SerializeField] private string name;
	// Sprite must not be serialized into save data. Store a key instead and resolve at runtime.
	[NonSerialized] private Sprite sprite;
	[SerializeField] private string spriteKey;
	[SerializeField] private int baseValue = 0;
	[SerializeField] private int minWinDepth = -1;
	[SerializeField] private float weight = 1;
	[SerializeField] private PayScaling payScaling = PayScaling.DepthSquared;
	
	// Wild behavior
	[SerializeField] private bool isWild = false;
	[SerializeField] private bool allowWildMatch = true;
	
	// New: win mode and total count trigger
	[SerializeField] private SymbolWinMode winMode = SymbolWinMode.LineMatch;
	[SerializeField] private int totalCountTrigger = -1;
	
	// New: optional per-reel cap for this symbol during a single spin. -1 to ignore.
	[SerializeField] private int maxPerReel = -1;
	
	// New: integer match group identifier (set by definition). Use -1 to indicate unset/null.
	[SerializeField] private int matchGroupId = -1;
	
	[SerializeField] private ScriptableObject eventTriggerScript;
	[NonSerialized] public IEventTriggerScript RuntimeEventTrigger;

	public string Name => name;
	public int MatchGroupId => matchGroupId;
	public string SpriteKey => spriteKey; // new: expose stored asset key
	public Sprite Sprite
	{
		get
		{
			if (sprite == null && !string.IsNullOrEmpty(spriteKey))
			{
				sprite = AssetResolver.ResolveSprite(spriteKey);
			}
			return sprite;
		}
		set
		{
			sprite = value;
			spriteKey = value != null ? value.name : null;
		}
	}

	/// <summary>
	/// Explicitly set an asset key to use for resolving this symbol's sprite at runtime.
	/// This does not attempt to load the sprite immediately; use this when the key is known but
	/// you don't have a Sprite instance available (e.g., when creating from inventory).
	/// </summary>
	public void SetSpriteKey(string key)
	{
		spriteKey = key;
		sprite = null;
	}

	/// <summary>
	/// The minimum number of consecutive matching symbols required for this symbol to trigger a win.
	/// Returns -1 if this symbol cannot trigger wins.
	/// </summary>
	public int MinWinDepth => minWinDepth;
	public int BaseValue => baseValue;
	public PayScaling PayScaling => payScaling;
	public float Weight => weight;

	public bool IsWild => isWild;
	public bool AllowWildMatch => allowWildMatch;

	public SymbolWinMode WinMode => winMode;
	public int TotalCountTrigger => totalCountTrigger;

	public int MaxPerReel => maxPerReel;

	public ScriptableObject EventTriggerScript
	{
		get => eventTriggerScript;
		set
		{
			eventTriggerScript = value;
			RuntimeEventTrigger = eventTriggerScript as IEventTriggerScript;
		}
	}

	// Backwards-compatible constructor (previous signature)
	public SymbolData(string symbolName, Sprite symbolSprite, int baseVal, int minDepth, float symbolWeight, PayScaling scaling = PayScaling.DepthSquared, bool wild = false, bool allowWild = true)
		: this(symbolName, symbolSprite, baseVal, minDepth, symbolWeight, scaling, wild, allowWild, SymbolWinMode.LineMatch, -1, -1, -1)
	{
	}

	// New constructor using baseValue/minWinDepth/scaling and explicit mode/totalTrigger
	public SymbolData(string symbolName, Sprite symbolSprite, int baseVal, int minDepth, float symbolWeight, PayScaling scaling = PayScaling.DepthSquared, bool wild = false, bool allowWild = true, SymbolWinMode mode = SymbolWinMode.LineMatch, int totalTrigger = -1, int maxPerReelParam = -1, int matchGroup = -1)
	{
		// Enforce invariant: SymbolData must be instantiated with a valid Sprite instance so a SpriteKey can be derived.
		if (symbolSprite == null)
		{
			throw new InvalidOperationException($"SymbolData: attempted to construct symbol '{symbolName ?? "<unnamed>"}' without a Sprite. Symbols must be created with a valid Sprite so a SpriteKey can be persisted.");
		}

		name = symbolName;
		sprite = symbolSprite;
		spriteKey = symbolSprite != null ? symbolSprite.name : null;
		baseValue = baseVal;
		minWinDepth = minDepth;
		weight = symbolWeight;
		payScaling = scaling;
		isWild = wild;
		allowWildMatch = allowWild;
		winMode = mode;
		totalCountTrigger = totalTrigger;
		maxPerReel = maxPerReelParam;
		matchGroupId = matchGroup;
		RuntimeEventTrigger = eventTriggerScript as IEventTriggerScript;
	}

	/// <summary>
	/// Determines if this symbol should be considered a match with <paramref name="other"/>.
	/// Matching rules:
	/// - Group id equality (positive id) indicates match
	/// - Wild symbol rules remain: two wilds match; wild matches non-wild when allowed
	/// </summary>
	public bool Matches(SymbolData other)
	{
		if (other == null) return false;

		// Both wild -> match
		if (this.IsWild && other.IsWild) return true;

		// Group match: if both have a positive MatchGroupId and they are equal
		if (this.MatchGroupId > 0 && other.MatchGroupId > 0 && this.MatchGroupId == other.MatchGroupId) return true;

		// Wild substitution rules
		if (this.IsWild && other.AllowWildMatch) return true;
		if (other.IsWild && this.AllowWildMatch) return true;

		// Fallback: direct name equality (handles symbols without explicit match group)
		if (!string.IsNullOrEmpty(this.Name) && !string.IsNullOrEmpty(other.Name) && this.Name == other.Name) return true;

		return false;
	}
}
