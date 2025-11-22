using UnityEngine;

public class ReelDefinition : BaseDefinition<ReelData>
{
	[SerializeField] private float reelSpinDuration = 0.5f;
	[SerializeField] private int symbolCount;
	[SerializeField] private ReelStripDefinition defaultReelStrip;

	public float ReelSpinDuration => reelSpinDuration;
	public int SymbolCount => symbolCount;

	/// <summary>
	/// Creates a runtime `ReelData` with the configured spin duration, symbol count and default reel strip.
	/// </summary>
	public override ReelData CreateInstance()
	{
		return new ReelData(reelSpinDuration, symbolCount, defaultReelStrip, this);
	}
}
