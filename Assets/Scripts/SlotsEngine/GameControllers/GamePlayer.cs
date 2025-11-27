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

		// Initialize inventory if missing (legacy saved data support)
		if (playerData.Inventory == null)
		{
			playerData.AddInventoryItem(new InventoryItemData("Starter Reel Strip", InventoryItemType.ReelStrip, null));
		}

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
			return;
		}


		// Testing remove slots
		if((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.S))
		{
			RemoveTestSlot();
		}
		// Testing add slots
		else if (Input.GetKeyDown(KeyCode.S) && playerSlots.All(x => x.CurrentState == State.Idle))
		{
			AddTestSlot();
		}

		// Testing remove reels (Shift+R)
		if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.R) && playerSlots.All(x => x.CurrentState == State.Idle))
		{
			RemoveTestReel();
		}
		// Testing add reels
		else if (Input.GetKeyDown(KeyCode.R) && playerSlots.All(x => x.CurrentState == State.Idle))
		{
			AddTestReel();
		}

		// Testing remove symbols
		if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.A) && playerSlots.All(x => x.CurrentState == State.Idle))
		{
			RemoveTestSymbol();
		}
		// Testing add symbols
		else if (Input.GetKeyDown(KeyCode.A) && playerSlots.All(x => x.CurrentState == State.Idle))
		{
			AddTestSymbol();
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
	}

	// ---------------- Test item helpers ----------------
	private SlotsEngine GetRandomIdleSlot()
	{
		var candidates = playerSlots.Where(s => s != null && s.CurrentState == State.Idle).ToList();
		return candidates.Count == 0 ? null : candidates.GetRandom();
	}

	public void AddTestSlot()
	{
		SpawnSlots(null, true);
	}

	public void RemoveTestSlot()
	{
		var slot = GetRandomIdleSlot();
		if (slot == null)
		{
			Debug.LogWarning("No idle slot available to remove.");
			return;
		}
		RemoveSlots(slot);
	}

	public void AddTestReel()
	{
		var slot = GetRandomIdleSlot();
		if (slot == null)
		{
			Debug.LogWarning("No slot available to add a reel.");
			return;
		}
		slot.AddReel();
		playerData.AddInventoryItem(new InventoryItemData("Reel", InventoryItemType.Reel, null));
	}

	public void RemoveTestReel()
	{
		var slot = GetRandomIdleSlot();
		if (slot == null)
		{
			Debug.LogWarning("No slot available to remove a reel.");
			return;
		}
		var reel = slot.CurrentReels.GetRandom();
		if (reel == null)
		{
			Debug.LogWarning("Selected slot has no reels to remove.");
			return;
		}
		// Use engine API to remove the entire reel now that it's implemented
		slot.RemoveReel(reel);

		// Remove a matching InventoryItem (best-effort): prefer the most recently added Reel item
		try
		{
			if (playerData != null)
			{
				var reels = playerData.GetItemsOfType(InventoryItemType.Reel);
				if (reels != null && reels.Count > 0)
				{
					// remove the last added reel item
					var toRemove = reels[reels.Count - 1];
					playerData.RemoveInventoryItem(toRemove);
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"RemoveTestReel: failed to remove inventory item: {ex.Message}");
		}
	}

	public void AddTestSymbol()
	{
		var slot = GetRandomIdleSlot();
		if (slot == null)
		{
			Debug.LogWarning("No slot available to add a symbol.");
			return;
		}
		var reel = slot.CurrentReels.GetRandom();
		if (reel == null)
		{
			Debug.LogWarning("Selected slot has no reels to add a symbol to.");
			return;
		}
		reel.SetSymbolCount(reel.CurrentReelData.SymbolCount + 1);
		playerData.AddInventoryItem(new InventoryItemData("Symbol" + reel.CurrentReelData.SymbolCount, InventoryItemType.Symbol, reel.CurrentReelData.CurrentReelStrip?.Definition?.name));
	}

	public void RemoveTestSymbol()
	{
		var slot = GetRandomIdleSlot();
		if (slot == null)
		{
			Debug.LogWarning("No slot available to remove a symbol.");
			return;
		}
		var reel = slot.CurrentReels.GetRandom();
		if (reel == null)
		{
			Debug.LogWarning("Selected slot has no reels to remove a symbol from.");
			return;
		}
		reel.SetSymbolCount(Mathf.Max(1, reel.CurrentReelData.SymbolCount - 1));

		// Best-effort: remove one Symbol inventory item (most recently added)
		try
		{
			if (playerData != null)
			{
				var syms = playerData.GetItemsOfType(InventoryItemType.Symbol);
				if (syms != null && syms.Count > 0)
				{
					var toRemove = syms[syms.Count - 1];
					playerData.RemoveInventoryItem(toRemove);
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"RemoveTestSymbol: failed to remove inventory item: {ex.Message}");
		}
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
			// Inventory registration for tracking
			playerData.AddInventoryItem(new InventoryItemData("Slot " + newSlots.CurrentSlotsData.Index, InventoryItemType.SlotEngine, null));
		}

		playerSlots.Add(newSlots);

		SlotsEngineManager.Instance.AdjustSlotsCanvases();
	}

	private void RemoveSlots(SlotsEngine slotsToRemove)
	{
		if (slotsToRemove == null) throw new ArgumentNullException(nameof(slotsToRemove));
		if (!playerSlots.Contains(slotsToRemove)) throw new Exception("Tried to remove slots that player doesn't have!");
		if (slotsToRemove.CurrentState == State.Spinning) { Debug.LogWarning("Cannot remove slots while spinning."); return; }

		// Remove player data entry if present
		if (slotsToRemove.CurrentSlotsData != null) playerData.RemoveSlots(slotsToRemove.CurrentSlotsData);

		// Also remove matching inventory item for this slot (best-effort match by display name)
		try
		{
			if (playerData != null && slotsToRemove.CurrentSlotsData != null)
			{
				var display = "Slot " + slotsToRemove.CurrentSlotsData.Index;
				var slotItems = playerData.GetItemsOfType(InventoryItemType.SlotEngine);
				if (slotItems != null)
				{
					var found = slotItems.Find(i => i.DisplayName == display);
					if (found != null) playerData.RemoveInventoryItem(found);
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"RemoveSlots: failed to remove inventory item: {ex.Message}");
		}

		// Remove from list first
		playerSlots.Remove(slotsToRemove);

		// Destroy engine (SlotsEngineManager will broadcast AllSlotsRemoved if it becomes empty)
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
			if (spinPurchase)
			{
				slots.SpinOrStopReels(true);
			}
			else
			{
				// Request stop at engine level; engines will defer or ignore repeated requests.
				slots.RequestStopWhenReady();
			}
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
