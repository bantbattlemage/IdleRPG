using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class SlotsEngineManager : Singleton<SlotsEngineManager>
{
	[SerializeField] private Transform slotsPagesRoot;
	[SerializeField] private GameObject slotsPagePrefab;

	[SerializeField] private Button nextPageButton;
	[SerializeField] private Button prevPageButton;

	[SerializeField] private GameObject slotsEnginePrefab;
	[SerializeField] private GameObject reelsGroupPrefab;

	[SerializeField] private SlotsDefinition testDefinition;

	private List<SlotsEngine> slotsEngines = new();
	private List<SlotsDisplayPage> slotsDisplayPages = new();
	private int currentSlotPageIndex;
	private SlotsDisplayPage currentSlotsDisplayPage => slotsDisplayPages[currentSlotPageIndex];

	void Start()
	{
		nextPageButton.onClick.AddListener(OnNextPageButtonPressed);
		prevPageButton.onClick.AddListener(OnPrevPageButtonPressed);
		
		nextPageButton.gameObject.SetActive(false);
		prevPageButton.gameObject.SetActive(false);
	}

	private void OnNextPageButtonPressed()
	{
		OnSlotsPageButtonPressed(1);
	}

	private void OnPrevPageButtonPressed()
	{
		OnSlotsPageButtonPressed(-1);
	}

	private void OnSlotsPageButtonPressed(object obj)
	{
		int adjust = (int)obj;
		currentSlotPageIndex += adjust;

		currentSlotsDisplayPage.transform.SetAsLastSibling();

		if (currentSlotPageIndex != slotsDisplayPages.Count - 1)
		{
			nextPageButton.gameObject.SetActive(true);
		}
		else
		{
			nextPageButton.gameObject.SetActive(false);
		}
		
		if (currentSlotPageIndex == 0)
		{
			prevPageButton.gameObject.SetActive(false);
		}
		else
		{
			prevPageButton.gameObject.SetActive(true);
		}

		foreach (SlotsDisplayPage page in slotsDisplayPages)
		{
			if (page == currentSlotsDisplayPage)
			{
				page.ToggleRenderers(true);
			}
			else
			{
				page.ToggleRenderers(false);
			}
		}
	}

	public SlotsEngine CreateSlots(SlotsData existingData = null, bool useGrid = false)
	{
		SlotsDisplayPage pageToUse;

		if ((slotsDisplayPages == null || !slotsDisplayPages.Any() || slotsDisplayPages.Last().slotsToDisplay.Count >= 4))
		{
			pageToUse = Instantiate(slotsPagePrefab, slotsPagesRoot).GetComponent<SlotsDisplayPage>();
			slotsDisplayPages.Add(pageToUse);
		}
		else
		{
			pageToUse = slotsDisplayPages.Last();
		}

		currentSlotPageIndex = slotsDisplayPages.Count - 1;

		RectTransform targetTransform = pageToUse.standardGroup;
		if (useGrid)
		{
			targetTransform = pageToUse.gridGroup;
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
		pageToUse.AddSlotsToPage(newSlots);

		if (slotsDisplayPages.Count > 1)
		{
			prevPageButton.gameObject.SetActive(true);
		}

		return newSlots;
	}

	public void DestroySlots(SlotsEngine slotsToDestroy)
	{
		if (!slotsEngines.Contains(slotsToDestroy))
		{
			throw new Exception("Tried to remove slots that engine doesn't have! Something has gone wrong");
		}

		// Remove from any display page that contains this slots instance
		foreach (var page in slotsDisplayPages.ToList())
		{
			if (page.slotsToDisplay != null && page.slotsToDisplay.Contains(slotsToDestroy))
			{
				page.RemoveSlotsFromPage(slotsToDestroy);

				// If page is now empty, remove/destroy the page
				if (page.slotsToDisplay.Count == 0)
				{
					slotsDisplayPages.Remove(page);
					Destroy(page.gameObject);

					// Adjust current page index if needed
					if (currentSlotPageIndex >= slotsDisplayPages.Count)
					{
						currentSlotPageIndex = Mathf.Max(0, slotsDisplayPages.Count - 1);
					}
				}
				break; // a slotsEngine will only be on a single page
			}
		}

		// Remove from the master engines list and destroy the engine + its reels root
		slotsEngines.Remove(slotsToDestroy);
		if (slotsToDestroy != null)
		{
			if (slotsToDestroy.ReelsRootTransform != null)
			{
				Destroy(slotsToDestroy.ReelsRootTransform.gameObject);
			}
			Destroy(slotsToDestroy.gameObject);
		}

		// Update UI and layout state
		if (slotsDisplayPages.Count == 0)
		{
			// Ensure buttons state
			nextPageButton.gameObject.SetActive(false);
			prevPageButton.gameObject.SetActive(false);
		}
		else
		{
			// Clamp current index and ensure visible page renderers are updated
			currentSlotPageIndex = Mathf.Clamp(currentSlotPageIndex, 0, slotsDisplayPages.Count - 1);
			foreach (SlotsDisplayPage p in slotsDisplayPages)
			{
				p.ToggleRenderers(p == currentSlotsDisplayPage);
			}

			// Update prev/next buttons
			nextPageButton.gameObject.SetActive(currentSlotPageIndex != slotsDisplayPages.Count - 1);
			prevPageButton.gameObject.SetActive(currentSlotPageIndex != 0);
		}

		AdjustSlotsCanvases();
	}

	public void AdjustSlotsCanvases()
	{
		if (currentSlotsDisplayPage.slotsToDisplay.Count >= 3)
		{
			currentSlotsDisplayPage.gridGroup.gameObject.SetActive(true);
			MovePageSlotsToGrid();
			currentSlotsDisplayPage.standardGroup.gameObject.SetActive(false);

			foreach (SlotsEngine s in currentSlotsDisplayPage.slotsToDisplay)
			{
				s.AdjustReelSize(currentSlotsDisplayPage.gridGroup.GetComponent<GridLayoutGroup>().cellSize.y);
			}

			currentSlotsDisplayPage.AdjustPlaceholderGroup();
		}
		else
		{
			currentSlotsDisplayPage.standardGroup.gameObject.SetActive(true);
			MovePageSlotsToDefaultCanvas();
			currentSlotsDisplayPage.gridGroup.gameObject.SetActive(false);

			foreach (SlotsEngine s in currentSlotsDisplayPage.slotsToDisplay)
			{
				s.AdjustReelSize(currentSlotsDisplayPage.standardGroup.sizeDelta.y);
			}
		}
	}

	public void MovePageSlotsToGrid()
	{
		foreach (SlotsEngine slot in currentSlotsDisplayPage.slotsToDisplay)
		{
			slot.ReelsRootTransform.SetParent(currentSlotsDisplayPage.gridGroup);
		}
	}

	public void MovePageSlotsToDefaultCanvas()
	{
		foreach (SlotsEngine slot in currentSlotsDisplayPage.slotsToDisplay)
		{
			slot.ReelsRootTransform.SetParent(currentSlotsDisplayPage.standardGroup);
		}
	}
}