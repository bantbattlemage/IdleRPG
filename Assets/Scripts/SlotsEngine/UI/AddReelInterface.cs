using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class AddReelInterface : MonoBehaviour
{
	public RectTransform AddReelRoot;

	public RectTransform ReelDetailsRoot;
	public ReelDetailItem ReelDetailItemPrefab;
	public Button CloseButton;

	private SlotsData current;

	private void Start()
	{
		if (CloseButton != null)
			CloseButton.onClick.AddListener(OnCloseButtonClicked);

		// ensure empty initially
		if (ReelDetailsRoot != null)
		{
			for (int i = ReelDetailsRoot.childCount - 1; i >= 0; i--)
			{
				Destroy(ReelDetailsRoot.GetChild(i).gameObject);
			}
		}
	}

	public void ShowSlot(SlotsData slots)
	{
		if (AddReelRoot != null) AddReelRoot.gameObject.SetActive(true); else gameObject.SetActive(true);
		current = slots;
		Refresh();

		Canvas.ForceUpdateCanvases();
		if (ReelDetailsRoot != null) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(ReelDetailsRoot);
	}

	public void Refresh()
	{
		if (ReelDetailsRoot == null || ReelDetailItemPrefab == null) return;

		// clear existing children
		for (int i = ReelDetailsRoot.childCount - 1; i >= 0; i--)
		{
			Destroy(ReelDetailsRoot.GetChild(i).gameObject);
		}

		if (current == null) return;

		var allReels = ReelDataManager.Instance != null ? ReelDataManager.Instance.GetAllData() : new List<ReelData>();
		var allSlots = SlotsDataManager.Instance != null ? SlotsDataManager.Instance.GetAllData() : new List<SlotsData>();

		int shownIndex = 0;
		for (int i = 0; i < allReels.Count; i++)
		{
			var rd = allReels[i];
			if (rd == null) continue;
			if (IsAssociatedWithAnySlot(rd, allSlots)) continue; // only show unassociated reels

			var item = Instantiate(ReelDetailItemPrefab, ReelDetailsRoot);
			item.Setup(rd, shownIndex);

			// hide remove button and disable symbol menus
			if (item.RemoveReelButton != null) item.RemoveReelButton.gameObject.SetActive(false);
			item.DisableSymbolMenus();

			// wire up add behavior: add this reel to the current slot
			item.AddReelButton?.gameObject.SetActive(true);
			item.ConfigureAddition(current, () => {
				// refresh UI after adding so the added reel disappears from the available list
				Refresh();
			});

			shownIndex++;
		}
	}

	private bool IsAssociatedWithAnySlot(ReelData reel, List<SlotsData> slots)
	{
		if (reel == null) return false;
		if (slots == null) return false;
		for (int i = 0; i < slots.Count; i++)
		{
			var s = slots[i];
			if (s == null || s.CurrentReelData == null) continue;
			for (int j = 0; j < s.CurrentReelData.Count; j++)
			{
				var rd = s.CurrentReelData[j];
				if (rd == null) continue;

				// match by accessor id
				if (reel.AccessorId > 0 && rd.AccessorId == reel.AccessorId) return true;

				// match by associated strip instance key when available
				var rStrip = reel.CurrentReelStrip;
				var rdStrip = rd.CurrentReelStrip;
				if (rStrip != null && rdStrip != null && !string.IsNullOrEmpty(rStrip.InstanceKey) && rStrip.InstanceKey == rdStrip.InstanceKey) return true;

				// fallback to reference equality
				if (ReferenceEquals(rd, reel)) return true;
			}
		}
		return false;
	}

	private void OnCloseButtonClicked()
	{
		if (AddReelRoot != null) AddReelRoot.gameObject.SetActive(false); else gameObject.SetActive(false);
	}
}
