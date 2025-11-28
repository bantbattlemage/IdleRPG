using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMP = TMPro.TMP_Text;

public class SlotInventoryInterface : InventoryInterfaceBase
{
	public SlotDetailInterface SlotDetailsInterface; // show slot-specific details

	private new void Start()
	{
		// call base to wire up common UI handlers
		base.Start();

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

	protected override void OnItemSelected(InventoryItemData itemData)
	{
		// Slot-specific handling: if item represents a slot, try to show the slot detail panel
		if (itemData != null && itemData.ItemType == InventoryItemType.SlotEngine && SlotDetailsInterface != null)
		{
			var pd2 = GamePlayer.Instance?.PlayerData;
			SlotsData foundSlot = null;
			if (pd2?.CurrentSlots != null)
			{
				// Prefer explicit association via DefinitionKey (stores slot AccessorId)
				if (!string.IsNullOrEmpty(itemData.DefinitionKey))
				{
					if (int.TryParse(itemData.DefinitionKey, out var accessor) && accessor > 0)
					{
						foundSlot = pd2.CurrentSlots.Find(s => s != null && s.AccessorId == accessor);
					}
					// If accessor parsed as 0 or negative, treat as uninitialized/legacy and fall back to display-name matching
				}

				// Fall back to legacy display name matching
				if (foundSlot == null)
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
			}

			if (foundSlot != null)
			{
				ShowSlotDetails(foundSlot);
				return;
			}
		}

		// fallback to generic item details
		base.OnItemSelected(itemData);
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
