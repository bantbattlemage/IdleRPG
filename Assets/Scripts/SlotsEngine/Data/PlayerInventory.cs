using System;
using System.Collections.Generic;
using UnityEngine;
using EvaluatorCore;

/// <summary>
/// Distinguishes item categories the player can collect.
/// Extend as more item archetypes as needed.
/// </summary>
public enum InventoryItemType
{
	Symbol,
	Reel,
	ReelStrip,
	SlotEngine
}

[Serializable]
public class InventoryItemData
{
	[SerializeField] private int id;
	[SerializeField] private string displayName;
	[SerializeField] private InventoryItemType itemType;
	// Associate inventory items to runtime data entries by accessor id. 0 == unassociated.
	[SerializeField] private int definitionAccessorId;
	// Optional: explicit sprite key (asset name or addressable key) for resolving visuals.
	[SerializeField] private string spriteKey;
	// Persist optional reference to a SymbolData accessor id so inventory items can keep a stable reference
	[SerializeField] private int symbolAccessorId;

	public int Id => id;
	public string DisplayName => displayName;
	public InventoryItemType ItemType => itemType;
	public int DefinitionAccessorId => definitionAccessorId;
	public string SpriteKey => spriteKey;
	public int SymbolAccessorId => symbolAccessorId;

	public InventoryItemData(string display, InventoryItemType type, int defAccessorId = 0)
	{
		id = GlobalAccessorIdProvider.GetNextId();
		displayName = display;
		itemType = type;
		definitionAccessorId = defAccessorId;
		spriteKey = null;
		symbolAccessorId = 0;
	}

	/// <summary>
	/// Constructor that allows explicitly specifying an asset key for sprite resolution.
	/// If the item is a Symbol and the sprite key resolves to a sprite, create a corresponding SymbolData and
	/// register it with the SymbolDataManager so the inventory item holds a reference to the runtime SymbolData.
	/// </summary>
	public InventoryItemData(string display, InventoryItemType type, int defAccessorId, string spriteKey)
	{
		id = GlobalAccessorIdProvider.GetNextId();
		displayName = display;
		itemType = type;
		definitionAccessorId = defAccessorId;
		this.spriteKey = spriteKey;
		symbolAccessorId = 0;

		if (itemType == InventoryItemType.Symbol && !string.IsNullOrEmpty(this.spriteKey))
		{
			try
			{
				Sprite sprite = AssetResolver.ResolveSprite(this.spriteKey);
				if (sprite != null)
				{
					string symbolNameForData = displayName;
					if (SymbolDefinitionManager.Instance != null)
					{
						if (SymbolDefinitionManager.Instance.TryGetDefinition(this.spriteKey, out var def))
						{
							if (!string.IsNullOrEmpty(def.SymbolName)) symbolNameForData = def.SymbolName;
							else if (!string.IsNullOrEmpty(def.name)) symbolNameForData = def.name;
							if (def.SymbolSprite != null) this.spriteKey = def.SymbolSprite.name; // canonicalize spriteKey
						}
					}

					var symbol = new SymbolData(symbolNameForData, sprite, 0, -1, 1f, PayScaling.DepthSquared, false, true, SymbolWinMode.LineMatch, -1, -1, -1);
					if (SymbolDataManager.Instance != null)
					{
						SymbolDataManager.Instance.AddNewData(symbol);
						symbolAccessorId = symbol.AccessorId;
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"InventoryItemData: failed to create SymbolData for inventory item '{displayName}': {ex.Message}");
			}
		}
	}

	// Update association to a runtime data accessor id and request persistence save.
	public void SetDefinitionAccessorId(int newAccessorId)
	{
		definitionAccessorId = newAccessorId;
		DataPersistenceManager.Instance?.RequestSave();
	}

	public void SetSpriteKey(string key)
	{
		spriteKey = key;
		DataPersistenceManager.Instance?.RequestSave();
	}

	/// <summary>
	/// Associate this inventory item with an existing persisted SymbolData accessor id.
	/// Use when the inventory item should reference an already-registered runtime SymbolData.
	/// </summary>
	public void SetSymbolAccessorId(int accessorId)
	{
		symbolAccessorId = accessorId;
		DataPersistenceManager.Instance?.RequestSave();
	}

	/// <summary>
	/// Allows updating the display name of this inventory item (used to mark items unassociated when their
	/// referenced slot/strip is removed).
	/// </summary>
	public void SetDisplayName(string newDisplayName)
	{
		displayName = newDisplayName;
		DataPersistenceManager.Instance?.RequestSave();
	}
}

[Serializable]
public class PlayerInventory
{
	[SerializeField] private List<InventoryItemData> items = new List<InventoryItemData>();
	public IReadOnlyList<InventoryItemData> Items => items;

	public void AddItem(InventoryItemData item)
	{
		if (item == null) return;
		items.Add(item);
		DataPersistenceManager.Instance?.RequestSave();
	}

	public bool RemoveItem(InventoryItemData item)
	{
		bool removed = item != null && items.Remove(item);
		if (removed) DataPersistenceManager.Instance?.RequestSave();
		return removed;
	}

	public List<InventoryItemData> GetItemsOfType(InventoryItemType type)
	{
		return items.FindAll(i => i.ItemType == type);
	}

	public InventoryItemData FindById(int id)
	{
		return items.Find(i => i.Id == id);
	}
}
