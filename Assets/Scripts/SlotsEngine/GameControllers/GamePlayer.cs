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

	private SlotsEngine primarySlots => playerSlots.Count > 0 ? playerSlots[0] : null;

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

		//	Testing add slots
		if (Input.GetKeyDown(KeyCode.S))
		{
			SpawnSlots(null, true);
		}

		//	Testing add reels
		if (Input.GetKeyDown(KeyCode.R))
		{
			var slots = playerSlots.GetRandom();
			slots.AddReel();
		}

		//	Testing add symbols
		if (Input.GetKeyDown(KeyCode.A))
		{
			var slots = playerSlots.GetRandom();
			var reel = slots.CurrentReels.GetRandom();
			reel.SetSymbolCount(reel.CurrentReelData.SymbolCount + 1);
		}

		//	Testing kill slots
		if (Input.GetKeyDown(KeyCode.X))
		{
			if (playerSlots.Count > 0)
			{
				RemoveSlots(playerSlots.Last());
			}
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
		SlotsEngine newSlots = SlotsEngineManager.Instance.CreateSlots(existingData);

		if (beginSlots)
		{
			newSlots.BeginSlots();
		}

		if (existingData == null)
		{
			playerData.AddSlots(newSlots.CurrentSlotsData);
		}

		playerSlots.Add(newSlots);

		SlotsEngineManager.Instance.AdjustSlotsCanvases();
	}

	private void RemoveSlots(SlotsEngine slotsToRemove)
	{
		if (slotsToRemove == null)
		{
			throw new ArgumentNullException(nameof(slotsToRemove));
		}

		if (!playerSlots.Contains(slotsToRemove))
		{
			throw new Exception("Tried to remove slots that player doesn't have!");
		}

		// Prevent removal while the slot is actively spinning
		if (slotsToRemove.CurrentState == State.Spinning)
		{
			Debug.LogWarning("Cannot remove slots while spinning.");
			return;
		}

		// Remove player data entry if present
		if (slotsToRemove.CurrentSlotsData != null)
		{
			playerData.RemoveSlots(slotsToRemove.CurrentSlotsData);
		}

		// Remove from our active list first to avoid callers accessing it during destroy
		playerSlots.Remove(slotsToRemove);

		// Destroy visual/engine objects
		SlotsEngineManager.Instance.DestroySlots(slotsToRemove);
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
		if (primarySlots == null || primarySlots.CurrentSlotsData == null) return;

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
		if (primarySlots == null || primarySlots.CurrentSlotsData == null) return;

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
