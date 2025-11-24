using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages creation and layout of multiple `SlotsEngine` instances across paged UI containers.
/// Responsibilities:
/// - Creates/destroys slots, assigns them to display pages and wires up reel-changed handlers.
/// - Computes responsive GridLayout cell sizes and forwards sizing to individual slots (AdjustReelSize).
/// - Provides navigation between pages and ensures proper button visibility.
/// 
/// Notes:
/// - Applies layout changes on page/slot count changes via `AdjustSlotsCanvases` and for single-slot changes via `AdjustSlotCanvas`.
/// </summary>
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

	private void OnNextPageButtonPressed() => OnSlotsPageButtonPressed(1);
	private void OnPrevPageButtonPressed() => OnSlotsPageButtonPressed(-1);

	/// <summary>
	/// Navigate the page index by +/- 1 and update visibility/layout.
	/// </summary>
	private void OnSlotsPageButtonPressed(object obj)
	{
		int adjust = (int)obj; currentSlotPageIndex += adjust; currentSlotsDisplayPage.transform.SetAsLastSibling();
		if (currentSlotPageIndex != slotsDisplayPages.Count - 1) nextPageButton.gameObject.SetActive(true); else nextPageButton.gameObject.SetActive(false);
		if (currentSlotPageIndex == 0) prevPageButton.gameObject.SetActive(false); else prevPageButton.gameObject.SetActive(true);
		foreach (SlotsDisplayPage page in slotsDisplayPages) page.ToggleRenderers(page == currentSlotsDisplayPage);
	}

	/// <summary>
	/// Creates a new `SlotsEngine` and places it on the current (or newly created) display page.
	/// When `existingData` is provided, the engine is initialized using that data; otherwise a test definition is used.
	/// </summary>
	public SlotsEngine CreateSlots(SlotsData existingData = null)
	{
		SlotsDisplayPage pageToUse;
		if ((slotsDisplayPages == null || !slotsDisplayPages.Any() || slotsDisplayPages.Last().slotsToDisplay.Count >= 4))
		{
			pageToUse = Instantiate(slotsPagePrefab, slotsPagesRoot).GetComponent<SlotsDisplayPage>(); slotsDisplayPages.Add(pageToUse);
		}
		else pageToUse = slotsDisplayPages.Last();
		currentSlotPageIndex = slotsDisplayPages.Count - 1;
		RectTransform targetTransform = pageToUse.gridGroup;
		SlotsEngine newSlots = Instantiate(slotsEnginePrefab, transform).GetComponent<SlotsEngine>();
		GameObject newReelsGroup = Instantiate(reelsGroupPrefab, targetTransform);
		if (existingData != null) newSlots.InitializeSlotsEngine(newReelsGroup.transform, existingData); else newSlots.InitializeSlotsEngine(newReelsGroup.transform, testDefinition);
		try { if (SlotsDataManager.Instance != null && newSlots.CurrentSlotsData != null) SlotsDataManager.Instance.UpdateSlotsData(newSlots.CurrentSlotsData); } catch (Exception ex) { Debug.LogException(ex); }
		newSlots.RegisterReelChanged(OnSlotReelChangedEvent);
		slotsEngines.Add(newSlots); pageToUse.AddSlotsToPage(newSlots);
		if (slotsDisplayPages.Count > 1) prevPageButton.gameObject.SetActive(true);
		// Slot count changed -> adjust whole page layout
		AdjustSlotsCanvases();
		return newSlots;
	}

	/// <summary>
	/// Destroys an existing slot instance, removes its page entry and cleans up associated data and UI.
	/// Automatically updates page navigation visibility.
	/// </summary>
	public void DestroySlots(SlotsEngine slotsToDestroy)
	{
		if (!slotsEngines.Contains(slotsToDestroy)) throw new Exception("Tried to remove slots that engine doesn't have! Something has gone wrong");
		// Remove associated data first to avoid orphan records
		try { SlotsDataManager.Instance?.RemoveSlotsDataIfExists(slotsToDestroy.CurrentSlotsData); } catch (Exception ex) { Debug.LogWarning($"SlotsData removal failed: {ex.Message}"); }

		foreach (var page in slotsDisplayPages.ToList())
		{
			if (page.slotsToDisplay != null && page.slotsToDisplay.Contains(slotsToDestroy))
			{
				page.RemoveSlotsFromPage(slotsToDestroy);
				if (page.slotsToDisplay.Count == 0)
				{
					slotsDisplayPages.Remove(page); Destroy(page.gameObject); if (currentSlotPageIndex >= slotsDisplayPages.Count) currentSlotPageIndex = Mathf.Max(0, slotsDisplayPages.Count - 1);
				}
				break;
			}
		}
		slotsToDestroy.UnregisterReelChanged(OnSlotReelChangedEvent);
		slotsEngines.Remove(slotsToDestroy);

		if (slotsToDestroy != null)
		{
			try { SlotConsoleController.Instance?.ClearMessagesForSlot(slotsToDestroy); } catch { }

			// Collect roots to destroy: reels root and slot GameObject
			var roots = new List<GameObject>();
			if (slotsToDestroy.ReelsRootTransform != null) roots.Add(slotsToDestroy.ReelsRootTransform.gameObject);
			if (slotsToDestroy.gameObject != null) roots.Add(slotsToDestroy.gameObject);

			// Destroy roots immediately (incremental coroutine removed)
			for (int i = 0; i < roots.Count; i++)
			{
				if (roots[i] != null) GameObject.Destroy(roots[i]);
			}
		}

		if (slotsDisplayPages.Count == 0)
		{
			nextPageButton.gameObject.SetActive(false); prevPageButton.gameObject.SetActive(false);
			// Broadcast global final removal event so UI can hide slot-specific components
			GlobalEventManager.Instance?.BroadcastEvent(SlotsEvent.AllSlotsRemoved);
		}
		else
		{
			currentSlotPageIndex = Mathf.Clamp(currentSlotPageIndex, 0, slotsDisplayPages.Count - 1);
			foreach (SlotsDisplayPage p in slotsDisplayPages) p.ToggleRenderers(p == currentSlotsDisplayPage);
			nextPageButton.gameObject.SetActive(currentSlotPageIndex != slotsDisplayPages.Count - 1);
			prevPageButton.gameObject.SetActive(currentSlotPageIndex != 0);
		}

		// Adjust layout immediately
		AdjustSlotsCanvases();
	}

	/// <summary>
	/// Adjust all slots on the current page (used only when slot count changes).
	/// </summary>
	public void AdjustSlotsCanvases()
	{
		// Guard: no pages / slots
		if (slotsDisplayPages == null || slotsDisplayPages.Count == 0)
		{
			return;
		}
		if (currentSlotPageIndex < 0 || currentSlotPageIndex >= slotsDisplayPages.Count)
		{
			currentSlotPageIndex = 0;
		}

		Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(currentSlotsDisplayPage.gridGroup);
		var grid = currentSlotsDisplayPage.gridGroup.GetComponent<GridLayoutGroup>(); if (grid == null) return;
		int slotCount = currentSlotsDisplayPage.slotsToDisplay?.Count ?? 0; if (slotCount == 0) { currentSlotsDisplayPage.AdjustPlaceholderGroup(); return; }
		int columns = Mathf.CeilToInt(Mathf.Sqrt(slotCount)); int rows = Mathf.CeilToInt(slotCount / (float)columns);
		Rect rect = currentSlotsDisplayPage.gridGroup.rect; float availableWidth = Mathf.Max(1f, rect.width - grid.padding.left - grid.padding.right); float availableHeight = Mathf.Max(1f, rect.height - grid.padding.top - grid.padding.bottom);
		float totalSpacingX = grid.spacing.x * (columns - 1); float totalSpacingY = grid.spacing.y * (rows - 1);
		float cellWidth = (availableWidth - totalSpacingX) / columns; float cellHeight = (availableHeight - totalSpacingY) / rows;
		cellWidth = Mathf.Max(1f, cellWidth); cellHeight = Mathf.Max(1f, cellHeight);
		grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = columns; grid.cellSize = new Vector2(cellWidth, cellHeight);
		foreach (SlotsEngine s in currentSlotsDisplayPage.slotsToDisplay)
		{
			// Use the specialized height-change path which avoids regenerating dummies and reassigning sprites
			ApplyCellSizeToSlotForHeightChange(s, cellWidth, cellHeight);
		}
		currentSlotsDisplayPage.AdjustPlaceholderGroup();
	}

	/// <summary>
	/// Adjust only a single slot (used when reels/symbol counts change inside that slot).
	/// </summary>
	public void AdjustSlotCanvas(SlotsEngine slot)
	{
		if (slot == null) return; if (slotsDisplayPages == null || slotsDisplayPages.Count == 0) return; if (currentSlotPageIndex < 0 || currentSlotPageIndex >= slotsDisplayPages.Count) currentSlotPageIndex = 0;
		var grid = currentSlotsDisplayPage.gridGroup.GetComponent<GridLayoutGroup>(); if (grid == null) return;
		Canvas.ForceUpdateCanvases();
		ApplyCellSizeToSlot(slot, grid.cellSize.x, grid.cellSize.y);
	}

	/// <summary>
	/// Adjust only a single slot when a reel's height change affects max height.
	/// This uses a specialized rescaling path that doesn't regenerate dummies (they were already regenerated selectively).
	/// </summary>
	public void AdjustSlotCanvasForHeightChange(SlotsEngine slot)
	{
		if (slot == null) return; if (slotsDisplayPages == null || slotsDisplayPages.Count == 0) return; if (currentSlotPageIndex < 0 || currentSlotPageIndex >= slotsDisplayPages.Count) currentSlotPageIndex = 0;
		var grid = currentSlotsDisplayPage.gridGroup.GetComponent<GridLayoutGroup>(); if (grid == null) return;
		Canvas.ForceUpdateCanvases();
		ApplyCellSizeToSlotForHeightChange(slot, grid.cellSize.x, grid.cellSize.y);
	}

	private void ApplyCellSizeToSlot(SlotsEngine s, float cellWidth, float cellHeight)
	{
		if (s.ReelsRootTransform != null)
		{
			var rt = s.ReelsRootTransform.GetComponent<RectTransform>(); if (rt != null) { rt.sizeDelta = new Vector2(cellWidth, cellHeight); rt.localScale = Vector3.one; }
		}
		s.AdjustReelSize(cellHeight, cellWidth);
	}

	private void ApplyCellSizeToSlotForHeightChange(SlotsEngine s, float cellWidth, float cellHeight)
	{
		if (s.ReelsRootTransform != null)
		{
			var rt = s.ReelsRootTransform.GetComponent<RectTransform>(); if (rt != null) { rt.sizeDelta = new Vector2(cellWidth, cellHeight); rt.localScale = Vector3.one; }
		}
		s.AdjustReelSizeForHeightChange(cellHeight, cellWidth);
	}

	/// <summary>
	/// Re-parents all slots on the current page back to the Grid container.
	/// </summary>
	public void MovePageSlotsToGrid()
	{
		foreach (SlotsEngine slot in currentSlotsDisplayPage.slotsToDisplay) slot.ReelsRootTransform.SetParent(currentSlotsDisplayPage.gridGroup, false);
	}
	public void MovePageSlotsToDefaultCanvas() => MovePageSlotsToGrid();

	private void OnSlotReelChanged(GameReel reel, int index)
	{
		// Legacy path not used; kept for reference
		AdjustSlotCanvas(reel?.OwnerEngine);
	}

	private void OnSlotReelChangedEvent(object obj)
	{
		// obj expected to be GameReel from SlotsEngine broadcast
		if (obj is GameReel gr) AdjustSlotCanvas(gr.OwnerEngine); else AdjustSlotsCanvases();
	}
}