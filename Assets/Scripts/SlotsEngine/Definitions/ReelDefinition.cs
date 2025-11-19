using UnityEngine;

public class ReelDefinition : BaseDefinition<ReelData>
{
	[SerializeField] private float reelSpinDuration = 0.5f;
	[SerializeField] private int symbolCount;
	[SerializeField] private int symbolSize = 170;
	[SerializeField] private int symbolSpacing = 15;
	[SerializeField] private ReelStripDefinition defaultReelStrip;

	public float ReelSpinDuration => reelSpinDuration;
	public int SymbolCount => symbolCount;
	public int SymbolSize => symbolSize;
	public int SymbolSpacing => symbolSpacing;

	public override ReelData CreateInstance()
	{
		return new ReelData(reelSpinDuration, symbolCount, symbolSize, symbolSpacing, defaultReelStrip, this);
	}
}
