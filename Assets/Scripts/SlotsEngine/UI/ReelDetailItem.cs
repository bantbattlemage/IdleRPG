using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ReelDetailItem : MonoBehaviour
{
	public TMP_Text ReelIndexText;
	public RectTransform SymbolDetailsRoot;
	public ReelSymbolDetailItem ReelSymbolDetailItemPrefab;
	public Button RemoveReelButton; // new: button to remove this reel from its slot
	public Button AddReelButton; // added: button to add this reel to a slot

	// cached strip used to configure symbol item menu callbacks after Setup
	private ReelStripData lastStrip;
	private ReelData boundReel;
	private SlotsData boundSlot;

	public void Setup(ReelData data, int index)
	{
		if (ReelIndexText != null) ReelIndexText.text = "Reel " + index.ToString();

		// clear existing children
		if (SymbolDetailsRoot != null)
		{
			for (int i = SymbolDetailsRoot.childCount - 1; i >= 0; i--) Destroy(SymbolDetailsRoot.GetChild(i).gameObject);
		}

		if (data == null) return;

		boundReel = data;

		// Try to use current reel strip; may be null for legacy/empty setups
		var strip = data.CurrentReelStrip;

		// Keep the strip reference owned by the reel; do not attempt string-key fallbacks.

		lastStrip = strip; // cache for later menu wiring

		var defs = strip != null ? strip.SymbolDefinitions : null;
		var runtime = strip != null ? strip.RuntimeSymbols : null;

		int defsLen = defs != null ? defs.Length : 0;
		int runtimeLen = runtime != null ? runtime.Count : 0;

		// Target number of symbol slots: prefer strip.StripSize if available, otherwise fall back to ReelData.SymbolCount
		int target = 1;
		if (strip != null && strip.StripSize > 0) target = strip.StripSize;
		else if (data != null && data.SymbolCount > 0) target = data.SymbolCount;

		// If the runtime list contains more symbols than the defined strip size, expand to show them as well
		if (runtimeLen > target) target = runtimeLen;

		if (ReelSymbolDetailItemPrefab == null)
		{
			Debug.LogWarning($"[ReelDetailItem] Missing ReelSymbolDetailItemPrefab on {name}");
			return;
		}
		if (SymbolDetailsRoot == null)
		{
			Debug.LogWarning($"[ReelDetailItem] Missing SymbolDetailsRoot on {name}");
			return;
		}

		// If a runtime list exists, always use it (show runtime symbols or placeholders). Do NOT fall back to authoring definitions.
		if (runtime != null)
		{
			for (int i = 0; i < target; i++)
			{
				var itm = Instantiate(ReelSymbolDetailItemPrefab, SymbolDetailsRoot);
				if (itm == null) continue;
				itm.gameObject.SetActive(true);
				if (i < runtimeLen && runtime[i] != null)
				{
					itm.Setup(runtime[i], i);
				}
				else
				{
					// Explicit placeholder when runtime list has no symbol at this index
					itm.Setup((SymbolDefinition)null, i);
				}
			}
			Debug.Log($"[ReelDetailItem] Setup boundReelAccessor={boundReel.AccessorId}, stripAccessor={strip?.AccessorId}, runtimeCount={runtimeLen}, target={target}");
		}
		else
		{
			// No runtime list: fall back to authoring definitions where available
			for (int i = 0; i < target; i++)
			{
				var itm = Instantiate(ReelSymbolDetailItemPrefab, SymbolDetailsRoot);
				if (itm == null) continue;
				itm.gameObject.SetActive(true);
				if (i < defsLen)
				{
					itm.Setup(defs[i], i);
				}
				else
				{
					itm.Setup((SymbolDefinition)null, i);
				}
			}
			Debug.LogWarning($"[ReelDetailItem] No runtime symbols for stripAccessor={strip?.AccessorId}. Using authoring definitions. defsLen={defsLen}, target={target}");
		}
	}

	public void ConfigureSymbolMenu(SlotsData slot, System.Action<ReelStripData, SlotsData> onOpen)
	{
		if (SymbolDetailsRoot == null) return;
		for (int i = 0; i < SymbolDetailsRoot.childCount; i++)
		{
			var child = SymbolDetailsRoot.GetChild(i);
			if (child == null) continue;
			var rs = child.GetComponent<ReelSymbolDetailItem>();
			if (rs != null)
			{
				rs.ConfigureMenu(lastStrip, slot, onOpen);
			}
		}
	}

	public void ConfigureRemoval(SlotsData slot, System.Action onRemoved = null, bool allowRemove = true)
	{
		boundSlot = slot;
		if (RemoveReelButton == null) return;

		RemoveReelButton.gameObject.SetActive(true);
		RemoveReelButton.interactable = allowRemove;

		RemoveReelButton.onClick.RemoveAllListeners();
		RemoveReelButton.onClick.AddListener(() =>
		{
			if (boundReel == null || boundSlot == null) return;

			try
			{
				var mgr = SlotsEngineManager.Instance;
				if (mgr != null)
				{
					var engine = mgr.FindEngineForSlotsData(boundSlot);
					if (engine != null)
					{
						if (engine.CurrentState == State.Spinning)
						{
							Debug.LogWarning($"[ReelDetailItem] Remove blocked: engine spinning for slotAccessor={boundSlot.AccessorId}");
							throw new System.InvalidOperationException("Cannot remove reel while engine is spinning.");
						}

						int idx = -1;
						if (engine.CurrentSlotsData != null && engine.CurrentSlotsData.CurrentReelData != null)
						{
							for (int i = 0; i < engine.CurrentSlotsData.CurrentReelData.Count; i++)
							{
								var rd = engine.CurrentSlotsData.CurrentReelData[i];
								if (rd == null) continue;

								if (boundReel.AccessorId > 0 && rd.AccessorId == boundReel.AccessorId) { idx = i; break; }

								if (ReferenceEquals(rd, boundReel)) { idx = i; break; }
							}
						}

						if (idx >= 0 && idx < engine.CurrentReels.Count)
						{
							Debug.Log($"[ReelDetailItem] Removing reel idx={idx} for slotAccessor={boundSlot.AccessorId}, reelAccessor={boundReel.AccessorId}");
							engine.RemoveReel(engine.CurrentReels[idx]);
							SlotsDataManager.Instance?.UpdateSlotsData(engine.CurrentSlotsData);
							onRemoved?.Invoke();
							return;
						}
						else
						{
							Debug.LogWarning($"[ReelDetailItem] Could not map bound reel to engine list for slotAccessor={boundSlot.AccessorId}, reelAccessor={boundReel.AccessorId}");
						}
					}
				}

				// Fallback: no live engine found - operate on data model directly
				boundSlot.RemoveReel(boundReel);
				SlotsDataManager.Instance?.UpdateSlotsData(boundSlot);
				Debug.Log($"[ReelDetailItem] Removed reel via data-only path for slotAccessor={boundSlot.AccessorId}, reelAccessor={boundReel.AccessorId}");
				onRemoved?.Invoke();
			}
			catch (System.Exception ex)
			{
				Debug.LogWarning($"[ReelDetailItem] Failed to remove reel for slotAccessor={boundSlot?.AccessorId}, reelAccessor={boundReel?.AccessorId}: {ex.Message}");
			}
		});
	}

	public void DisableSymbolMenus()
	{
		if (SymbolDetailsRoot == null) return;
		for (int i = 0; i < SymbolDetailsRoot.childCount; i++)
		{
			var child = SymbolDetailsRoot.GetChild(i);
			if (child == null) continue;
			var rs = child.GetComponent<ReelSymbolDetailItem>();
			if (rs != null)
			{
				rs.DisableMenuInteraction();
			}
		}
	}

	public void ConfigureAddition(SlotsData slot, System.Action onAdded = null)
	{
		boundSlot = slot;
		if (AddReelButton == null) return;

		AddReelButton.gameObject.SetActive(true);
		AddReelButton.interactable = true;
		AddReelButton.onClick.RemoveAllListeners();
		AddReelButton.onClick.AddListener(() =>
		{
			if (boundReel == null || boundSlot == null) return;
			try
			{
				var mgr = SlotsEngineManager.Instance;
				if (mgr != null)
				{
					var engine = mgr.FindEngineForSlotsData(boundSlot);
					if (engine != null && engine.CurrentState == State.Spinning)
					{
						Debug.LogWarning($"[ReelDetailItem] Add blocked: engine spinning for slotAccessor={boundSlot.AccessorId}");
						throw new System.InvalidOperationException("Cannot add reel while engine is spinning.");
					}
				}

				boundSlot.AddNewReel(boundReel);

				if (boundReel.AccessorId == 0) ReelDataManager.Instance?.AddNewData(boundReel);
				SlotsDataManager.Instance?.UpdateSlotsData(boundSlot);
				Debug.Log($"[ReelDetailItem] Added reel to slotAccessor={boundSlot.AccessorId}, reelAccessor={boundReel.AccessorId}. dataReels={boundSlot.CurrentReelData?.Count ?? 0}");
				onAdded?.Invoke();
			}
			catch (System.Exception ex)
			{
				Debug.LogWarning($"[ReelDetailItem] Failed to add reel for slotAccessor={boundSlot?.AccessorId}, reelAccessor={boundReel?.AccessorId}: {ex.Message}");
			}
		});
	}
}
