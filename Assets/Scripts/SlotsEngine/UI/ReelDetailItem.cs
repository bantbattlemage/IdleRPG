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

		// Prefer the canonical manager-registered strip if available to ensure we reflect runtime-managed symbols
		if (strip != null && ReelStripDataManager.Instance != null)
		{
			// First try by accessor id
			if (strip.AccessorId > 0 && ReelStripDataManager.Instance.TryGetData(strip.AccessorId, out var canonicalById))
			{
				strip = canonicalById;
			}
			else if (!string.IsNullOrEmpty(strip.InstanceKey))
			{
				// fallback: search manager dictionary for matching instance key
				var all = ReelStripDataManager.Instance.ReadOnlyLocalData;
				if (all != null)
				{
					foreach (var kv in all)
					{
						var s = kv.Value; if (s == null) continue;
						if (!string.IsNullOrEmpty(s.InstanceKey) && s.InstanceKey == strip.InstanceKey) { strip = s; break; }
					}
				}
			}
		}

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
			Debug.LogWarning($"ReelDetailItem: ReelSymbolDetailItemPrefab is not assigned on {name}. Cannot instantiate symbol items.");
			return;
		}
		if (SymbolDetailsRoot == null)
		{
			Debug.LogWarning($"ReelDetailItem: SymbolDetailsRoot is not assigned on {name}. Cannot parent instantiated items.");
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
		}
	}

	/// <summary>
	/// After Setup is called, configure each instantiated child `ReelSymbolDetailItem` to open
	/// the AddRemoveSymbolsMenu using the provided slot context and callback.
	/// </summary>
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

	/// <summary>
	/// Configure the remove-reel button to remove the bound reel from the provided slot.
	/// </summary>
	public void ConfigureRemoval(SlotsData slot, System.Action onRemoved = null, bool allowRemove = true)
	{
		boundSlot = slot;
		if (RemoveReelButton == null) return;

		// Show/hide remove button based on allowRemove flag
		RemoveReelButton.gameObject.SetActive(true);
		RemoveReelButton.interactable = allowRemove;

		RemoveReelButton.onClick.RemoveAllListeners();
		RemoveReelButton.onClick.AddListener(() =>
		{
			// Safety checks
			if (boundReel == null || boundSlot == null) return;

			try
			{
				// If removal is disabled via UI, do nothing
				if (!allowRemove) return;

				// Try to find a live engine for this slot so we can remove the visual reel
				var mgr = SlotsEngineManager.Instance;
				if (mgr != null)
				{
					var engine = mgr.FindEngineForSlotsData(boundSlot);
					if (engine != null)
					{
						// If engine is spinning, surface an error
						if (engine.CurrentState == State.Spinning)
						{
							throw new System.InvalidOperationException("Cannot remove reel while engine is spinning.");
						}

						// Find index of the ReelData within the engine's data list using AccessorId or strip InstanceKey when available
						int idx = -1;
						if (engine.CurrentSlotsData != null && engine.CurrentSlotsData.CurrentReelData != null)
						{
							for (int i = 0; i < engine.CurrentSlotsData.CurrentReelData.Count; i++)
							{
								var rd = engine.CurrentSlotsData.CurrentReelData[i];
								if (rd == null) continue;

								// Prefer matching by AccessorId when present
								if (boundReel.AccessorId > 0 && rd.AccessorId == boundReel.AccessorId) { idx = i; break; }

								// Next prefer matching by associated strip instance key when available
								var brStrip = boundReel.CurrentReelStrip;
								var rdStrip = rd.CurrentReelStrip;
								if (brStrip != null && rdStrip != null && !string.IsNullOrEmpty(brStrip.InstanceKey) && brStrip.InstanceKey == rdStrip.InstanceKey) { idx = i; break; }

								// Fallback to reference equality
								if (ReferenceEquals(rd, boundReel)) { idx = i; break; }
							}
						}

						if (idx >= 0 && idx < engine.CurrentReels.Count)
						{
							// Use engine API to remove the visual reel; this also updates engine data and persistence
							engine.RemoveReel(engine.CurrentReels[idx]);
							// Persist updated slots data
							SlotsDataManager.Instance?.UpdateSlotsData(engine.CurrentSlotsData);
							// Notify caller
							onRemoved?.Invoke();
							return;
						}
					}
				}

				// Fallback: no live engine found - operate on data model directly
				boundSlot.RemoveReel(boundReel);

				// Remove persisted reel data and any contained symbol data
				ReelDataManager.Instance?.RemoveDataIfExists(boundReel);

				// Persist slots update
				SlotsDataManager.Instance?.UpdateSlotsData(boundSlot);

				// Notify listeners / update UI via callback
				onRemoved?.Invoke();
			}
			catch (System.Exception ex)
			{
				Debug.LogWarning($"ReelDetailItem: failed to remove reel: {ex.Message}");
			}
		});
	}
}
