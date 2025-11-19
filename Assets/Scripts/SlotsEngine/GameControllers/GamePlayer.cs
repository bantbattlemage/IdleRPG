using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GamePlayer : Singleton<GamePlayer>
{
	private PlayerData playerData;
	public PlayerData PlayerData => playerData;

	[HideInInspector] public BetLevelDefinition CurrentBet => playerData.CurrentBet;
	public int CurrentCredits => playerData.Credits;

	private List<SlotsEngine> playerSlots = new List<SlotsEngine>();

	private SlotsEngine primarySlots => playerSlots[0];

	public bool CheckAllSlotsState(State state)
	{
		return playerSlots.TrueForAll(x => x.CurrentState == state);
	}

	private void SetAllSlotsState(State state)
	{
		playerSlots.ForEach(x => x.SetState(state));
	}

	public void InitializePlayer(BetLevelDefinition defaultBetLevel)
	{
		GlobalEventManager.Instance.RegisterEvent("BetUpPressed", OnBetUpPressed);
		GlobalEventManager.Instance.RegisterEvent("BetDownPressed", OnBetDownPressed);
		GlobalEventManager.Instance.RegisterEvent("SpinButtonPressed", OnPlayerInputPressed);
		GlobalEventManager.Instance.RegisterEvent("StopButtonPressed", OnPlayerInputPressed);
		GlobalEventManager.Instance.RegisterEvent("PlayerInputPressed", OnPlayerInputPressed);

		playerData = PlayerDataManager.Instance.GetPlayerData();

		if (playerData.CurrentSlots == null || playerData.CurrentSlots.Count == 0)
		{
			SpawnSlots();
		}
		else
		{
			foreach (SlotsData data in playerData.CurrentSlots)
			{
				SpawnSlots(data);
			}
		}

		if (playerData.CurrentBet == null)
		{
			playerData.SetCurrentBet(defaultBetLevel);
		}
	}

	void Update()
	{
		//	Space input
		if (Input.GetKeyDown(KeyCode.Space))
		{
			GlobalEventManager.Instance.BroadcastEvent("PlayerInputPressed");
		}

		//	testing save data
		if (Input.GetKeyDown(KeyCode.F1))
		{
			SlotsDataManager.Instance.ClearSlotsData();

			foreach (SlotsEngine slots in playerSlots)
			{
				slots.SaveSlotsData();
			}

			Debug.LogWarning("Saving slot data");
		}

		//	testing reel scaling
		if (Input.GetKeyDown(KeyCode.UpArrow))
		{
			foreach (SlotsEngine slots in playerSlots)
			{
				slots.AdjustReelSize(true);
			}
		}
		if (Input.GetKeyDown(KeyCode.DownArrow))
		{
			foreach (SlotsEngine slots in playerSlots)
			{
				slots.AdjustReelSize(false);
			}
		}

		//	Testing add slots
		if (Input.GetKeyDown(KeyCode.R))
		{
			SpawnSlots(null, true);
		}

		//	Testing kill slots
		if (Input.GetKeyDown(KeyCode.X))
		{
			RemoveSlots(playerSlots.Last());
		}

		//	Testing add credits
		if (Input.GetKeyDown(KeyCode.Alpha1))
		{
			Debug.LogWarning("Adding credits for testing.");
			AddCredits(100);
		}

		//	Testing slow/speed game
		if (Input.GetKeyDown(KeyCode.Minus))
		{
			Time.timeScale -= 0.1f;
			Debug.LogWarning(Time.timeScale);
		}
		if (Input.GetKeyDown(KeyCode.Equals))
		{
			Time.timeScale += 0.1f;
			Debug.LogWarning(Time.timeScale);
		}
	}

	public void BeginGame()
	{
		foreach (SlotsEngine slots in playerSlots)
		{
			slots.BeginSlots();
		}

		SetCurrentBet(playerData.CurrentBet);
		GlobalEventManager.Instance.BroadcastEvent("CreditsChanged", CurrentCredits);
	}

	private void SpawnSlots(SlotsData existingData = null, bool beginSlots = false)
	{
		SlotsEngine newSlots = SlotsEngineController.Instance.CreateSlots(existingData);

		if (beginSlots)
		{
			newSlots.BeginSlots();
		}

		if (existingData == null)
		{
			playerData.AddSlots(newSlots.CurrentSlotsData);
		}

		playerSlots.Add(newSlots);

		SlotsEngineController.Instance.AdjustSlotsCanvases();
	}

	private void RemoveSlots(SlotsEngine slotsToRemove)
	{
		if (!playerSlots.Contains(slotsToRemove))
		{
			throw new Exception("Tried to remove slots that player doesn't have!");
		}

		playerData.RemoveSlots(slotsToRemove.CurrentSlotsData);
		playerSlots.Remove(slotsToRemove);
		SlotsEngineController.Instance.DestroySlots(slotsToRemove);
	}

	public bool RequestSpinPurchase()
	{
		if (!CheckAllSlotsState(State.Idle))
		{
			return false;
		}

		if (CurrentCredits < CurrentBet.CreditCost)
		{
			return false;
		}

		AddCredits(-CurrentBet.CreditCost);

		SetAllSlotsState(State.SpinPurchased);

		return true;
	}

	public void AddCredits(int value)
	{
		playerData.SetCurrentCredits(CurrentCredits + value);
		GlobalEventManager.Instance.BroadcastEvent("CreditsChanged", CurrentCredits);
	}

	public void SetCurrentBet(BetLevelDefinition bet)
	{
		if (!CheckAllSlotsState(State.Idle))
		{
			return;
		}

		playerData.SetCurrentBet(bet);
		GlobalEventManager.Instance.BroadcastEvent("BetChanged", bet);
	}

	private void OnBetDownPressed(object obj)
	{
		var betLevels = primarySlots.CurrentSlotsData.BetLevelDefinitions;

		int targetLevel = -1;
		for (int i = 0; i < betLevels.Count; i++)
		{
			if (CurrentBet == betLevels[i] && i - 1 >= 0)
			{
				targetLevel = i - 1;
				break;
			}
		}

		if (targetLevel == -1)
		{
			return;
		}

		SetCurrentBet(betLevels[targetLevel]);
	}

	private void OnBetUpPressed(object obj)
	{
		var betLevels = primarySlots.CurrentSlotsData.BetLevelDefinitions;

		int targetLevel = -1;
		for (int i = 0; i < betLevels.Count; i++)
		{
			if (CurrentBet == betLevels[i] && i + 1 < betLevels.Count)
			{
				targetLevel = i + 1;
				break;
			}
		}

		if (targetLevel == -1)
		{
			return;
		}

		SetCurrentBet(betLevels[targetLevel]);
	}

	private void OnPlayerInputPressed(object obj)
	{
		bool spinPurchase = RequestSpinPurchase();

		foreach (SlotsEngine slots in playerSlots)
		{
			slots.SpinOrStopReels(spinPurchase);
		}
	}
}
