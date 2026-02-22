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

	public void InitializePlayer(BetLevelDefinition defaultBetLevel, bool isNewGame = false)
	{
		GlobalEventManager.Instance.RegisterEvent(SlotsEvent.BetUpPressed, OnBetUpPressed);
		GlobalEventManager.Instance.RegisterEvent(SlotsEvent.BetDownPressed, OnBetDownPressed);
		GlobalEventManager.Instance.RegisterEvent(SlotsEvent.SpinButtonPressed, OnPlayerInputPressed);
		GlobalEventManager.Instance.RegisterEvent(SlotsEvent.StopButtonPressed, OnPlayerInputPressed);
		GlobalEventManager.Instance.RegisterEvent(SlotsEvent.PlayerInputPressed, OnPlayerInputPressed);

		playerData = PlayerDataManager.Instance.GetPlayerData();

		// Only spawn a default slot when explicitly starting a new game. When continuing/loading an existing
		// profile that contains zero saved slots, do not auto-create a slot so the user's "no slots" choice
		// is preserved.
		if (playerData.CurrentSlots == null || playerData.CurrentSlots.Count == 0)
		{
			if (isNewGame)
			{
				SpawnSlots();
			}
			else
			{
				Debug.Log("Player has no persisted slots and this is a continue/load; not spawning default slots.");
			}
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

		// Note: Test shortcuts moved to TestTool.cs GUI (toggle with F1)
	}

	// ---------------- Test item helpers ----------------
	private SlotsEngine GetRandomIdleSlot()
	{
		var candidates = playerSlots.Where(s => s != null && s.CurrentState == State.Idle).ToList();
		return candidates.Count == 0 ? null : candidates.GetRandom();
	}

	private bool IsSlotInventoryBacked(SlotsEngine slot)
	{
		if (slot == null || playerData == null) return false;
		try
		{
			var slotItems = playerData.GetItemsOfType(InventoryItemType.SlotEngine);
			if (slotItems != null)
			{
				foreach (var s in slotItems)
				{
					if (s == null) continue;
					// Prefer explicit DefinitionAccessorId match
					if (s.DefinitionAccessorId != 0 && s.DefinitionAccessorId == slot.CurrentSlotsData?.AccessorId)
					{
						return true;
					}
					// Legacy fallback: display-name match for items that don't reference an accessor
					if (s.DefinitionAccessorId == 0 && s.DisplayName == ("Slot " + slot.CurrentSlotsData?.Index))
					{
						return true;
					}
				}
			}
		}
		catch { }
		return false;
	}

	private SlotsEngine GetRandomIdleInventoryBackedSlot()
	{
		// Prefer most-recently-added idle inventory-backed slot for deterministic behavior
		for (int i = playerSlots.Count - 1; i >= 0; i--)
		{
			var s = playerSlots[i];
			if (s == null) continue;
			if (s.CurrentState != State.Idle) continue;
			if (IsSlotInventoryBacked(s)) return s;
		}
		return null;
	}

	private SlotsEngine GetRandomIdleNonInventorySlot()
	{
		// Prefer most-recently-added idle non-inventory slot for deterministic behavior
		for (int i = playerSlots.Count - 1; i >= 0; i--)
		{
			var s = playerSlots[i];
			if (s == null) continue;
			if (s.CurrentState != State.Idle) continue;
			if (!IsSlotInventoryBacked(s)) return s;
		}
		return null;
	}

	public void AddTestSlot()
	{
		// Create a test slot that IS represented as an Inventory item
		SpawnSlots(null, true, true);
	}

	public void AddTestSlotNoInventory()
	{
		// Create a test slot that is NOT represented as an Inventory item (legacy-style slot)
		SpawnSlots(null, true, false);
	}

	public void RemoveTestSlot()
	{
		var slot = GetRandomIdleInventoryBackedSlot();
		if (slot == null)
		{
			Debug.LogWarning("No idle inventory-backed slot available to remove.");
			return;
		}
		RemoveSlots(slot);
	}

	public void RemoveTestNonInventorySlot()
	{
		// Diagnostic: log current slots and why none might be considered non-inventory/idle
		try
		{
			Debug.Log($"RemoveTestNonInventorySlot: playerSlots.Count={playerSlots?.Count ?? 0}");
			for (int i = 0; i < playerSlots.Count; i++)
			{
				var s = playerSlots[i];
				if (s == null) { Debug.Log($" slot[{i}] = null"); continue; }
				var sd = s.CurrentSlotsData;
				int accessor = sd != null ? sd.AccessorId : -1;
				int index = sd != null ? sd.Index : -1;
				bool inventoryBacked = IsSlotInventoryBacked(s);
				Debug.Log($" slot[{i}] index={index} accessor={accessor} state={s.CurrentState} inventoryBacked={inventoryBacked}");
			}
		}
		catch (Exception ex) { Debug.LogWarning($"RemoveTestNonInventorySlot: diagnostic logging failed: {ex.Message}"); }

		var slot = GetRandomIdleNonInventorySlot();
		if (slot == null)
		{
			Debug.LogWarning("No idle non-inventory slot available to remove.");
			return;
		}
		RemoveSlots(slot);
	}

	// --------------------- Reels and Symbols ----------------------
	public void AddTestReel()
	{
		var slot = GetRandomIdleSlot();
		if (slot == null)
		{
			Debug.LogWarning("No slot available to add a reel.");
			return;
		}
		slot.AddReel();

		// Try to locate the newly added reel and register its runtime strip + symbols into inventory
		try
		{
			var addedReel = slot.CurrentReels != null && slot.CurrentReels.Count > 0 ? slot.CurrentReels.Last() : null;
			var rd = addedReel?.CurrentReelData;
			var strip = rd?.CurrentReelStrip;
			if (playerData != null && strip != null)
			{
				// Only register runtime strips into inventory if this slot is inventory-backed. Avoid creating inventory
				// entries for legacy/non-inventory slots.
				bool slotIsInventoryBacked = false;
				try
				{
					var slotItems = playerData.GetItemsOfType(InventoryItemType.SlotEngine);
					if (slotItems != null)
					{
						foreach (var s in slotItems)
						{
							if (s == null) continue;
							// Prefer DefinitionAccessorId match then fall back to display name matching
							if (s.DefinitionAccessorId != 0 && s.DefinitionAccessorId == slot.CurrentSlotsData?.AccessorId) { slotIsInventoryBacked = true; break; }
							if (s.DisplayName == ("Slot " + slot.CurrentSlotsData?.Index)) { slotIsInventoryBacked = true; break; }
						}
					}
				}
				catch { slotIsInventoryBacked = false; }

				if (slotIsInventoryBacked)
				{
					string display = null;
					try { display = $"ReelStrip {slot.CurrentSlotsData?.Index ?? 0}-{slot.CurrentReels.IndexOf(addedReel)}"; } catch { display = null; }
					playerData.RegisterRuntimeReelStrip(strip, display);
				}
				else
				{
					Debug.Log("AddTestReel: slot is not inventory-backed; skipping registering runtime strip to inventory.");
				}
			}
			else
			{
				Debug.LogWarning("AddTestReel: newly added reel has no runtime strip to register.");
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"AddTestReel: failed to register reel inventory: {ex.Message}");
		}
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
			RemoveMostRecentInventoryItem(InventoryItemType.Reel);
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

		// Previously this modified a reel. New behavior: only add an inventory item using SymbolDefinitionManager
		var pd = playerData;
		if (pd == null)
		{
			Debug.LogWarning("PlayerData missing; cannot add test symbol.");
			return;
		}

		string newName = "Symbol" + (pd.Inventory?.Items.Count + 1);
		Sprite chosen = null;
		if (SymbolDefinitionManager.Instance != null)
		{
			var defs = SymbolDefinitionManager.Instance.GetAllDefinitions();
			if (defs != null && defs.Count > 0)
			{
				for (int i = 0; i < defs.Count; i++)
				{
					var d = defs[i]; if (d == null) continue;
					if (d.SymbolSprite != null) { chosen = d.SymbolSprite; break; }
				}
			}
		}

		// No legacy fallback: if no symbol definitions are available, add symbol without sprite
		AddSymbolInventory(chosen, newName);
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
			RemoveMostRecentInventoryItem(InventoryItemType.Symbol);
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

	private void SpawnSlots(SlotsData existingData = null, bool beginSlots = false, bool createInventoryItems = true)
	{
		SlotsEngine newSlots = SlotsEngineManager.Instance.CreateSlots(existingData);

		if (beginSlots)
		{
			newSlots.BeginSlots();
		}

		// If this is a brand-new slot (existingData == null) we need to register it with PlayerData so it becomes
		// part of the player's CurrentSlots collection. Optionally, we also create Inventory entries for the slot
		// and for any runtime reel strips/symbols it created. Passing createInventoryItems=false will skip creating
		// any InventoryItemData entries so the slot and its strips/symbols will not be exposed or modifiable in the UI.
		if (existingData == null)
		{
			playerData.AddSlots(newSlots.CurrentSlotsData);

			if (createInventoryItems)
			{
				// Inventory registration for tracking
				playerData.AddInventoryItem(new InventoryItemData("Slot " + newSlots.CurrentSlotsData.Index, InventoryItemType.SlotEngine, newSlots.CurrentSlotsData.AccessorId));

				// NEW: Register runtime reel strips used by this slot and add associated symbol inventory items
				try
				{
					if (newSlots?.CurrentSlotsData?.CurrentReelData != null)
					{
						for (int i = 0; i < newSlots.CurrentSlotsData.CurrentReelData.Count; i++)
						{
							var rd = newSlots.CurrentSlotsData.CurrentReelData[i];
							if (rd == null) continue;
							var strip = rd.CurrentReelStrip;
							if (strip == null) continue;

							// Delegate registration to the authoritative PlayerData helper to avoid duplicated logic
							string stripDisplay = $"ReelStrip {newSlots.CurrentSlotsData.Index}-{i}";
							playerData.RegisterRuntimeReelStrip(strip, stripDisplay);
						}
					}
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"SpawnSlots: failed to register runtime reelstrip inventory for new slot: {ex.Message}");
				}
			}
		}

		playerSlots.Add(newSlots);

		SlotsEngineManager.Instance.AdjustSlotsCanvases();
	}

	private void RemoveSlots(SlotsEngine slotsToRemove)
	{
		if (slotsToRemove == null) throw new ArgumentNullException(nameof(slotsToRemove));
		if (!playerSlots.Contains(slotsToRemove)) throw new Exception("Tried to remove slots that player doesn't have!");
		if (slotsToRemove.CurrentState == State.Spinning) { Debug.LogWarning("Cannot remove slots while spinning."); return; }

		// Disassociate inventory items from this slot's reel strips before removal
		try
		{
			if (slotsToRemove.CurrentSlotsData?.CurrentReelData != null && playerData != null)
			{
				foreach (var reel in slotsToRemove.CurrentSlotsData.CurrentReelData)
				{
					if (reel == null) continue;
					var strip = reel.CurrentReelStrip;
					if (strip == null) continue;

					// Clear DefinitionAccessorId for ReelStrip inventory items matching this strip
					var reelStripItems = playerData.GetItemsOfType(InventoryItemType.ReelStrip);
					if (reelStripItems != null)
					{
						foreach (var item in reelStripItems)
						{
							if (item != null && item.DefinitionAccessorId == strip.AccessorId)
							{
								item.SetDefinitionAccessorId(0);
							}
						}
					}

					// NOTE: do NOT clear symbol inventory items here. Symbols should remain associated with their
					// ReelStrip instances even if the slot using the strip is removed. This preserves player-owned
					// symbol associations and allows reusing strips/symbols across slots.

				}

				// Additionally, remove any SlotEngine inventory item that references this slot
				try
				{
					var slotDisplay = "Slot " + slotsToRemove.CurrentSlotsData.Index;
					var slotItems = playerData.GetItemsOfType(InventoryItemType.SlotEngine);
					if (slotItems != null)
					{
						int targetAccessor = slotsToRemove.CurrentSlotsData.AccessorId;
						// iterate over a copy since RemoveInventoryItem will modify the underlying list
						foreach (var sItem in slotItems.ToList())
						{
							if (sItem == null) continue;
							if (targetAccessor > 0)
							{
								if (sItem.DefinitionAccessorId == targetAccessor)
								{
									playerData.RemoveInventoryItem(sItem);
								}
							}
							else
							{
								// Legacy fallback: only match by display name for items that don't already reference an accessor
								if (sItem.DefinitionAccessorId == 0 && sItem.DisplayName == slotDisplay)
								{
									playerData.RemoveInventoryItem(sItem);
								}
							}
						}
					}
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"RemoveSlots: failed to remove SlotEngine inventory items: {ex.Message}");
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"RemoveSlots: failed to disassociate inventory items: {ex.Message}");
		}

		// Remove player data entry if present
		try
		{
			if (slotsToRemove.CurrentSlotsData != null)
			{
				// Try direct removal first
				try { playerData.RemoveSlots(slotsToRemove.CurrentSlotsData); }
				catch (Exception)
				{
					// Fallback: remove any SlotsData with matching AccessorId to avoid stale references
					if (playerData.CurrentSlots != null && slotsToRemove.CurrentSlotsData.AccessorId > 0)
					{
					 playerData.CurrentSlots.RemoveAll(s => s != null && s.AccessorId == slotsToRemove.CurrentSlotsData.AccessorId);
					}
					else if (playerData.CurrentSlots != null)
					{
						// Last resort: remove by reference match
						playerData.CurrentSlots.RemoveAll(s => ReferenceEquals(s, slotsToRemove.CurrentSlotsData));
					}
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"RemoveSlots: failed to remove SlotsData from PlayerData: {ex.Message}");
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
			else if(!spinPurchase && slots.CurrentState == State.Spinning)
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

	private void AddSymbolInventory(Sprite sprite, string displayName = null)
	{
		if (playerData == null) return;
		string name = string.IsNullOrEmpty(displayName) ? "Symbol" + (playerData.Inventory?.Items.Count + 1) : displayName;
		if (sprite != null)
		{
			playerData.AddInventoryItem(new InventoryItemData(name, InventoryItemType.Symbol, 0, sprite.name));
		}
		else
		{
			playerData.AddInventoryItem(new InventoryItemData(name, InventoryItemType.Symbol, 0));
		}
	}

	private void RemoveMostRecentInventoryItem(InventoryItemType type)
	{
		if (playerData == null) return;
		var list = playerData.GetItemsOfType(type);
		if (list != null && list.Count > 0)
		{
			var toRemove = list[list.Count - 1];
			playerData.RemoveInventoryItem(toRemove);
		}
	}
}
