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

		// Diagnostic: log counts and basic info to help reproduce missing reels
		Debug.Log($"[AddReelInterface] Refresh called. allReels.Count={allReels.Count}, allSlots.Count={allSlots.Count}, targetSlotIndex={current.Index}, targetSlotAccessor={current.AccessorId}");
		for (int r = 0; r < allReels.Count; r++)
		{
			var rr = allReels[r];
			if (rr == null) { Debug.Log($"[AddReelInterface] Reel[{r}] is null"); continue; }
			var rs = rr.CurrentReelStrip;
			Debug.Log($"[AddReelInterface] Reel[{r}] accessor={rr.AccessorId} symbolCount={rr.CurrentSymbolData?.Count ?? 0} stripAccessor={rs?.AccessorId ?? 0}");
		}

		int shownIndex = 0;
		for (int i = 0; i < allReels.Count; i++)
		{
			var rd = allReels[i];
			if (rd == null) continue;
			if (IsAssociatedWithAnySlot(rd, allSlots))
			{
				Debug.Log($"[AddReelInterface] Skipping Reel accessor={rd.AccessorId} because it is associated with a slot.");
				continue; // only show unassociated reels
			}

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

		// Helper to get canonical strip from manager when available
		ReelStripData GetCanonicalStrip(ReelStripData s)
		{
			if (s == null || ReelStripDataManager.Instance == null) return s;
			// Try by accessor id
			if (s.AccessorId > 0 && ReelStripDataManager.Instance.ReadOnlyLocalData != null && ReelStripDataManager.Instance.ReadOnlyLocalData.ContainsKey(s.AccessorId))
				return ReelStripDataManager.Instance.ReadOnlyLocalData[s.AccessorId];
			return s;
		}

		var reelStripCanonical = GetCanonicalStrip(reel.CurrentReelStrip);

		for (int i = 0; i < slots.Count; i++)
		{
			var s = slots[i];
			if (s == null || s.CurrentReelData == null) continue;
			for (int j = 0; j < s.CurrentReelData.Count; j++)
			{
				var rd = s.CurrentReelData[j];
				if (rd == null) continue;

				// match by accessor id on ReelData
				if (reel.AccessorId > 0 && rd.AccessorId == reel.AccessorId)
				{
					Debug.Log($"[AddReelInterface] Reel accessor={reel.AccessorId} is associated with Slot accessor={s.AccessorId} by Reel.AccessorId match.");
					return true;
				}

				// canonicalize both reel strips using manager data when possible
				var rdStripCanonical = GetCanonicalStrip(rd.CurrentReelStrip);
				if (reelStripCanonical != null && rdStripCanonical != null)
				{
					if (reelStripCanonical.AccessorId > 0 && rdStripCanonical.AccessorId == reelStripCanonical.AccessorId)
					{
						Debug.Log($"[AddReelInterface] Reel accessor={reel.AccessorId} is associated with Slot accessor={s.AccessorId} by canonical strip AccessorId match ({reelStripCanonical.AccessorId}).");
						return true;
					}
				}

				// fallback to reference equality
				if (ReferenceEquals(rd, reel))
				{
					Debug.Log($"[AddReelInterface] Reel accessor={reel.AccessorId} is associated with Slot accessor={s.AccessorId} by reference equality.");
					return true;
				}
			}
		}
		return false;
	}

	private void OnCloseButtonClicked()
	{
		if (AddReelRoot != null) AddReelRoot.gameObject.SetActive(false); else gameObject.SetActive(false);
	}
}
