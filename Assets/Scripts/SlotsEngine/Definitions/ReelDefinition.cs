using UnityEngine;

public class ReelDefinition : BaseDefinition<ReelData>
{
	[SerializeField] private float reelSpinDuration = 0.5f;
	[SerializeField] private int symbolCount;
	[SerializeField] private ReelStripDefinition defaultReelStrip;

	public float ReelSpinDuration => reelSpinDuration;
	public int SymbolCount => symbolCount;

	public override ReelData CreateInstance()
	{
		return new ReelData(reelSpinDuration, symbolCount, defaultReelStrip, this);
	}
}
