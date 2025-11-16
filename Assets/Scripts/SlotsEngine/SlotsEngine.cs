using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class SlotsEngine : Singleton<SlotsEngine>
{
	[SerializeField] private Canvas gameCanvas;
	[SerializeField] private int reelCount; 
	[SerializeField] private GameObject reelPrefab;
	[SerializeField] private ReelDefinition reelDefinition;

	private List<GameReel> reels = new List<GameReel>();

	private bool spinning = false;

	void Start()
	{
		SpawnReels();
	}

	void Update()
	{
		if (Input.GetKeyDown(KeyCode.Space))
		{
			SpinReel();
		}
	}

	private void SpinReel()
	{
		for (int i = 0; i < reels.Count; i++)
		{
			if (!spinning)
			{
				List<SymbolDefinition> testSolution = new List<SymbolDefinition>();
				for (int k = 0; k < reels[i].Definition.SymbolCount; k++)
				{
					testSolution.Add(SymbolSpawner.Instance.GetRandomSymbol());
				}

				reels[i].BeginSpin(testSolution);
			}
			else
			{
				reels[i].CompleteSpin();
			}
		}

		spinning = !spinning;
	}

	private void SpawnReels()
	{
		Transform reelsGroup = new GameObject("ReelsGroup").transform;
		reelsGroup.parent = gameCanvas.transform;
		reelsGroup.localScale = new Vector3(1, 1, 1);

		for (int i = 0; i < reelCount; i++)
		{
			GameObject r = Instantiate(reelPrefab, reelsGroup.transform);
			GameReel reel = r.GetComponent<GameReel>();
			reel.InitializeReel(reelDefinition);
			r.transform.localPosition = new Vector3((reelDefinition.ReelsSpacing + reelDefinition.SymbolSize) * i, 0, 0);
			reels.Add(reel);
		}

		reelsGroup.transform.localPosition = new Vector3(-((reelCount-1) * (reelDefinition.ReelsSpacing + reelDefinition.SymbolSize))/2f, -((reelDefinition.SymbolCount-1) * (reelDefinition.SymbolSpacing + reelDefinition.SymbolSize))/2f, 0);
	}
}
