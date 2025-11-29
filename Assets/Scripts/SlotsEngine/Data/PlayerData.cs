using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlayerData : Data
{
	[SerializeField] private int credits;
	public int Credits => credits;

	[SerializeField] private string currentBetKey;
	[System.NonSerialized] private BetLevelDefinition currentBet;
	public BetLevelDefinition CurrentBet
	{
		get { EnsureResolved(); return currentBet; }
	}

	[SerializeField] private List<SlotsData> currentSlots;
	public List<SlotsData> CurrentSlots => currentSlots;

	// --- New: Player inventory scaffolding ---
	[SerializeField] private PlayerInventory inventory;
	public PlayerInventory Inventory => inventory;

	public PlayerData(int c = 0, BetLevelDefinition bet = null)
	{
		credits = c;
		currentBet = bet;
		currentBetKey = bet != null ? bet.name : null;
		currentSlots = new List<SlotsData>();
		inventory = new PlayerInventory(); // initialize empty inventory
	}

	private void EnsureResolved()
	{
		if (currentBet == null && !string.IsNullOrEmpty(currentBetKey))
		{
			currentBet = DefinitionResolver.Resolve<BetLevelDefinition>(currentBetKey);
		}
	}

	public void AddSlots(SlotsData slots)
	{
		currentSlots.Add(slots);
	}

	public void RemoveSlots(SlotsData slots)
	{
		if (!currentSlots.Contains(slots))
		{
			throw new Exception("tried to remove slot that isn't registered!");
		}

		currentSlots.Remove(slots);
	}

	public void SetCurrentBet(BetLevelDefinition bet)
	{
		currentBet = bet;
		currentBetKey = bet != null ? bet.name : null;
	}

	public void SetCurrentCredits(int c)
	{
		credits = c;
	}

	// --- Inventory operations convenience wrappers ---
	public void AddInventoryItem(InventoryItemData item)
	{
		if (item == null) return;
		if (inventory == null) inventory = new PlayerInventory();

		// Special handling: if adding a ReelStrip item created from a definition, create a runtime
		// ReelStripData instance and register it, then add symbol inventory items associated to this strip.
		try
		{
			if (item.ItemType == InventoryItemType.ReelStrip && item.DefinitionAccessorId != 0)
			{
				// If this item already references a runtime accessor id, nothing special to do here
			}

			// New: special handling for Reel inventory items – create a runtime ReelData from a ReelDefinition
			if (item.ItemType == InventoryItemType.Reel && item.DefinitionAccessorId != 0)
			{
				// If definitionAccessorId is used to store a ReelDefinition accessor, this path is not typical.
				// Keep behavior minimal: skip special handling.
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"PlayerData.AddInventoryItem: failed to perform reelstrip/reel association: {ex.Message}");
		}

		// finally add the reelstrip (or generic) item to inventory
		inventory.AddItem(item);
	}
	public bool RemoveInventoryItem(InventoryItemData item)
	{
		if (inventory == null) return false;
		bool removed = inventory.RemoveItem(item);
		return removed;
	}
	public List<InventoryItemData> GetItemsOfType(InventoryItemType type)
	{
		if (inventory == null) return new List<InventoryItemData>();
		return inventory.GetItemsOfType(type);
	}

	/// <summary>
	/// Register an already-created runtime ReelStripData and its authoring symbols into the player's inventory.
	/// This is intended for runtime-created strips (not definition-based), and will add a ReelStrip inventory
	/// item with DefinitionAccessorId set to the strip's AccessorId, plus Symbol inventory items associated to that id.
	/// </summary>
	public void RegisterRuntimeReelStrip(ReelStripData strip, string displayName = null)
	{
		if (strip == null) return;
		if (inventory == null) inventory = new PlayerInventory();

		try
		{
			// Ensure the strip is managed/persisted so other systems can find it by AccessorId
			if (ReelStripDataManager.Instance != null && (strip.AccessorId == 0 || ReelStripDataManager.Instance.ReadOnlyLocalData == null || !ReelStripDataManager.Instance.ReadOnlyLocalData.ContainsKey(strip.AccessorId)))
			{
				ReelStripDataManager.Instance.AddNewData(strip);
			}

			string stripDisplay = !string.IsNullOrEmpty(displayName) ? displayName : $"ReelStrip {strip.AccessorId}";
			var stripItem = new InventoryItemData(stripDisplay, InventoryItemType.ReelStrip, strip.AccessorId);
			inventory.AddItem(stripItem);

			// Prefer existing runtime symbol instances on the strip so inventory items reference the canonical persisted SymbolData.
			var runtimeList = strip.RuntimeSymbols;
			if (runtimeList != null && runtimeList.Count > 0)
			{
				for (int i = 0; i < runtimeList.Count; i++)
				{
					var sym = runtimeList[i];
					if (sym == null) continue;

					// Ensure this runtime symbol is registered with the SymbolDataManager so it has an AccessorId
					if (SymbolDataManager.Instance != null && sym.AccessorId == 0)
					{
						SymbolDataManager.Instance.AddNewData(sym);
					}

					var name = !string.IsNullOrEmpty(sym.Name) ? sym.Name : (sym.SpriteKey ?? "<unnamed>");
					var symItem = new InventoryItemData(name, InventoryItemType.Symbol, strip.AccessorId);
					if (!string.IsNullOrEmpty(sym.SpriteKey)) symItem.SetSpriteKey(sym.SpriteKey);
					if (sym.AccessorId > 0) symItem.SetSymbolAccessorId(sym.AccessorId);
					inventory.AddItem(symItem);
				}
			}
			else
			{
				// Create symbol items from authoring definitions
				var defs = strip.SymbolDefinitions;
				if (defs != null)
				{
					foreach (var sdef in defs)
					{
						if (sdef == null) continue;
						SymbolData sym = null;
						try
						{
							sym = sdef.CreateInstance();
							if (SymbolDataManager.Instance != null) SymbolDataManager.Instance.AddNewData(sym);
						}
						catch { sym = null; }

						string name = !string.IsNullOrEmpty(sdef.SymbolName) ? sdef.SymbolName : sdef.name;
						var symItem = new InventoryItemData(name, InventoryItemType.Symbol, strip.AccessorId);
						if (sym != null)
						{
							if (!string.IsNullOrEmpty(sym.SpriteKey)) symItem.SetSpriteKey(sym.SpriteKey);
							symItem.SetSymbolAccessorId(sym.AccessorId);
						}
						inventory.AddItem(symItem);
					}
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"PlayerData.RegisterRuntimeReelStrip: failed to register runtime reelstrip inventory: {ex.Message}");
		}
	}
}
