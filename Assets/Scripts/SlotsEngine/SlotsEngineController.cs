using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

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

	public void AdjustSlotsCanvases()
	{
		if (slotsEngines.Count >= 3)
		{
			slotsGridCanvasGroup.gameObject.SetActive(true);
			MoveAllSlotsToGrid();
			slotsCanvasGroup.gameObject.SetActive(false);

			foreach (SlotsEngine s in slotsEngines)
			{
				s.AdjustReelSize(slotsGridCanvasGroup.GetComponent<GridLayoutGroup>().cellSize.y);
			}
		}
		else
		{
			slotsCanvasGroup.gameObject.SetActive(true);
			MoveAllSlotsToDefaultCanvas();
			slotsGridCanvasGroup.gameObject.SetActive(false);

			foreach (SlotsEngine s in slotsEngines)
			{
				s.AdjustReelSize(slotsCanvasGroup.sizeDelta.y);
			}
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
