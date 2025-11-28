using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ReelDetailItem : MonoBehaviour
{
	public TMP_Text ReelIndexText;
	public RectTransform SymbolDetailsRoot;
	public ReelSymbolDetailItem ReelSymbolDetailItemPrefab;

	// cached strip used to configure symbol item menu callbacks after Setup
	private ReelStripData lastStrip;

	public void Setup(ReelData data, int index)
	{
		if (ReelIndexText != null) ReelIndexText.text = "Reel " + index.ToString();

		// clear existing children
		if (SymbolDetailsRoot != null)
		{
			for (int i = SymbolDetailsRoot.childCount - 1; i >= 0; i--) Destroy(SymbolDetailsRoot.GetChild(i).gameObject);
		}

		if (data == null) return;

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
}
