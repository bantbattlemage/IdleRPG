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
	[SerializeField] private ReelStripDefinition defaultReelStrip;
	[SerializeField] private ReelStripData currentReelStrip;
	[SerializeField] private ReelDefinition baseDefinition;
	[SerializeField] private List<SymbolData> currentSymbolData;

	public float ReelSpinDuration => reelSpinDuration;
	public int SymbolCount => symbolCount;
	public int SymbolSize => symbolSize;
	public int SymbolSpacing => symbolSpacing;
	public ReelStripDefinition DefaultReelStrip => defaultReelStrip;
	public ReelDefinition BaseDefinition => baseDefinition;
	public List<SymbolData> CurrentSymbolData => currentSymbolData;
	public ReelStripData CurrentReelStrip => currentReelStrip;

	public ReelData(float duration, int count, int size, int spacing, ReelStripDefinition defaultStrip, ReelDefinition def, ReelStripData existingStripData = null)
	{
		reelSpinDuration = duration;
		symbolCount = count;
		symbolSize = size;
		symbolSpacing = spacing;
		defaultReelStrip = defaultStrip;
		baseDefinition = def;
		currentSymbolData = new List<SymbolData>();

		if (existingStripData != null)
		{
			SetReelStrip(existingStripData);
		}
		else
		{
			SetReelStrip(defaultReelStrip.CreateInstance());
		}
	}

	public void SetReelStrip(ReelStripData reelStrip)
	{
		currentReelStrip = reelStrip;
	}

	public void SetCurrentSymbolData(List<SymbolData> symbolDatas)
	{
		currentSymbolData = symbolDatas;
	}

	public void SetSymbolSize(float size, float spacing)
	{
		symbolSize = (int)size;
		symbolSpacing = (int)spacing;
	}
}
