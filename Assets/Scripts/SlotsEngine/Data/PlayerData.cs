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

			// New: special handling for Reel inventory items — create a runtime ReelData from a ReelDefinition
			if (item.ItemType == InventoryItemType.Reel && !string.IsNullOrEmpty(item.DefinitionKey))
			{
				var reelDef = DefinitionResolver.Resolve<ReelDefinition>(item.DefinitionKey);
				if (reelDef != null)
				{
					// Create runtime ReelData using the same path as slot initialization
					var runtimeReel = reelDef.CreateInstance();
					if (runtimeReel != null)
					{
						// Register reel with manager so it receives an AccessorId and its contained symbols are persisted
						ReelDataManager.Instance?.AddNewData(runtimeReel);

						// If the reel has an associated runtime strip, ensure it's registered as well
						if (runtimeReel.CurrentReelStrip != null && ReelStripDataManager.Instance != null)
						{
							if (runtimeReel.CurrentReelStrip.AccessorId == 0)
								ReelStripDataManager.Instance.AddNewData(runtimeReel.CurrentReelStrip);
						}

						// Associate inventory item to this new runtime reel by storing its accessor id as the definition key
						// (use string form; callers can parse back if needed)
						item.SetDefinitionKey(runtimeReel.AccessorId.ToString());
					}
				}
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
		return inventory.RemoveItem(item);
	}
	public List<InventoryItemData> GetItemsOfType(InventoryItemType type)
	{
		if (inventory == null) return new List<InventoryItemData>();
		return inventory.GetItemsOfType(type);
	}

	/// <summary>
	/// Register an already-created runtime ReelStripData and its authoring symbols into the player's inventory.
	/// This is intended for runtime-created strips (not definition-based), and will add a ReelStrip inventory
	/// item with DefinitionKey set to the strip's InstanceKey, plus Symbol inventory items associated to that key.
	/// </summary>
	public void RegisterRuntimeReelStrip(ReelStripData strip, string displayName = null)
	{
		if (strip == null) return;
		if (inventory == null) inventory = new PlayerInventory();

		try
		{
			// Ensure the strip is managed/persisted so other systems can find it by AccessorId/InstanceKey
			if (ReelStripDataManager.Instance != null && (strip.AccessorId == 0 || ReelStripDataManager.Instance.ReadOnlyLocalData == null || !ReelStripDataManager.Instance.ReadOnlyLocalData.ContainsKey(strip.AccessorId)))
			{
				ReelStripDataManager.Instance.AddNewData(strip);
			}

			string stripDisplay = !string.IsNullOrEmpty(displayName) ? displayName : $"ReelStrip {strip.InstanceKey}";
			var stripItem = new InventoryItemData(stripDisplay, InventoryItemType.ReelStrip, strip.InstanceKey);
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
					var symItem = new InventoryItemData(name, InventoryItemType.Symbol, strip.InstanceKey);
					if (!string.IsNullOrEmpty(sym.SpriteKey)) symItem.SetSpriteKey(sym.SpriteKey);
					if (sym.AccessorId > 0) symItem.SetSymbolAccessorId(sym.AccessorId);
					inventory.AddItem(symItem);
				}
			}
			else
			{
				// Fallback: create symbol items from authoring definitions (preserves original behavior)
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
						var symItem = new InventoryItemData(name, InventoryItemType.Symbol, strip.InstanceKey);
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
