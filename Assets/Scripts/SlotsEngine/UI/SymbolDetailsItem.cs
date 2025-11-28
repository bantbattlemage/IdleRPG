using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SymbolDetailsItem : MonoBehaviour
{
	public TMP_Text NameText;
	public TMP_Text MetaText;
	public Button AddButton;
	public Button RemoveButton;
	public Button TransferButton;

	private InventoryItemData boundItem;
	private System.Action<InventoryItemData> onRemoveCallback;
	private System.Action<InventoryItemData> onAddCallback;
	private System.Action<InventoryItemData> onTransferCallback;

	public void Setup(InventoryItemData item, System.Action<InventoryItemData> onAdd, System.Action<InventoryItemData> onRemove, bool allowAdd, bool allowRemove, System.Action<InventoryItemData> onTransfer = null, bool allowTransfer = false)
	{
		boundItem = item;
		onAddCallback = onAdd;
		onRemoveCallback = onRemove;
		onTransferCallback = onTransfer;

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

		if (TransferButton != null)
		{
			TransferButton.onClick.RemoveAllListeners();
			TransferButton.onClick.AddListener(OnTransferClicked);
			TransferButton.gameObject.SetActive(allowTransfer);
		}

		// when all actions are disabled, visually dim and disable interaction
		var noneEnabled = !allowAdd && !allowRemove && !allowTransfer;
		var cg = GetComponent<CanvasGroup>();
		if (cg == null && noneEnabled)
		{
			cg = gameObject.AddComponent<CanvasGroup>();
		}
		if (cg != null)
		{
			if (noneEnabled)
			{
				cg.interactable = false;
				cg.blocksRaycasts = false;
				cg.alpha = 0.6f;
			}
			else
			{
				cg.interactable = true;
				cg.blocksRaycasts = true;
				cg.alpha = 1f;
			}
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

	private void OnTransferClicked()
	{
		onTransferCallback?.Invoke(boundItem);
	}
}
