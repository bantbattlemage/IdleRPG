using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;

public class SlotsEngine : Singleton<SlotsEngine>
{
	[SerializeField] private Canvas gameCanvas;
	[SerializeField] private GameObject reelPrefab;
	[SerializeField] private SlotsDefinition slotsDefinition;
	public SlotsDefinition SlotsDefinition => slotsDefinition;

	private List<GameReel> reels = new List<GameReel>();

	private bool spinInProgress = false;

	public void InitializeSlotsEngine()
	{
		EventManager.Instance.RegisterEvent("SpinCompleted", OnSpinCompleted);
		EventManager.Instance.RegisterEvent("ReelSpinStarted", OnReelSpinStarted);
		EventManager.Instance.RegisterEvent("ReelCompleted", OnReelCompleted);
		EventManager.Instance.RegisterEvent("PresentationEnter", OnPresentationEnter);

		SpawnReels();
	}

	public void SpinOrStopReels(bool spin)
	{
		if (!spinInProgress && spin)
		{
			SpinAllReels();
		}
		else if (spinInProgress && !spin && StateMachine.Instance.CurrentState == State.Spinning)
		{
			StopAllReels();
		}
		else
		{
			Debug.Log("SpinOrStopReels called but no action could be taken.");

			if (GamePlayer.Instance.CurrentCredits < GamePlayer.Instance.CurrentBet.CreditCost)
			{
				SlotConsoleController.Instance.SetConsoleMessage("Not enough credits! Switch to a lower bet or add credits.");
			}
		}
	}

	private void SpinAllReels()
	{
		if (spinInProgress)
		{
			throw new Exception("spin already in progress!");
		}

		float falloutDelay = 0.025f;

		for (int i = 0; i < reels.Count; i++)
		{
			List<SymbolDefinition> testSolution = new List<SymbolDefinition>();
			for (int k = 0; k < reels[i].Definition.SymbolCount; k++)
			{
				testSolution.Add(SymbolSpawner.Instance.GetRandomSymbol());
			}

			reels[i].BeginSpin(testSolution, falloutDelay);
			falloutDelay += 0.025f;
		}
	}

	private void OnReelSpinStarted(object obj)
	{
		if (reels.TrueForAll(x => x.Spinning))
		{
			spinInProgress = true;
			StateMachine.Instance.SetState(State.Spinning);
		}
	}

	private void StopAllReels()
	{
		//	only call stop if all reels are spinning
		if (!reels.TrueForAll(x => x.Spinning))
		{
			return;
		}

		float stagger = 0.025f;
		for (int i = 0; i < reels.Count; i++)
		{
			reels[i].StopReel(stagger);
			stagger += 0.025f;
		}

		EventManager.Instance.BroadcastEvent("StoppingReels");
	}

	private void SpawnReels()
	{
		Transform reelsGroup = new GameObject("ReelsGroup").transform;
		reelsGroup.parent = gameCanvas.transform;
		reelsGroup.localScale = new Vector3(1, 1, 1);

		for (int i = 0; i < slotsDefinition.ReelDefinitions.Length; i++)
		{
			ReelDefinition reelDef = slotsDefinition.ReelDefinitions[i];
			GameObject g = Instantiate(reelPrefab, reelsGroup.transform);
			GameReel reel = g.GetComponent<GameReel>();
			reel.InitializeReel(reelDef, i);
			g.transform.localPosition = new Vector3((reelDef.SymbolSpacing + reelDef.SymbolSize) * i, 0, 0);
			reels.Add(reel);
		}

		ReelDefinition reelDefinition = slotsDefinition.ReelDefinitions[0];

		int count = reels.Count;
		float totalWidth = (count * (reelDefinition.SymbolSize)) + ((count - 1) * reelDefinition.SymbolSpacing);
		float offset = totalWidth / 2f;
		float xPos = (-offset + (reelDefinition.SymbolSize / 2f));

		count = slotsDefinition.ReelDefinitions.Max(x => x.SymbolCount);
		totalWidth = (count * (reelDefinition.SymbolSize)) + ((count - 1) * reelDefinition.SymbolSpacing);
		offset = totalWidth / 2f;
		float yPos = (-offset + (reelDefinition.SymbolSize / 2f));

		//float xPos = -((slotsDefinition.ReelDefinitions.Length) * (reelDefinition.ReelsSpacing + reelDefinition.SymbolSize)) / 2f;
		//float xPos = -((slotsDefinition.ReelDefinitions.Length) * (reelDefinition.ReelsSpacing + reelDefinition.SymbolSize)) / 2f;
		//float yPos = -((reelDefinition.SymbolCount - 1) * (reelDefinition.SymbolSpacing + reelDefinition.SymbolSize)) / 2f;
		
		reelsGroup.transform.localPosition = new Vector3(xPos, yPos, 0);
	}

	void OnReelCompleted(object e)
	{
		//int value = (int)e;

		//Debug.Log($"Reel {value} Completed");

		if (reels.TrueForAll(x => !x.Spinning))
		{
			spinInProgress = false;
			EventManager.Instance.BroadcastEvent("SpinCompleted");
		}
	}

	void OnSpinCompleted(object e)
	{
		//Debug.Log($"Spin Completed");

		StateMachine.Instance.SetState(State.Presentation);
	}

	private void OnPresentationEnter(object obj)
	{
		foreach (GameReel gr in reels)
		{
			gr.DimDummySymbols();
		}
	}

	public GameSymbol[] GetCurrentSymbolGrid()
	{
		List<GameSymbol[]> reelSymbols = new List<GameSymbol[]>();
		foreach (GameReel gameReel in reels)
		{
			var newList = new GameSymbol[gameReel.Symbols.Count];
			for(int i = 0; i < gameReel.Symbols.Count; i++)
			{
				newList[i] = gameReel.Symbols[i];
			}
			reelSymbols.Add(newList);
		}

		return Helpers.CombineColumnsToGrid(reelSymbols);
	}
}
