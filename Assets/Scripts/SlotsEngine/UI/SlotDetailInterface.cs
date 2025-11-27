using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SlotDetailInterface : MonoBehaviour
{
	public RectTransform SlotDetailsRoot;

	public RectTransform ReelDetailsRoot;
	public ReelDetailItem ReelDetailItemPrefab;
	public Button CloseButton;

	private SlotsData current;

	private void Start()
	{
		if (CloseButton != null)
			CloseButton.onClick.AddListener(OnCloseButtonClicked);

		if (ReelDetailsRoot != null)
		{
			// ensure it's empty initially
			for (int i = ReelDetailsRoot.childCount - 1; i >= 0; i--) Destroy(ReelDetailsRoot.GetChild(i).gameObject);
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
			Destroy(ReelDetailsRoot.GetChild(i).gameObject);
		}

		if (current == null || current.CurrentReelData == null) return;

		var list = current.CurrentReelData;
		for (int i = 0; i < list.Count; i++)
		{
			var rd = list[i];
			var item = Instantiate(ReelDetailItemPrefab, ReelDetailsRoot);
			item.Setup(rd, i);
		}
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
