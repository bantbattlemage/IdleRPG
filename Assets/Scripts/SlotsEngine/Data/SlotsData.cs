using UnityEngine;

public class SlotsData : Data
{
	[SerializeField] private int index;
	public int Index => index;

	[SerializeField] private ReelData[] currentReelData;
	public ReelData[] CurrentCurrentReelData => currentReelData;
}
