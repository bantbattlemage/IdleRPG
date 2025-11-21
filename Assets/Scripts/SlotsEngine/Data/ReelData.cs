using System;
using System.Collections.Generic;
using UnityEngine;

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
			for (int i = 0; i < symbolDatas.Count; i++)
			{
				currentSymbolKeys[i] = symbolDatas[i] != null ? symbolDatas[i].Name : null;
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

		if ((currentSymbolData == null || currentSymbolData.Count == 0) && currentSymbolKeys != null)
		{
			currentSymbolData = new List<SymbolData>();
			for (int i = 0; i < currentSymbolKeys.Length; i++)
			{
				// Resolve by name: this assumes symbol name is unique and used as key
				Sprite sprite = AssetResolver.ResolveSprite(currentSymbolKeys[i]);
				// Create a runtime SymbolData with default no-win values (BaseValue=0, MinWinDepth=-1)
				currentSymbolData.Add(new SymbolData(currentSymbolKeys[i], sprite, 0, -1, 1f, PayScaling.DepthSquared, false, true));
			}
		}
	}
}
