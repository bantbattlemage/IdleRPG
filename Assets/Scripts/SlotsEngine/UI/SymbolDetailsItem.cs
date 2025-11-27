using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SymbolDetailsItem : MonoBehaviour
{
	public TMP_Text NameText;
	public TMP_Text MetaText;
	public Button AddButton;
	public Button RemoveButton;

	private InventoryItemData boundItem;
	private System.Action<InventoryItemData> onRemoveCallback;
	private System.Action<InventoryItemData> onAddCallback;

	public void Setup(InventoryItemData item, System.Action<InventoryItemData> onAdd, System.Action<InventoryItemData> onRemove, bool allowAdd, bool allowRemove)
	{
		boundItem = item;
		onAddCallback = onAdd;
		onRemoveCallback = onRemove;
		if (NameText != null) NameText.text = item != null ? item.DisplayName : "(null)";
		if (MetaText != null) MetaText.text = item != null ? item.ItemType.ToString() : "";

		if (AddButton != null)
		{
			AddButton.onClick.RemoveAllListeners();
			AddButton.onClick.AddListener(OnAddClicked);
			AddButton.gameObject.SetActive(allowAdd);
		}

		if (RemoveButton != null)
		{
			RemoveButton.onClick.RemoveAllListeners();
			RemoveButton.onClick.AddListener(OnRemoveClicked);
			RemoveButton.gameObject.SetActive(allowRemove);
		}

		// when both actions are disabled, visually dim and disable interaction
		var bothDisabled = !allowAdd && !allowRemove;
		var cg = GetComponent<CanvasGroup>();
		if (cg == null && bothDisabled)
		{
			cg = gameObject.AddComponent<CanvasGroup>();
		}
		if (cg != null)
		{
			cg.interactable = !bothDisabled;
			cg.blocksRaycasts = !bothDisabled;
			cg.alpha = bothDisabled ? 0.6f : 1f;
		}
	}

	private void OnRemoveClicked()
	{
		onRemoveCallback?.Invoke(boundItem);
	}

	private void OnAddClicked()
	{
		onAddCallback?.Invoke(boundItem);
	}
}
