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

		// Display the reel's strip definitions when available
		var strip = data.CurrentReelStrip;
		lastStrip = strip; // cache for later menu wiring
		if (strip == null || strip.SymbolDefinitions == null) return;

		var defs = strip.SymbolDefinitions;
		int target = Mathf.Max(strip.StripSize, defs.Length);
		for (int i = 0; i < target; i++)
		{
			if (ReelSymbolDetailItemPrefab == null || SymbolDetailsRoot == null) break;
			var itm = Instantiate(ReelSymbolDetailItemPrefab, SymbolDetailsRoot);
			if (i < defs.Length)
			{
				itm.Setup(defs[i], i);
			}
			else
			{
				// spawn placeholder for missing symbols (pass null so the item renders as empty)
				itm.Setup((SymbolDefinition)null, i);
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
