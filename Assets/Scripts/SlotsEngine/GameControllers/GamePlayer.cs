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
		// iterate over a copy to avoid collection modified during callbacks
		foreach (var s in playerSlots.ToArray())
		{
			s.SetState(state);
		}
	}

	public void InitializePlayer(BetLevelDefinition defaultBetLevel)
	{
		GlobalEventManager.Instance.RegisterEvent(SlotsEvent.BetUpPressed, OnBetUpPressed);
		GlobalEventManager.Instance.RegisterEvent(SlotsEvent.BetDownPressed, OnBetDownPressed);
		GlobalEventManager.Instance.RegisterEvent(SlotsEvent.SpinButtonPressed, OnPlayerInputPressed);
		GlobalEventManager.Instance.RegisterEvent(SlotsEvent.StopButtonPressed, OnPlayerInputPressed);
		GlobalEventManager.Instance.RegisterEvent(SlotsEvent.PlayerInputPressed, OnPlayerInputPressed);

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
		// Space input (keep in runtime)
		if (Input.GetKeyDown(KeyCode.Space))
		{
			GlobalEventManager.Instance.BroadcastEvent(SlotsEvent.PlayerInputPressed);
		}

#if UNITY_EDITOR
		// Debug/testing inputs (editor only)
		// Testing add slots
		if (Input.GetKeyDown(KeyCode.S))
		{
			SpawnSlots(null, true);
		}

		// Testing add reels
		if (Input.GetKeyDown(KeyCode.R))
		{
			var slots = playerSlots.GetRandom();
			slots.AddReel();
		}

		// Testing add symbols
		if (Input.GetKeyDown(KeyCode.A))
		{
			var slots = playerSlots.GetRandom();
			var reel = slots.CurrentReels.GetRandom();
			reel.SetSymbolCount(reel.CurrentReelData.SymbolCount + 1);
		}
		if (Input.GetKeyDown(KeyCode.Z))
		{
			var slots = playerSlots.GetRandom();
			var reel = slots.CurrentReels.GetRandom();
			reel.SetSymbolCount(reel.CurrentReelData.SymbolCount - 1);
		}

		// Testing kill slots
		if (Input.GetKeyDown(KeyCode.X))
		{
			if (playerSlots.Count > 0)
			{
				RemoveSlots(playerSlots.Last());
			}
		}

		// Testing add credits
		if (Input.GetKeyDown(KeyCode.Alpha1))
		{
			Debug.LogWarning("Adding credits for testing.");
			AddCredits(100);
		}

		// Testing slow/speed game
		if (Input.GetKeyDown(KeyCode.Minus))
		{
			Time.timeScale = Mathf.Clamp(Time.timeScale - 0.1f, 0.1f, 10f);
			Debug.LogWarning(Time.timeScale);
		}
		if (Input.GetKeyDown(KeyCode.Equals))
		{
			Time.timeScale = Mathf.Clamp(Time.timeScale + 0.1f, 0.1f, 10f);
			Debug.LogWarning(Time.timeScale);
		}
#endif
	}

	public void BeginGame()
	{
		// iterate over copy in case BeginSlots can modify playerSlots
		foreach (var slots in playerSlots.ToArray())
		{
			slots.BeginSlots();
		}

		SetCurrentBet(playerData.CurrentBet);
		GlobalEventManager.Instance.BroadcastEvent(SlotsEvent.CreditsChanged, CurrentCredits);
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

		if (CurrentBet == null)
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
		GlobalEventManager.Instance.BroadcastEvent(SlotsEvent.CreditsChanged, CurrentCredits);
	}

	public void SetCurrentBet(BetLevelDefinition bet)
	{
		if (!CheckAllSlotsState(State.Idle))
		{
			return;
		}

		playerData.SetCurrentBet(bet);
		GlobalEventManager.Instance.BroadcastEvent(SlotsEvent.BetChanged, bet);
	}

	private void OnBetDownPressed(object obj)
	{
		if (primarySlots == null || primarySlots.CurrentSlotsData == null) return;

		var betLevels = primarySlots.CurrentSlotsData.BetLevelDefinitions;

		int currentIndex = betLevels.IndexOf(CurrentBet);
		if (currentIndex > 0)
		{
			SetCurrentBet(betLevels[currentIndex - 1]);
		}
	}

	private void OnBetUpPressed(object obj)
	{
		if (primarySlots == null || primarySlots.CurrentSlotsData == null) return;

		var betLevels = primarySlots.CurrentSlotsData.BetLevelDefinitions;

		int currentIndex = betLevels.IndexOf(CurrentBet);
		if (currentIndex >= 0 && currentIndex + 1 < betLevels.Count)
		{
			SetCurrentBet(betLevels[currentIndex + 1]);
		}
	}

	private void OnPlayerInputPressed(object obj)
	{
		bool spinPurchase = RequestSpinPurchase();

		// iterate over copy to avoid modification during iteration
		foreach (var slots in playerSlots.ToArray())
		{
			slots.SpinOrStopReels(spinPurchase);
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		// Ensure we unregister any global events we registered to avoid dangling callbacks
		var gm = GlobalEventManager.Instance;
		if (gm != null)
		{
			try
			{
				gm.UnregisterEvent(SlotsEvent.BetUpPressed, OnBetUpPressed);
				gm.UnregisterEvent(SlotsEvent.BetDownPressed, OnBetDownPressed);
				gm.UnregisterEvent(SlotsEvent.SpinButtonPressed, OnPlayerInputPressed);
				gm.UnregisterEvent(SlotsEvent.StopButtonPressed, OnPlayerInputPressed);
				gm.UnregisterEvent(SlotsEvent.PlayerInputPressed, OnPlayerInputPressed);
			}
			catch (Exception ex)
			{
				Debug.LogException(ex);
			}
		}
	}
}
