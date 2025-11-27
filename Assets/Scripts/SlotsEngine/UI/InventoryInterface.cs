using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class InventoryInterface : MonoBehaviour
{
	public InventoryInterfaceItem ItemPanelPrefab;
	public Button CloseButton;
	public RectTransform ItemPrefabContentRoot;

	public RectTransform ItemDetailsGroup;
	public TMP_Text ItemDetailsNameText;
	public TMP_Text ItemDetailsDescriptionText;
	public Button ItemDetailsBackButton;

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
	}

	private void OnEnable()
	{
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
				ShowItemDetails(itemData);
			});
		}
	}

	void OnCloseButtonClicked()
	{
		gameObject.SetActive(false);
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
}
