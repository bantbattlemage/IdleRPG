using UnityEngine;

public class ReelData : Data
{
	private float reelSpinDuration = 0.5f;
	private int symbolCount;
	private int symbolSize = 170;
	private int symbolSpacing = 15;
	private ReelStripDefinition defaultReelStrip;
	private ReelDefinition baseDefinition;

	public float ReelSpinDuration => reelSpinDuration;
	public int SymbolCount => symbolCount;
	public int SymbolSize => symbolSize;
	public int SymbolSpacing => symbolSpacing;
	public ReelStripDefinition DefaultReelStrip => defaultReelStrip;
	public ReelDefinition BaseDefinition => baseDefinition;

	public ReelData(float duration, int count, int size, int spacing, ReelStripDefinition defaultStrip, ReelDefinition def)
	{
		reelSpinDuration = duration;
		symbolCount = count;
		symbolSize = size;
		symbolSpacing = spacing;
		defaultReelStrip = defaultStrip;
		baseDefinition = def;
	}
}
