using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class SlotsEngineController : Singleton<SlotsEngineController>
{
	[SerializeField] private RectTransform slotsCanvasGroup;
	[SerializeField] private RectTransform slotsGridCanvasGroup;

	[SerializeField] private GameObject slotsEnginePrefab;
	[SerializeField] private GameObject reelsGroupPrefab;

	[SerializeField] private SlotsDefinition testDefinition;

	private List<SlotsEngine> slotsEngines = new();

	public SlotsEngine CreateSlots(SlotsData existingData = null, bool useGrid = false)
	{
		RectTransform targetTransform = slotsCanvasGroup;
		if (useGrid)
		{
			targetTransform = slotsGridCanvasGroup;
		}

		SlotsEngine newSlots = Instantiate(slotsEnginePrefab, transform).GetComponent<SlotsEngine>();
		GameObject newReelsGroup = Instantiate(reelsGroupPrefab, targetTransform);

		if (existingData != null)
		{
			newSlots.InitializeSlotsEngine(newReelsGroup.transform, existingData);
		}
		else
		{
			newSlots.InitializeSlotsEngine(newReelsGroup.transform, testDefinition);
		}

		slotsEngines.Add(newSlots);

		AdjustSlotsCanvases();

		return newSlots;
	}

	public void DestroySlots(SlotsEngine slotsToDestroy)
	{
		if (!slotsEngines.Contains(slotsToDestroy))
		{
			throw new Exception("Tried to remove slots that engine doesn't have! Something has gone wrong");
		}

		slotsEngines.Remove(slotsToDestroy);
		Destroy(slotsToDestroy.gameObject);
		Destroy(slotsToDestroy.ReelsRootTransform.gameObject);

		AdjustSlotsCanvases();
	}

	private void AdjustSlotsCanvases()
	{
		if (slotsEngines.Count >= 4)
		{
			slotsGridCanvasGroup.gameObject.SetActive(true);
			MoveAllSlotsToGrid();
			slotsCanvasGroup.gameObject.SetActive(false);
		}
		else
		{
			slotsCanvasGroup.gameObject.SetActive(true);
			MoveAllSlotsToDefaultCanvas();
			slotsGridCanvasGroup.gameObject.SetActive(false);
		}
	}

	public void MoveAllSlotsToGrid()
	{
		foreach (SlotsEngine slot in slotsEngines)
		{
			slot.ReelsRootTransform.SetParent(slotsGridCanvasGroup);
		}
	}

	public void MoveAllSlotsToDefaultCanvas()
	{
		foreach (SlotsEngine slot in slotsEngines)
		{
			slot.ReelsRootTransform.SetParent(slotsCanvasGroup);
		}
	}
}
