using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Distinguishes item categories the player can collect.
/// Extend as more item archetypes are needed.
/// </summary>
public enum InventoryItemType
{
	Symbol,
	Reel,
	ReelStrip,
	SlotEngine
}

/// <summary>
/// Base runtime data for an inventory item owned by the player.
/// Specific sub-types can inherit from this for richer metadata.
/// </summary>
[Serializable]
public class InventoryItemData
{
	[SerializeField] private string id; // unique runtime id
	[SerializeField] private string displayName;
	[SerializeField] private InventoryItemType itemType;
	[SerializeField] private string definitionKey; // link back to authoring definition if applicable

	public string Id => id;
	public string DisplayName => displayName;
	public InventoryItemType ItemType => itemType;
	public string DefinitionKey => definitionKey;

	public InventoryItemData(string display, InventoryItemType type, string defKey)
	{
		id = Guid.NewGuid().ToString();
		displayName = display;
		itemType = type;
		definitionKey = defKey;
	}
}

/// <summary>
/// Player inventory container. Provides minimal management and lookup utilities.
/// </summary>
[Serializable]
public class PlayerInventory
{
	[SerializeField] private List<InventoryItemData> items = new List<InventoryItemData>();
	public IReadOnlyList<InventoryItemData> Items => items;

	public void AddItem(InventoryItemData item)
	{
		if (item == null) return;
		items.Add(item);
	}

	public bool RemoveItem(InventoryItemData item)
	{
		return item != null && items.Remove(item);
	}

	public List<InventoryItemData> GetItemsOfType(InventoryItemType type)
	{
		return items.FindAll(i => i.ItemType == type);
	}

	public InventoryItemData FindById(string id)
	{
		return items.Find(i => i.Id == id);
	}
}
