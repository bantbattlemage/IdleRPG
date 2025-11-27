using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ReelDetailItem : MonoBehaviour
{
	public TMP_Text ReelIndexText;
	public RectTransform SymbolDetailsRoot;
	public ReelSymbolDetailItem ReelSymbolDetailItemPrefab;

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
}
