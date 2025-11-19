using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;


public class SlotsDisplayPage : MonoBehaviour
{
	public List<SlotsEngine> slotsToDisplay;
	public RectTransform standardGroup;
	public RectTransform gridGroup;

	[SerializeField] private RectTransform placeHolderGroup;	//	placeholder when there are 3 items so 4th position isn't transparent

	public void AddSlotsToPage(SlotsEngine slotsToAdd)
	{
		slotsToDisplay.Add(slotsToAdd);
		AdjustPlaceholderGroup();
	}

	public void RemoveSlotsFromPage(SlotsEngine slotsToRemove)
	{
		slotsToDisplay.Remove(slotsToRemove);
	}

	public void AdjustPlaceholderGroup()
	{
		placeHolderGroup.gameObject.SetActive(slotsToDisplay.Count == 3);
		placeHolderGroup.SetAsLastSibling();
	}
}
