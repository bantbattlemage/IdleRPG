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
			if (item.ItemType == InventoryItemType.ReelStrip && !string.IsNullOrEmpty(item.DefinitionKey))
			{
				// Try resolve as a ReelStripDefinition first (definition-based create)
				var stripDef = DefinitionResolver.Resolve<ReelStripDefinition>(item.DefinitionKey);
				if (stripDef != null)
				{
					// create runtime instance and register
					var runtimeStrip = stripDef.CreateInstance();
					ReelStripDataManager.Instance?.AddNewData(runtimeStrip);

					// Update the inventory item's definition key to reference the runtime strip instance key
					if (!string.IsNullOrEmpty(runtimeStrip.InstanceKey))
					{
						item.SetDefinitionKey(runtimeStrip.InstanceKey);
					}

					// For each symbol definition in the strip, create runtime SymbolData and add symbol inventory items
					var symbolDefs = runtimeStrip.SymbolDefinitions ?? stripDef.Symbols;
					if (symbolDefs != null && symbolDefs.Length > 0)
					{
						foreach (var sdef in symbolDefs)
						{
							if (sdef == null) continue;
							// create runtime symbol from definition and register
							SymbolData symData = null;
							try
							{
								symData = sdef.CreateInstance();
								if (SymbolDataManager.Instance != null) SymbolDataManager.Instance.AddNewData(symData);
							}
							catch (Exception)
							{
								// fall back to skipping symbol if creation fails
								symData = null;
							}

							// create inventory item for this symbol and associate it with the reelstrip via the runtime strip's InstanceKey
							string displayName = !string.IsNullOrEmpty(sdef.SymbolName) ? sdef.SymbolName : sdef.name;
							var symItem = new InventoryItemData(displayName, InventoryItemType.Symbol, runtimeStrip.InstanceKey);
							if (symData != null)
							{
								// ensure sprite key and accessor id are set so UI can resolve visuals
								if (!string.IsNullOrEmpty(symData.SpriteKey)) symItem.SetSpriteKey(symData.SpriteKey);
								symItem.SetSymbolAccessorId(symData.AccessorId);
							}
							inventory.AddItem(symItem);
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"PlayerData.AddInventoryItem: failed to perform reelstrip-symbol association: {ex.Message}");
		}

		// finally add the reelstrip (or generic) item to inventory
		inventory.AddItem(item);
	}
	public bool RemoveInventoryItem(InventoryItemData item)
	{
		if (inventory == null) return false;
		return inventory.RemoveItem(item);
	}
	public List<InventoryItemData> GetItemsOfType(InventoryItemType type)
	{
		if (inventory == null) return new List<InventoryItemData>();
		return inventory.GetItemsOfType(type);
	}
}
