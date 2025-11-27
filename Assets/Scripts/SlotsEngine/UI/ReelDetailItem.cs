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
		for (int i = 0; i < defs.Length; i++)
		{
			var d = defs[i];
			if (ReelSymbolDetailItemPrefab != null && SymbolDetailsRoot != null)
			{
				var itm = Instantiate(ReelSymbolDetailItemPrefab, SymbolDetailsRoot);
				itm.Setup(d, i);
			}
		}
	}
}
