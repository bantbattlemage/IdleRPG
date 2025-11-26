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
		if (inventory == null) inventory = new PlayerInventory();
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
