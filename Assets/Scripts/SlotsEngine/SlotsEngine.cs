using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class SlotsEngine : Singleton<SlotsEngine>
{
	[SerializeField] private Canvas gameCanvas;
	[SerializeField] private GameObject reelPrefab;
	[SerializeField] private ReelDefinition reelDefinition;

	private List<GameReel> reels = new List<GameReel>();

	private bool spinInProgress = false;

	void Start()
	{
		SpawnReels();

		EventManager.Instance.RegisterEvent("SpinCompleted", OnSpinCompleted);
		EventManager.Instance.RegisterEvent("ReelCompleted", OnReelCompleted);
		EventManager.Instance.RegisterEvent("PresentationEnter", OnPresentationEnter);
	}

	void Update()
	{
		if (Input.GetKeyDown(KeyCode.Space))
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
	}

	private void SpawnReels()
	{
		Transform reelsGroup = new GameObject("ReelsGroup").transform;
		reelsGroup.parent = gameCanvas.transform;
		reelsGroup.localScale = new Vector3(1, 1, 1);

		for (int i = 0; i < reelDefinition.ReelCount; i++)
		{
			GameObject r = Instantiate(reelPrefab, reelsGroup.transform);
			GameReel reel = r.GetComponent<GameReel>();
			reel.InitializeReel(reelDefinition, i);
			r.transform.localPosition = new Vector3((reelDefinition.ReelsSpacing + reelDefinition.SymbolSize) * i, 0, 0);
			reels.Add(reel);
		}

		reelsGroup.transform.localPosition = new Vector3(-((reelDefinition.ReelCount-1) * (reelDefinition.ReelsSpacing + reelDefinition.SymbolSize))/2f, -((reelDefinition.SymbolCount-1) * (reelDefinition.SymbolSpacing + reelDefinition.SymbolSize))/2f, 0);
	}

	void OnReelCompleted(object e)
	{
		int value = (int)e;

		Debug.Log($"Reel {value} Completed");

		if (reels.TrueForAll(x => !x.Spinning))
		{
			spinInProgress = false;
			EventManager.Instance.BroadcastEvent("SpinCompleted");
		}
	}

	void OnSpinCompleted(object e)
	{
		Debug.Log($"Spin Completed");

		StateMachine.Instance.SetState(State.Presentation);
	}

	private void OnPresentationEnter(object obj)
	{
		DOTween.Sequence().AppendInterval(1f).AppendCallback(CompletePresentation);
	}

	public void CompletePresentation()
	{
		StateMachine.Instance.SetState(State.Idle);
	}
}
