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

		// Always use the single gridGroup as the parent for reels
		RectTransform targetTransform = pageToUse.gridGroup;

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

		// Subscribe to runtime reel add/remove so manager can adjust layout
		// Old native events replaced with EventManager-backed API
		newSlots.RegisterReelChanged(OnSlotReelChangedEvent);

		slotsEngines.Add(newSlots);
		pageToUse.AddSlotsToPage(newSlots);

		if (slotsDisplayPages.Count > 1)
		{
			prevPageButton.gameObject.SetActive(true);
		}

		// Ensure layout updates in case reel count affects sizing
		AdjustSlotsCanvases();

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

		// Unsubscribe events (EventManager-backed API)
		slotsToDestroy.UnregisterReelChanged(OnSlotReelChangedEvent);

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
		// Force UI update to ensure layout components have up-to-date values
		Canvas.ForceUpdateCanvases();

		// Force rebuild so GridLayoutGroup.cellSize / rect is correct
		LayoutRebuilder.ForceRebuildLayoutImmediate(currentSlotsDisplayPage.gridGroup);

		var grid = currentSlotsDisplayPage.gridGroup.GetComponent<GridLayoutGroup>();
		if (grid == null) return;

		int slotCount = currentSlotsDisplayPage.slotsToDisplay?.Count ?? 0;
		if (slotCount == 0)
		{
			currentSlotsDisplayPage.AdjustPlaceholderGroup();
			return;
		}

		// Determine an appropriate columns/rows layout (try to form a near-square grid)
		int columns = Mathf.CeilToInt(Mathf.Sqrt(slotCount));
		int rows = Mathf.CeilToInt(slotCount / (float)columns);

		// Calculate available space inside the grid rect (subtract padding)
		Rect rect = currentSlotsDisplayPage.gridGroup.rect;
		float availableWidth = Mathf.Max(1f, rect.width - grid.padding.left - grid.padding.right);
		float availableHeight = Mathf.Max(1f, rect.height - grid.padding.top - grid.padding.bottom);

		// Account for spacing between cells
		float totalSpacingX = grid.spacing.x * (columns - 1);
		float totalSpacingY = grid.spacing.y * (rows - 1);

		float cellWidth = (availableWidth - totalSpacingX) / columns;
		float cellHeight = (availableHeight - totalSpacingY) / rows;

		// Ensure positive sizes
		cellWidth = Mathf.Max(1f, cellWidth);
		cellHeight = Mathf.Max(1f, cellHeight);

		// Apply to GridLayoutGroup so it places children accordingly
		grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		grid.constraintCount = columns;
		grid.cellSize = new Vector2(cellWidth, cellHeight);

		// Use the grid cell sizes to size reels for every slot on the page
		foreach (SlotsEngine s in currentSlotsDisplayPage.slotsToDisplay)
		{
			// Ensure the slot's root rect matches the grid cell so GridLayout places them correctly
			if (s.ReelsRootTransform != null)
			{
				var rt = s.ReelsRootTransform.GetComponent<RectTransform>();
				if (rt != null)
				{
					rt.sizeDelta = new Vector2(cellWidth, cellHeight);
					rt.localScale = Vector3.one;
				}
			}

			// Pass both height and width from the cell size so the slot can constrain layout
			s.AdjustReelSize(cellHeight, cellWidth);
		}

		currentSlotsDisplayPage.AdjustPlaceholderGroup();
	}

	public void MovePageSlotsToGrid()
	{
		foreach (SlotsEngine slot in currentSlotsDisplayPage.slotsToDisplay)
		{
			// Use worldPositionStays=false so anchored positions behave under the layout
			slot.ReelsRootTransform.SetParent(currentSlotsDisplayPage.gridGroup, false);
		}
	}

	public void MovePageSlotsToDefaultCanvas()
	{
		// Alias to gridGroup now - single group approach
		MovePageSlotsToGrid();
	}

	private void OnSlotReelChanged(GameReel reel, int index)
	{
		// Called whenever a reel is added/removed in any SlotsEngine
		// Trigger layout adjustments so other slots and the page update sizes/positions
		AdjustSlotsCanvases();
	}

	private void OnSlotReelChangedEvent(object obj)
	{
		// EventManager passes the changed reel or similar payload; we only need to update layout
		AdjustSlotsCanvases();
	}
}