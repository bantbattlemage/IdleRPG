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
	public int MinWinDepth
	{
		get
		{
			for (int i = 0; i < baseValueMultiplier.Length; i++)
			{
				if (baseValueMultiplier[i] > 0) return i;
			}

			return -1;
		}
	}
	public int[] BaseValueMultiplier => baseValueMultiplier;
	public float Weight => weight;

	public SymbolData(string symbolName, Sprite symbolSprite, int[] values, float symbolWeight)
	{
		name = symbolName;
		sprite = symbolSprite;
		spriteKey = symbolSprite != null ? symbolSprite.name : null;
		baseValueMultiplier = values;
		weight = symbolWeight;
	}
}
