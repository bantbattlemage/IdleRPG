using System;
using UnityEngine;

[Serializable]
public class SymbolData : Data
{
	[SerializeField] private string name;
	// Sprite must not be serialized into save data. Store a key instead and resolve at runtime.
	[NonSerialized] private Sprite sprite;
	[SerializeField] private string spriteKey;
	[SerializeField] private int[] baseValueMultiplier;
	[SerializeField] private float weight = 1;

	// Wild behavior
	[SerializeField] private bool isWild = false;
	[SerializeField] private bool allowWildMatch = true;

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
	/// Returns -1 if this symbol cannot trigger wins (all multipliers are 0).
	/// Example: if baseValueMultiplier = [0, 0, 5, 10, 20], MinWinDepth returns 3 (need 3+ matches).
	/// </summary>
	public int MinWinDepth
	{
		get
		{
			for (int i = 0; i < (baseValueMultiplier != null ? baseValueMultiplier.Length : 0); i++)
			{
				if (baseValueMultiplier[i] > 0) return i + 1; // Return count (1-based), not index
			}

			return -1;
		}
	}
	public int[] BaseValueMultiplier => baseValueMultiplier;
	public float Weight => weight;

	public bool IsWild => isWild;
	public bool AllowWildMatch => allowWildMatch;

	public SymbolData(string symbolName, Sprite symbolSprite, int[] values, float symbolWeight)
	{
		name = symbolName;
		sprite = symbolSprite;
		spriteKey = symbolSprite != null ? symbolSprite.name : null;
		baseValueMultiplier = values;
		weight = symbolWeight;
	}

	public SymbolData(string symbolName, Sprite symbolSprite, int[] values, float symbolWeight, bool wild, bool allowWild)
	{
		name = symbolName;
		sprite = symbolSprite;
		spriteKey = symbolSprite != null ? symbolSprite.name : null;
		baseValueMultiplier = values;
		weight = symbolWeight;
		isWild = wild;
		allowWildMatch = allowWild;
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
