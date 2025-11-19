using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SlotsDefinition : BaseDefinition<SlotsData>
{
	[SerializeField] private ReelDefinition[] reelDefinitions;
	public ReelDefinition[] ReelDefinitions => reelDefinitions;

	[SerializeField] private WinlineDefinition[] winlineDefinitions;
	public WinlineDefinition[] WinlineDefinitions => winlineDefinitions;

	[SerializeField] private BetLevelDefinition[] betLevelDefinitions;
	public BetLevelDefinition[] BetLevelDefinitions => betLevelDefinitions;

	public override SlotsData CreateInstance()
	{
		return new SlotsData(0, winlineDefinitions.ToList(), betLevelDefinitions.ToList());
	}
}
