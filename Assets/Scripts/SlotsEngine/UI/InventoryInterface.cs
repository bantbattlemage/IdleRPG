using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class InventoryInterface : MonoBehaviour
{
	public InventoryInterfaceItem ItemPanelPrefab;
	public Button CloseButton;
	public RectTransform ItemPrefabContentRoot;

	private void Start()
	{
		if (CloseButton != null)
			CloseButton.onClick.AddListener(OnCloseButtonClicked);
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
			});
		}
	}

	void OnCloseButtonClicked()
	{
		gameObject.SetActive(false);
	}
}
