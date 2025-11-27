using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class InventoryInterface : MonoBehaviour
{
	public RectTransform InventoryRoot;

	public InventoryInterfaceItem ItemPanelPrefab;
	public Button CloseButton;
	public RectTransform ItemPrefabContentRoot;

	public RectTransform ItemDetailsGroup;
	public TMP_Text ItemDetailsNameText;
	public TMP_Text ItemDetailsDescriptionText;
	public Button ItemDetailsBackButton;

	public SlotDetailInterface SlotDetailsInterface; // show slot-specific details

	private void Start()
	{
		if (CloseButton != null)
			CloseButton.onClick.AddListener(OnCloseButtonClicked);

		if (ItemDetailsBackButton != null)
		{
			ItemDetailsBackButton.onClick.RemoveAllListeners();
			ItemDetailsBackButton.onClick.AddListener(OnItemDetailsBack);
		}

		// Ensure details group is hidden initially
		if (ItemDetailsGroup != null)
		{
			ItemDetailsGroup.gameObject.SetActive(false);
		}

		// Ensure slot details are hidden initially and hook close to restore list
		if (SlotDetailsInterface != null)
		{
			if (SlotDetailsInterface.CloseButton != null)
			{
				// ensure no duplicate listener
				SlotDetailsInterface.CloseButton.onClick.RemoveListener(OnSlotDetailsClosed);
				SlotDetailsInterface.CloseButton.onClick.AddListener(OnSlotDetailsClosed);
			}

			// If the slot details interface exposes a root, hide it initially so it behaves like a panel
			if (SlotDetailsInterface.SlotDetailsRoot != null)
			{
				SlotDetailsInterface.SlotDetailsRoot.gameObject.SetActive(false);
			}
		}
	}

	// Removed OnEnable dependency: InventoryInterface may no longer be toggled directly.
	// Call OpenInventory() to show the inventory and refresh its contents.
	public void OpenInventory()
	{
		if (InventoryRoot != null)
		{
			InventoryRoot.gameObject.SetActive(true);
		}
		else
		{
			// fallback for older setups where this component lived on the UI root
			gameObject.SetActive(true);
		}

		Refresh();
	}

	public void Refresh()
	{
		if (ItemPanelPrefab == null || ItemPrefabContentRoot == null) return;

		var pd = GamePlayer.Instance?.PlayerData;
		var items = pd?.Inventory?.Items as IList<InventoryItemData>;
		// Clear existing children
		for (int i = ItemPrefabContentRoot.childCount - 1; i >= 0; i--)
		{
			Destroy(ItemPrefabContentRoot.GetChild(i).gameObject);
		}

		if (items == null) return;

		foreach (var it in items)
		{
			// create a local copy so the lambda captures the correct item
			var itemData = it;
			var itemUI = Instantiate(ItemPanelPrefab, ItemPrefabContentRoot);
			itemUI.Setup(itemData, () =>
			{
				// Remove callback
				if (pd != null)
				{
					var found = pd.Inventory?.FindById(itemData.Id);
					if (found != null) pd.RemoveInventoryItem(found);
					Refresh();
				}
			}, () =>
			{
				// Select / show details
				if (itemData != null && itemData.ItemType == InventoryItemType.SlotEngine && SlotDetailsInterface != null)
				{
					// Try to find matching SlotsData by display name (best-effort)
					var pd2 = GamePlayer.Instance?.PlayerData;
					SlotsData foundSlot = null;
					if (pd2?.CurrentSlots != null)
					{
						string display = itemData.DisplayName;
						foreach (var s in pd2.CurrentSlots)
						{
							if (s == null) continue;
							if (("Slot " + s.Index) == display || s.Index.ToString() == display)
							{
								foundSlot = s;
								break;
							}
						}
					}

					if (foundSlot != null)
					{
						ShowSlotDetails(foundSlot);
						return;
					}
				}

				// fallback to generic item details
				ShowItemDetails(itemData);
			});
		}
	}

	void OnCloseButtonClicked()
	{
		if (InventoryRoot != null)
		{
			InventoryRoot.gameObject.SetActive(false);
		}
		else
		{
			// fallback for older setups
			gameObject.SetActive(false);
		}
	}

	private void OnItemDetailsBack()
	{
		// Hide details and show list
		SetDetailsVisible(false);
	}

	private void ShowItemDetails(InventoryItemData item)
	{
		if (item == null) return;

		if (ItemDetailsNameText != null) ItemDetailsNameText.text = item.DisplayName ?? "(unnamed)";

		// Build a simple description: include type, id, and definition key if present
		string desc = "";
		desc += $"Type: {item.ItemType}\n";
		if (!string.IsNullOrEmpty(item.DefinitionKey)) desc += $"Definition: {item.DefinitionKey}\n";
		desc += $"ID: {item.Id}\n";

		if (ItemDetailsDescriptionText != null) ItemDetailsDescriptionText.text = desc;

		SetDetailsVisible(true);
	}

	private void SetDetailsVisible(bool visible)
	{
		if (ItemDetailsGroup != null)
		{
			ItemDetailsGroup.gameObject.SetActive(visible);
		}

		if (ItemPrefabContentRoot != null)
		{
			ItemPrefabContentRoot.gameObject.SetActive(!visible);
		}
	}

	private void ShowSlotDetails(SlotsData slot)
	{
		if (SlotDetailsInterface == null || slot == null)
		{
			// fallback to generic
			ShowItemDetails(new InventoryItemData($"Slot {slot?.Index}", InventoryItemType.SlotEngine, slot?.BaseDefinition?.name));
			return;
		}

		SlotDetailsInterface.ShowSlot(slot);
		// hide list and generic details while slot details panel is visible
		if (ItemPrefabContentRoot != null) ItemPrefabContentRoot.gameObject.SetActive(false);
		if (ItemDetailsGroup != null) ItemDetailsGroup.gameObject.SetActive(false);
	}

	private void OnSlotDetailsClosed()
	{
		// restore list visibility when slot details closed
		if (ItemPrefabContentRoot != null) ItemPrefabContentRoot.gameObject.SetActive(true);
	}
}
