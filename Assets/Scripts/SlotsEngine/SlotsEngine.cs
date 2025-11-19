using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;

public class SlotsEngine : MonoBehaviour
{
	[SerializeField] private GameObject reelPrefab;
	
	private SlotsData currentSlotsData;
	public SlotsData CurrentSlotsData => currentSlotsData;

	private Transform reelsRootTransform;
	public Transform ReelsRootTransform => reelsRootTransform;

	private List<GameReel> reels = new List<GameReel>();
	private EventManager eventManager;
	private SlotsStateMachine stateMachine;

	private bool spinInProgress = false;

	public State CurrentState
	{
		get => stateMachine.CurrentState;
	}

	public void SetState(State state)
	{
		stateMachine.SetState(state);
	}

	public void InitializeSlotsEngine(Transform canvasTransform, SlotsDefinition definition)
	{
		currentSlotsData = definition.CreateInstance();

		for (int i = 0; i < definition.ReelDefinitions.Length; i++)
		{
			ReelData newData = definition.ReelDefinitions[i].CreateInstance();
			currentSlotsData.AddNewReel(newData);
		}

		eventManager = new EventManager();
		stateMachine = new SlotsStateMachine();
		stateMachine.InitializeStateMachine(this, eventManager);

		eventManager.RegisterEvent("SpinCompleted", OnSpinCompleted);
		eventManager.RegisterEvent("ReelSpinStarted", OnReelSpinStarted);
		eventManager.RegisterEvent("ReelCompleted", OnReelCompleted);
		eventManager.RegisterEvent("PresentationEnter", OnPresentationEnter);
		eventManager.RegisterEvent("PresentationComplete", OnPresentationComplete);

		reelsRootTransform = canvasTransform;

		SpawnReels(canvasTransform);
	}

	public void BeginSlots()
	{
		stateMachine.BeginStateMachine();
	}

	public void SpinOrStopReels(bool spin)
	{
		if (!spinInProgress && spin)
		{
			SpinAllReels();
		}
		else if (spinInProgress && !spin && stateMachine.CurrentState == State.Spinning)
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
			List<SymbolData> testSolution = new List<SymbolData>();
			for (int k = 0; k < reels[i].CurrentReelData.SymbolCount; k++)
			{
				testSolution.Add(reels[i].GetRandomSymbolFromStrip());
			}

			reels[i].CurrentReelData.SetCurrentSymbolData(testSolution);

			reels[i].BeginSpin(testSolution, falloutDelay);
			falloutDelay += 0.025f;
		}
	}

	private void OnReelSpinStarted(object obj)
	{
		if (reels.TrueForAll(x => x.Spinning))
		{
			spinInProgress = true;
			stateMachine.SetState(State.Spinning);
		}
	}

	private void StopAllReels()
	{
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

		eventManager.BroadcastEvent("StoppingReels");
	}

	private void SpawnReels(Transform gameCanvas)
	{
		Transform reelsGroup = new GameObject("ReelsGroup").transform;
		reelsGroup.parent = gameCanvas.transform;
		reelsGroup.localScale = new Vector3(1, 1, 1);

		for (int i = 0; i < currentSlotsData.CurrentReelData.Count; i++)
		{
			ReelData data = currentSlotsData.CurrentReelData[i];
			GameObject g = Instantiate(reelPrefab, reelsGroup.transform);
			GameReel reel = g.GetComponent<GameReel>();
			reel.InitializeReel(data, i, eventManager, data.DefaultReelStrip);
			g.transform.localPosition = new Vector3((data.SymbolSpacing + data.SymbolSize) * i, 0, 0);
			reels.Add(reel);
		}

		ReelData reelDefinition = currentSlotsData.CurrentReelData[0];

		int count = reels.Count;
		float totalWidth = (count * (reelDefinition.SymbolSize)) + ((count - 1) * reelDefinition.SymbolSpacing);
		float offset = totalWidth / 2f;
		float xPos = (-offset + (reelDefinition.SymbolSize / 2f));

		count = currentSlotsData.CurrentReelData.Max(x => x.SymbolCount);
		totalWidth = (count * (reelDefinition.SymbolSize)) + ((count - 1) * reelDefinition.SymbolSpacing);
		offset = totalWidth / 2f;
		float yPos = (-offset + (reelDefinition.SymbolSize / 2f));

		reelsGroup.transform.localPosition = new Vector3(xPos, yPos, 0);
	}

	void OnReelCompleted(object e)
	{
		if (reels.TrueForAll(x => !x.Spinning))
		{
			spinInProgress = false;
			eventManager.BroadcastEvent("SpinCompleted");
		}
	}

	void OnSpinCompleted(object e)
	{
		stateMachine.SetState(State.Presentation);
	}

	private void OnPresentationEnter(object obj)
	{
		foreach (GameReel gr in reels)
		{
			gr.DimDummySymbols();
		}

		eventManager.BroadcastEvent("BeginSlotPresentation", this);
	}

	private void OnPresentationComplete(object obj)
	{
		stateMachine.SetState(State.Idle);
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

	public void BroadcastSlotsEvent(string eventName, object value = null)
	{
		eventManager.BroadcastEvent(eventName, value);
	}

	public void SaveSlotsData()
	{
		SlotsDataManager.Instance.AddNewData(currentSlotsData);
		DataPersistenceManager.Instance.SaveGame();
	}
}
