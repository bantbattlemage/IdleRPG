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
		lastStrip = strip; // cache for later menu wiring

		var defs = strip != null ? strip.SymbolDefinitions : null;
		var runtime = strip != null ? strip.RuntimeSymbols : null;

		int defsLen = defs != null ? defs.Length : 0;
		int runtimeLen = runtime != null ? runtime.Count : 0;

		// Target number of symbol slots: prefer strip.StripSize if available, otherwise fall back to ReelData.SymbolCount
		int target = 1;
		if (strip != null && strip.StripSize > 0) target = strip.StripSize;
		else if (data != null && data.SymbolCount > 0) target = data.SymbolCount;

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

		// First: instantiate items for indices that have runtime data (non-null)
		for (int i = 0; i < target; i++)
		{
			if (runtime != null && i < runtimeLen && runtime[i] != null)
			{
				var itm = Instantiate(ReelSymbolDetailItemPrefab, SymbolDetailsRoot);
				if (itm != null)
				{
					itm.gameObject.SetActive(true);
					itm.Setup(runtime[i], i);
				}
			}
		}

		// Second: instantiate remaining slots (definitions or empty placeholders) up to target
		for (int i = 0; i < target; i++)
		{
			bool hasRuntime = runtime != null && i < runtimeLen && runtime[i] != null;
			if (hasRuntime) continue;

			var itm = Instantiate(ReelSymbolDetailItemPrefab, SymbolDetailsRoot);
			if (itm != null)
			{
				itm.gameObject.SetActive(true);
				if (i < defsLen)
				{
					itm.Setup(defs[i], i);
				}
				else
				{
					// spawn placeholder for missing symbols (pass null so the item renders as Random)
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
