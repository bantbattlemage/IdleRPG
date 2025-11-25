using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;


public class SlotsDisplayPage : MonoBehaviour
{
	public List<SlotsEngine> slotsToDisplay;
	public RectTransform gridGroup; // Single group now used for all layouts

	[SerializeField] private RectTransform placeHolderGroup; // placeholder when there are 3 items so 4th position isn't transparent

	public void AddSlotsToPage(SlotsEngine slotsToAdd)
	{
		slotsToDisplay.Add(slotsToAdd);
		AdjustPlaceholderGroup();
	}

	public void RemoveSlotsFromPage(SlotsEngine slotsToRemove)
	{
		slotsToDisplay.Remove(slotsToRemove);
		AdjustPlaceholderGroup();
	}

	public void AdjustPlaceholderGroup()
	{
		if (placeHolderGroup == null) return;
		placeHolderGroup.gameObject.SetActive(slotsToDisplay.Count == 3);
		placeHolderGroup.SetAsLastSibling();
	}

	public void ToggleRenderers(bool shouldEnable)
	{
		transform.GetComponentsInChildren<Canvas>().ToList().ForEach(x => { x.enabled = shouldEnable; });
		// NEW: inform engines of page visibility so they can pause spins
		foreach (var engine in slotsToDisplay)
		{
			if (engine == null) continue;
			try { engine.SetPageActive(shouldEnable); } catch { }
		}
	}
}
