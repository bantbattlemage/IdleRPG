using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;
using NUnit.Framework;

public class SlotsEngine : Singleton<SlotsEngine>
{
	[SerializeField] private Canvas gameCanvas;
	[SerializeField] private GameObject reelPrefab;
	[SerializeField] private SlotsDefinition slotsDefinition;
	public SlotsDefinition SlotsDefinition => slotsDefinition;

	private List<GameReel> reels = new List<GameReel>();

	private bool spinInProgress = false;

	void Start()
	{
		SpawnReels();

		EventManager.Instance.RegisterEvent("SpinCompleted", OnSpinCompleted);
		EventManager.Instance.RegisterEvent("ReelCompleted", OnReelCompleted);
		EventManager.Instance.RegisterEvent("PresentationEnter", OnPresentationEnter);

		EventManager.Instance.RegisterEvent("SpinButtonPressed", OnSpinButtonPressed);
		EventManager.Instance.RegisterEvent("StopButtonPressed", OnStopButtonPressed);

		SlotConsoleController.Instance.InitializeConsole();

		StateMachine.Instance.SetState(State.Idle);
	}

	void Update()
	{
		if (Input.GetKeyDown(KeyCode.Space))
		{
			PlayerInputPressed();
		}
	}

	private void PlayerInputPressed()
	{
		if (!spinInProgress && StateMachine.Instance.CurrentState == State.Idle)
		{
			SpinAllReels();
		}
		else if (spinInProgress && StateMachine.Instance.CurrentState == State.Spinning)
		{
			StopAllReels();
		}
	}

	private void SpinAllReels()
	{
		if (spinInProgress)
		{
			throw new Exception("spin already in progress!");
		}

		for (int i = 0; i < reels.Count; i++)
		{
			List<SymbolDefinition> testSolution = new List<SymbolDefinition>();
			for (int k = 0; k < reels[i].Definition.SymbolCount; k++)
			{
				testSolution.Add(SymbolSpawner.Instance.GetRandomSymbol());
			}

			reels[i].BeginSpin(testSolution);
		}

		spinInProgress = true;
		StateMachine.Instance.SetState(State.Spinning);
	}

	private void StopAllReels()
	{
		for (int i = 0; i < reels.Count; i++)
		{
			reels[i].CompleteSpin();
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
		reelsGroup.transform.localPosition = new Vector3(-((slotsDefinition.ReelDefinitions.Length-1) * (reelDefinition.ReelsSpacing + reelDefinition.SymbolSize))/2f, -((reelDefinition.SymbolCount-1) * (reelDefinition.SymbolSpacing + reelDefinition.SymbolSize))/2f, 0);
	}

	private void OnSpinButtonPressed(object obj)
	{
		PlayerInputPressed();
	}

	private void OnStopButtonPressed(object obj)
	{
		PlayerInputPressed();
	}

	void OnReelCompleted(object e)
	{
		int value = (int)e;

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
		List<WinData> winData = WinlineEvaluator.Instance.EvaluateWins(GetCurrentSymbolGrid().ToSymbolDefinitions(), slotsDefinition.WinlineDefinitions);

		if (winData.Count > 0)
		{
			foreach (WinData w in winData)
			{
				Debug.LogWarning($"Won {w.WinValue} on line {w.LineIndex}!");

				foreach (int index in w.WinningSymbolIndexes)
				{
					EventManager.Instance.BroadcastEvent("SymbolWin", GetCurrentSymbolGrid()[index]);
				}
			}

			DOTween.Sequence().AppendInterval(2f).AppendCallback(CompletePresentation);
		}
		else
		{
			CompletePresentation();
			//DOTween.Sequence().AppendInterval(0.2f).AppendCallback(CompletePresentation);
		}
	}

	public void CompletePresentation()
	{
		StateMachine.Instance.SetState(State.Idle);
	}

	public GameSymbol[] GetCurrentSymbolGrid()
	{
		List<GameSymbol[]> reelSymbols = new List<GameSymbol[]>();
		foreach (GameReel g in reels)
		{
			var newList = new GameSymbol[g.Symbols.Count];
			for(int i = 0; i < g.Symbols.Count; i++)
			{
				newList[i] = g.Symbols[i];
			}
			reelSymbols.Add(newList);
		}

		return Helpers.CombineColumnsToGrid(reelSymbols);
	}

	public SymbolDefinition[] ToSymbolDefinitions(List<GameSymbol> symbols)
	{
		List<SymbolDefinition> definitions = new List<SymbolDefinition>();

		foreach (GameSymbol s in symbols)
		{
			definitions.Add(s.Definition);
		}

		return definitions.ToArray();
	}
}
