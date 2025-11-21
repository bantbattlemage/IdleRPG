using System;
using UnityEngine;

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

	public string Name => name;
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

	// Backwards-compatible constructor (previous signature)
	public SymbolData(string symbolName, Sprite symbolSprite, int baseVal, int minDepth, float symbolWeight, PayScaling scaling = PayScaling.DepthSquared, bool wild = false, bool allowWild = true)
		: this(symbolName, symbolSprite, baseVal, minDepth, symbolWeight, scaling, wild, allowWild, SymbolWinMode.LineMatch, -1)
	{
	}

	// New constructor using baseValue/minWinDepth/scaling and explicit mode/totalTrigger
	public SymbolData(string symbolName, Sprite symbolSprite, int baseVal, int minDepth, float symbolWeight, PayScaling scaling = PayScaling.DepthSquared, bool wild = false, bool allowWild = true, SymbolWinMode mode = SymbolWinMode.LineMatch, int totalTrigger = -1)
	{
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
	}

	/// <summary>
	/// Determines if this symbol should be considered a match with <paramref name="other"/>.
	/// Matching rules:
	/// - Exact name equality matches
	/// - A wild symbol matches another symbol if the other symbol allows being matched by wilds
	/// - A non-wild symbol matches a wild symbol if this symbol allows being matched by wilds
	/// - Two wilds always match
	/// This method centralizes matching rules so different Symbol implementations can alter behavior via properties.
	/// </summary>
	public bool Matches(SymbolData other)
	{
		if (other == null) return false;

		// Exact name match
		if (!string.IsNullOrEmpty(this.Name) && this.Name == other.Name) return true;

		// Both wild -> match
		if (this.IsWild && other.IsWild) return true;

		// This is wild and other allows being matched by wilds
		if (this.IsWild && other.AllowWildMatch) return true;

		// Other is wild and this allows being matched by wilds
		if (other.IsWild && this.AllowWildMatch) return true;

		return false;
	}
}
