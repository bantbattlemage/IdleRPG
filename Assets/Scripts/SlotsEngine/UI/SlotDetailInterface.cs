using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SlotDetailInterface : MonoBehaviour
{
	public RectTransform SlotDetailsRoot;

	public RectTransform ReelDetailsRoot;
	public ReelDetailItem ReelDetailItemPrefab;
	public Button CloseButton;
	public Button AddReelButton;

	// AddRemoveSymbolsMenu reference (prefab or scene instance)
	public AddRemoveSymbolsMenu AddRemoveSymbolsMenuInstance;

	private SlotsData current;

	private void Start()
	{
		if (CloseButton != null)
			CloseButton.onClick.AddListener(OnCloseButtonClicked);

		if (ReelDetailsRoot != null)
		{
			// ensure it's empty initially
			for (int i = ReelDetailsRoot.childCount - 1; i >= 0; i--) 
			{
				var child = ReelDetailsRoot.GetChild(i);
				// Preserve AddReelButton (or its parent container) if it's under ReelDetailsRoot
				if (AddReelButton != null)
				{
					if (child == AddReelButton.transform || AddReelButton.transform.IsChildOf(child))
						continue;
				}

				Destroy(child.gameObject);
			}

			// Ensure AddReelButton (if present under ReelDetailsRoot) is last sibling
			if (AddReelButton != null && AddReelButton.transform.parent == ReelDetailsRoot)
			{
				AddReelButton.transform.SetAsLastSibling();
			}
		}
	}

	public void ShowSlot(SlotsData slots)
	{
		// Activate before populating so instantiated UI receives proper layout and OnEnable events
		if (SlotDetailsRoot != null)
		{
			SlotDetailsRoot.gameObject.SetActive(true);
		}
		else
		{
			// fallback to activating the component GameObject for older setups
			gameObject.SetActive(true);
		}

		current = slots;
		Refresh();

		// Force layout rebuild to ensure first-time visuals are correct
		Canvas.ForceUpdateCanvases();
		if (ReelDetailsRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(ReelDetailsRoot);
	}

	public void Refresh()
	{
		if (ReelDetailsRoot == null || ReelDetailItemPrefab == null) return;

		// clear existing children
		for (int i = ReelDetailsRoot.childCount - 1; i >= 0; i--)
		{
			var child = ReelDetailsRoot.GetChild(i);
			// Preserve AddReelButton (or its parent container) if it's under ReelDetailsRoot
			if (AddReelButton != null)
			{
				if (child == AddReelButton.transform || AddReelButton.transform.IsChildOf(child))
					continue;
			}

			Destroy(child.gameObject);
		}

		if (current == null || current.CurrentReelData == null) return;

		var list = current.CurrentReelData;
		for (int i = 0; i < list.Count; i++)
		{
			var rd = list[i];
			var item = Instantiate(ReelDetailItemPrefab, ReelDetailsRoot);
			item.Setup(rd, i);

			// wire up each reel's symbol menu buttons to open our AddRemoveSymbolsMenu
			item.ConfigureSymbolMenu(current, OpenAddRemoveSymbolsMenu);
		}

		// Ensure AddReelButton remains the last child in its parent (typically ReelDetailsRoot)
		if (AddReelButton != null && AddReelButton.transform.parent == ReelDetailsRoot)
		{
			AddReelButton.transform.SetAsLastSibling();
		}
	}

	private void OpenAddRemoveSymbolsMenu(ReelStripData strip, SlotsData slot)
	{
		if (AddRemoveSymbolsMenuInstance == null)
		{
			Debug.LogWarning("AddRemoveSymbolsMenuInstance is not assigned on SlotDetailInterface.");
			return;
		}

		// Optionally pass context to the menu. Currently the menu reads PlayerData inventory; this
		// provides a hook if you want the menu to pre-select symbols from the provided strip or slot.
		AddRemoveSymbolsMenuInstance.Show();
		// future: AddRemoveSymbolsMenuInstance.SetContext(strip, slot);
	}

	private void OnCloseButtonClicked()
	{
		if (SlotDetailsRoot != null)
		{
			SlotDetailsRoot.gameObject.SetActive(false);
		}
		else
		{
			SlotDetailsRoot?.gameObject.SetActive(false); // no-op if null, kept for clarity
			// fallback: hide component GameObject if no separate root
			gameObject.SetActive(false);
		}
	}
}
