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

	// reference to the AddReelInterface UI
	public AddReelInterface AddReelInterfaceInstance;

	// AddRemoveSymbolsMenu reference (prefab or scene instance)
	public AddRemoveSymbolsMenu AddRemoveSymbolsMenuInstance;

	private SlotsData current;

	private int lastShownSlotAccessor = -1;
	private SlotsData lastShownSlotRef = null;
	private int lastShownSlotIndex = -1;
	private bool refreshInProgress = false;

	private void Start()
	{
		if (CloseButton != null)
			CloseButton.onClick.AddListener(OnCloseButtonClicked);

		if (AddReelButton != null)
		{
			AddReelButton.onClick.RemoveAllListeners();
			AddReelButton.onClick.AddListener(OnAddReelButtonClicked);
		}

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

		// Subscribe to global reel-strip updates so UI can refresh when strips change elsewhere
		GlobalEventManager.Instance?.RegisterEvent(SlotsEvent.ReelStripUpdated, OnReelStripUpdated);

		// Listen for slot-level updates (reused the ReelAdded global broadcast when SlotsDataManager updates a slot)
		GlobalEventManager.Instance?.RegisterEvent(SlotsEvent.ReelAdded, OnSlotsDataUpdated);
	}

	private void OnDestroy()
	{
		// Unregister to avoid dangling references
		GlobalEventManager.Instance?.UnregisterEvent(SlotsEvent.ReelStripUpdated, OnReelStripUpdated);
		GlobalEventManager.Instance?.UnregisterEvent(SlotsEvent.ReelAdded, OnSlotsDataUpdated);
	}

	public void ShowSlot(SlotsData slots)
	{
		// If already showing the same slot and UI is visible, avoid redundant work
		if (slots != null && SlotDetailsRoot != null && SlotDetailsRoot.gameObject.activeSelf)
		{
			// Consider accessor id when available, otherwise fall back to index or reference equality to
			// distinguish distinct SlotsData instances. Index is used as a stable local identity
			// that is unique per slot and avoids collisions when AccessorId may be shared/zero.
			if ((slots.AccessorId > 0 && lastShownSlotAccessor == slots.AccessorId)
				|| (lastShownSlotIndex >= 0 && slots.Index == lastShownSlotIndex)
				|| ReferenceEquals(lastShownSlotRef, slots))
			{
				// minor optimization: skip duplicate show requests
				return;
			}
		}

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
		lastShownSlotRef = current;
		lastShownSlotAccessor = current != null ? current.AccessorId : -1;
		lastShownSlotIndex = current != null ? current.Index : -1;
		Refresh();

		// Force layout rebuild to ensure first-time visuals are correct
		Canvas.ForceUpdateCanvases();
		if (ReelDetailsRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(ReelDetailsRoot);
	}

	public void Refresh()
	{
		if (refreshInProgress) return;
		refreshInProgress = true;

		try
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

				// wire up each reel's symbol menu to open our AddRemoveSymbolsMenu
				item.ConfigureSymbolMenu(current, OpenAddRemoveSymbolsMenu);

				// Determine if remove should be allowed (do not allow removing the last reel)
				bool allowRemove = list.Count > 1;

				// wire up remove button to detach this reel from the slot and refresh
				item.ConfigureRemoval(current, () => {
					// refresh UI after removal
					Refresh();
				}, allowRemove);
			}

			// Ensure AddReelButton remains the last child in its parent (typically ReelDetailsRoot)
			if (AddReelButton != null && AddReelButton.transform.parent == ReelDetailsRoot)
			{
				AddReelButton.transform.SetAsLastSibling();
			}
		}
		finally
		{
			refreshInProgress = false;
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
		AddRemoveSymbolsMenuInstance.Show(strip, slot);
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

		public void OnDataReloaded()
		{
			if (current != null) Refresh();
		}

		private void OnReelStripUpdated(object obj)
		{
			var updated = obj as ReelStripData;
			if (updated == null) return;
			if (current == null || current.CurrentReelData == null) return;

			// If any reel in the current slot references this strip, refresh the UI.
			// IMPORTANT: do NOT assign the manager's canonical strip instance into the reel's data here
			// because that would create shared strip instances between slots. Only refresh the UI so the
			// currently displayed strip shows the latest runtime symbols. Slot-level ownership must remain
			// with the slot's ReelData instance (no automatic cross-slot assignment).
			foreach (var rd in current.CurrentReelData)
			{
				if (rd == null) continue;
				var strip = rd.CurrentReelStrip;
				if (strip == null) continue;
				// Match by accessor id; if matched, refresh UI but do NOT mutate rd
				if (strip.AccessorId == updated.AccessorId)
				{
					try
					{
						// Previously we called rd.SetReelStrip(updated) here which caused manager instances to be
						// injected into multiple ReelData objects and produced shared state across slots. Avoid that.
						Refresh();
					}
					catch { }
					break;
				}
			}
		}

		private void OnAddReelButtonClicked()
		{
			if (AddReelInterfaceInstance == null)
			{
				Debug.LogWarning("AddReelInterfaceInstance is not assigned on SlotDetailInterface.");
				return;
			}

			if (current == null)
			{
				Debug.LogWarning("SlotDetailInterface: no current slot selected when invoking AddReel.");
				return;
			}

			AddReelInterfaceInstance.ShowSlot(current);
		}

    private void OnSlotsDataUpdated(object obj)
    {
        var updated = obj as SlotsData;
        if (updated == null) return;
        if (current == null) return;

        // If this update targets our currently shown slot (by reference or accessor id), refresh
        if (ReferenceEquals(updated, current) || (updated.AccessorId > 0 && current.AccessorId > 0 && updated.AccessorId == current.AccessorId))
        {
            // Update our local reference and refresh UI
            current = updated;
            Refresh();
        }
    }
}
