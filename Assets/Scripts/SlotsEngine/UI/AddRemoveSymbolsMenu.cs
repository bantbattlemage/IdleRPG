using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple inventory menu to display player's collected Symbol inventory items.
/// Instantiates `SymbolDetailsItem` prefabs under `SymbolListRoot`.
/// Minimal add/remove functionality is provided for testing and UI wiring.
/// </summary>
public class AddRemoveSymbolsMenu : MonoBehaviour
{
	public RectTransform MenuRoot;
	public RectTransform SymbolListRoot;
	public SymbolDetailsItem SymbolDetailsItemPrefab;
	public Button CloseButton;
	public Button AddSymbolButton;

	private ReelStripData currentStrip;
	private SlotsData currentSlot;

	private void Start()
	{
		if (CloseButton != null) CloseButton.onClick.AddListener(OnCloseClicked);
		if (AddSymbolButton != null) AddSymbolButton.onClick.AddListener(OnAddSymbolClicked);

		// ensure list is empty initially
		if (SymbolListRoot != null)
		{
			for (int i = SymbolListRoot.childCount - 1; i >= 0; i--)
			{
				var child = SymbolListRoot.GetChild(i);
				if (AddSymbolButton != null)
				{
					if (child == AddSymbolButton.transform || AddSymbolButton.transform.IsChildOf(child))
						continue;
				}

				Destroy(child.gameObject);
			}

			if (AddSymbolButton != null && AddSymbolButton.transform.parent == SymbolListRoot)
			{
				AddSymbolButton.transform.SetAsLastSibling();
			}
		}
	}

	/// <summary>
	/// Show the menu with a specific reel-strip and slot context.
	/// </summary>
	public void Show(ReelStripData strip, SlotsData slot)
	{
		currentStrip = strip;
		currentSlot = slot;

		if (MenuRoot != null) MenuRoot.gameObject.SetActive(true);
		else gameObject.SetActive(true);

		Refresh();

		Canvas.ForceUpdateCanvases();
		if (SymbolListRoot != null) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(SymbolListRoot);
	}

	public void Refresh()
	{
		if (SymbolListRoot == null || SymbolDetailsItemPrefab == null) return;

		// clear existing children but preserve AddSymbolButton if it's a child
		for (int i = SymbolListRoot.childCount - 1; i >= 0; i--)
		{
			var child = SymbolListRoot.GetChild(i);
			if (AddSymbolButton != null)
			{
				if (child == AddSymbolButton.transform || AddSymbolButton.transform.IsChildOf(child))
					continue;
			}
			Destroy(child.gameObject);
		}

		var player = GamePlayer.Instance?.PlayerData;
		if (player == null) return;

		var symbols = player.GetItemsOfType(InventoryItemType.Symbol) ?? new List<InventoryItemData>();

		// categorize
		var associatedThis = new List<InventoryItemData>();
		var unassociated = new List<InventoryItemData>();
		var associatedOther = new List<InventoryItemData>();

		string thisKey = currentStrip != null && currentStrip.Definition != null ? currentStrip.Definition.name : null;

		for (int i = 0; i < symbols.Count; i++)
		{
			var s = symbols[i];
			if (s == null) continue;
			var defKey = s.DefinitionKey;
			if (!string.IsNullOrEmpty(defKey) && !string.IsNullOrEmpty(thisKey) && defKey == thisKey)
			{
				associatedThis.Add(s);
			}
			else if (string.IsNullOrEmpty(defKey))
			{
				unassociated.Add(s);
			}
			else
			{
				associatedOther.Add(s);
			}
		}

		// show associated with this strip first (Remove only)
		for (int i = 0; i < associatedThis.Count; i++)
		{
			var itm = Instantiate(SymbolDetailsItemPrefab, SymbolListRoot);
			itm.Setup(associatedThis[i], OnAddInventoryItem, OnRemoveInventoryItem, allowAdd: false, allowRemove: true);
		}

		// then unassociated (Add only)
		for (int i = 0; i < unassociated.Count; i++)
		{
			var itm = Instantiate(SymbolDetailsItemPrefab, SymbolListRoot);
			itm.Setup(unassociated[i], OnAddInventoryItem, OnRemoveInventoryItem, allowAdd: true, allowRemove: false);
		}

		// last, items associated with other strips (both disabled / dimmed)
		for (int i = 0; i < associatedOther.Count; i++)
		{
			var itm = Instantiate(SymbolDetailsItemPrefab, SymbolListRoot);
			itm.Setup(associatedOther[i], OnAddInventoryItem, OnRemoveInventoryItem, allowAdd: false, allowRemove: false);
		}

		if (AddSymbolButton != null && AddSymbolButton.transform.parent == SymbolListRoot)
		{
			AddSymbolButton.transform.SetAsLastSibling();
		}
	}

	private void OnCloseClicked()
	{
		if (MenuRoot != null) MenuRoot.gameObject.SetActive(false);
		else gameObject.SetActive(false);
	}

	private void OnAddSymbolClicked()
	{
		// Minimal test helper: add a simple inventory symbol entry using a default display name.
		var pd = GamePlayer.Instance?.PlayerData;
		if (pd == null) return;

		string newName = "Symbol" + (pd.Inventory?.Items.Count + 1);
		var newItem = new InventoryItemData(newName, InventoryItemType.Symbol, null);
		pd.AddInventoryItem(newItem);
		Refresh();
	}

	private void OnAddInventoryItem(InventoryItemData item)
	{
		if (item == null) return;
		var pd = GamePlayer.Instance?.PlayerData;
		if (pd == null) return;

		// associate with current strip by replacing the inventory item with a new one carrying the strip key
		if (currentStrip != null && currentStrip.Definition != null)
		{
			// remove old
			pd.RemoveInventoryItem(item);
			var newItem = new InventoryItemData(item.DisplayName, InventoryItemType.Symbol, currentStrip.Definition.name);
			pd.AddInventoryItem(newItem);
			// refresh to update ordering and button states
			Refresh();
		}
	}

	private void OnRemoveInventoryItem(InventoryItemData item)
	{
		if (item == null) return;
		var pd = GamePlayer.Instance?.PlayerData;
		if (pd == null) return;
		pd.RemoveInventoryItem(item);
		Refresh();
	}
}
