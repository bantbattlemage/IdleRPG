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

		var symbols = data.CurrentSymbolData;
		if (symbols == null) return;

		for (int i = 0; i < symbols.Count; i++)
		{
			var s = symbols[i];
			if (ReelSymbolDetailItemPrefab != null && SymbolDetailsRoot != null)
			{
				var itm = Instantiate(ReelSymbolDetailItemPrefab, SymbolDetailsRoot);
				itm.Setup(s, i);
			}
		}
	}
}
