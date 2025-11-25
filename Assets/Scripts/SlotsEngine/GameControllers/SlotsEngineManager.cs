using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages creation and layout of multiple `SlotsEngine` instances across paged UI containers.
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
	[SerializeField] private bool navigationDebug = false; // NEW: optional debug logging
	private bool pagesNormalized = false; // NEW: ensures initial activation normalization occurs before first spin

	private List<SlotsEngine> slotsEngines = new();
	private List<SlotsDisplayPage> slotsDisplayPages = new();
	private int currentSlotPageIndex;
	private SlotsDisplayPage currentSlotsDisplayPage => (currentSlotPageIndex >= 0 && currentSlotPageIndex < slotsDisplayPages.Count) ? slotsDisplayPages[currentSlotPageIndex] : null;

	void Start()
	{
		nextPageButton.onClick.AddListener(() => ChangePage(1));
		prevPageButton.onClick.AddListener(() => ChangePage(-1));
		UpdateNavigationButtons();
	}

	// NEW: centralized page change logic
	private void ChangePage(int delta)
	{
		if (slotsDisplayPages == null || slotsDisplayPages.Count == 0) return;
		int target = Mathf.Clamp(currentSlotPageIndex + delta, 0, slotsDisplayPages.Count - 1);
		ActivatePage(target);
	}

	public void NormalizePageActivation()
	{
		if (pagesNormalized) return;
		// Ensure a valid index
		currentSlotPageIndex = Mathf.Clamp(currentSlotPageIndex, 0, Mathf.Max(0, slotsDisplayPages.Count - 1));
		ActivatePage(currentSlotPageIndex);
		pagesNormalized = true;
		if (navigationDebug) Debug.Log("[SlotsEngineManager] Initial page activation normalized.");
	}

	// NEW: activate a specific page index; handles visibility toggling and button state
	private void ActivatePage(int index)
	{
		if (slotsDisplayPages == null || slotsDisplayPages.Count == 0) return;
		index = Mathf.Clamp(index, 0, slotsDisplayPages.Count - 1);
		currentSlotPageIndex = index; // always assign even if same to allow normalization pass
		for (int i = 0; i < slotsDisplayPages.Count; i++)
		{
			var page = slotsDisplayPages[i];
			bool shouldEnable = (i == currentSlotPageIndex);
			try { page.ToggleRenderers(shouldEnable); } catch { }
		}
		UpdateNavigationButtons();
		if (navigationDebug) Debug.Log($"[SlotsEngineManager] Activated page {currentSlotPageIndex + 1}/{slotsDisplayPages.Count}");
	}

	// NEW: button visibility update logic extracted
	private void UpdateNavigationButtons()
	{
		bool hasPages = slotsDisplayPages != null && slotsDisplayPages.Count > 0;
		if (!hasPages)
		{
			nextPageButton.gameObject.SetActive(false);
			prevPageButton.gameObject.SetActive(false);
			return;
		}
		nextPageButton.gameObject.SetActive(currentSlotPageIndex < slotsDisplayPages.Count - 1);
		prevPageButton.gameObject.SetActive(currentSlotPageIndex > 0);
	}

	/// <summary>
	/// Creates a new `SlotsEngine` and places it on the current (or newly created) display page.
	/// </summary>
	public SlotsEngine CreateSlots(SlotsData existingData = null)
	{
		SlotsDisplayPage pageToUse;
		if (slotsDisplayPages == null || slotsDisplayPages.Count == 0 || slotsDisplayPages.Last().slotsToDisplay.Count >= 4)
		{
			pageToUse = Instantiate(slotsPagePrefab, slotsPagesRoot).GetComponent<SlotsDisplayPage>();
			slotsDisplayPages.Add(pageToUse);
		}
		else pageToUse = slotsDisplayPages.Last();

		RectTransform targetTransform = pageToUse.gridGroup;
		SlotsEngine newSlots = Instantiate(slotsEnginePrefab, transform).GetComponent<SlotsEngine>();
		GameObject newReelsGroup = Instantiate(reelsGroupPrefab, targetTransform);
		if (existingData != null) newSlots.InitializeSlotsEngine(newReelsGroup.transform, existingData); else newSlots.InitializeSlotsEngine(newReelsGroup.transform, testDefinition);
		try { if (SlotsDataManager.Instance != null && newSlots.CurrentSlotsData != null) SlotsDataManager.Instance.UpdateSlotsData(newSlots.CurrentSlotsData); } catch (Exception ex) { Debug.LogException(ex); }
		newSlots.RegisterReelChanged(OnSlotReelChangedEvent);
		slotsEngines.Add(newSlots); pageToUse.AddSlotsToPage(newSlots);

		// Always activate the page containing the newly created slot
		currentSlotPageIndex = slotsDisplayPages.IndexOf(pageToUse);
		ActivatePage(currentSlotPageIndex);
		// After creating at least one page, we can normalize immediately (will simply re-run ActivatePage once)
		NormalizePageActivation();
		// Slot count changed -> adjust whole page layout
		AdjustSlotsCanvases();
		return newSlots;
	}

	/// <summary>
	/// Destroys an existing slot instance and updates paging/navigation.
	/// </summary>
	public void DestroySlots(SlotsEngine slotsToDestroy)
	{
		if (!slotsEngines.Contains(slotsToDestroy)) throw new Exception("Tried to remove slots that engine doesn't have! Something has gone wrong");
		try { SlotsDataManager.Instance?.RemoveSlotsDataIfExists(slotsToDestroy.CurrentSlotsData); } catch (Exception ex) { Debug.LogWarning($"SlotsData removal failed: {ex.Message}"); }

		// Remove from pages
		foreach (var page in slotsDisplayPages.ToList())
		{
			if (page.slotsToDisplay != null && page.slotsToDisplay.Contains(slotsToDestroy))
			{
				page.RemoveSlotsFromPage(slotsToDestroy);
				if (page.slotsToDisplay.Count == 0)
				{
					slotsDisplayPages.Remove(page);
					Destroy(page.gameObject);
				}
				break;
			}
		}

		slotsToDestroy.UnregisterReelChanged(OnSlotReelChangedEvent);
		slotsEngines.Remove(slotsToDestroy);
		try { SlotConsoleController.Instance?.ClearMessagesForSlot(slotsToDestroy); } catch { }

		if (slotsToDestroy.ReelsRootTransform != null) Destroy(slotsToDestroy.ReelsRootTransform.gameObject);
		Destroy(slotsToDestroy.gameObject);

		// Adjust current index if out of range
		currentSlotPageIndex = Mathf.Clamp(currentSlotPageIndex, 0, Math.Max(0, slotsDisplayPages.Count - 1));
		ActivatePage(currentSlotPageIndex);
		NormalizePageActivation();

		if (slotsDisplayPages.Count == 0)
		{
			GlobalEventManager.Instance?.BroadcastEvent(SlotsEvent.AllSlotsRemoved);
		}

		AdjustSlotsCanvases();
	}

	public void AdjustSlotsCanvases()
	{
		if (slotsDisplayPages == null || slotsDisplayPages.Count == 0) { UpdateNavigationButtons(); return; }
		currentSlotPageIndex = Mathf.Clamp(currentSlotPageIndex, 0, slotsDisplayPages.Count - 1);
		var page = currentSlotsDisplayPage; if (page == null) { UpdateNavigationButtons(); return; }
		Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(page.gridGroup);
		var grid = page.gridGroup.GetComponent<GridLayoutGroup>(); if (grid == null) { UpdateNavigationButtons(); return; }
		int slotCount = page.slotsToDisplay?.Count ?? 0; if (slotCount == 0) { page.AdjustPlaceholderGroup(); UpdateNavigationButtons(); return; }
		int columns = Mathf.CeilToInt(Mathf.Sqrt(slotCount)); int rows = Mathf.CeilToInt(slotCount / (float)columns);
		Rect rect = page.gridGroup.rect; float availableWidth = Mathf.Max(1f, rect.width - grid.padding.left - grid.padding.right); float availableHeight = Mathf.Max(1f, rect.height - grid.padding.top - grid.padding.bottom);
		float totalSpacingX = grid.spacing.x * (columns - 1); float totalSpacingY = grid.spacing.y * (rows - 1);
		float cellWidth = (availableWidth - totalSpacingX) / columns; float cellHeight = (availableHeight - totalSpacingY) / rows;
		cellWidth = Mathf.Max(1f, cellWidth); cellHeight = Mathf.Max(1f, cellHeight);
		grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = columns; grid.cellSize = new Vector2(cellWidth, cellHeight);
		foreach (SlotsEngine s in page.slotsToDisplay) ApplyCellSizeToSlotForHeightChange(s, cellWidth, cellHeight);
		page.AdjustPlaceholderGroup();
		UpdateNavigationButtons();
	}

	public void AdjustSlotCanvas(SlotsEngine slot)
	{
		if (slot == null || slotsDisplayPages == null || slotsDisplayPages.Count == 0) return;
		var page = currentSlotsDisplayPage; if (page == null) return; var grid = page.gridGroup.GetComponent<GridLayoutGroup>(); if (grid == null) return; Canvas.ForceUpdateCanvases(); ApplyCellSizeToSlot(slot, grid.cellSize.x, grid.cellSize.y);
	}

	public void AdjustSlotCanvasForHeightChange(SlotsEngine slot)
	{
		if (slot == null || slotsDisplayPages == null || slotsDisplayPages.Count == 0) return;
		var page = currentSlotsDisplayPage; if (page == null) return; var grid = page.gridGroup.GetComponent<GridLayoutGroup>(); if (grid == null) return; Canvas.ForceUpdateCanvases(); ApplyCellSizeToSlotForHeightChange(slot, grid.cellSize.x, grid.cellSize.y);
	}

	private void ApplyCellSizeToSlot(SlotsEngine s, float cellWidth, float cellHeight)
	{
		if (s?.ReelsRootTransform != null)
		{
			var rt = s.ReelsRootTransform.GetComponent<RectTransform>(); if (rt != null) { rt.sizeDelta = new Vector2(cellWidth, cellHeight); rt.localScale = Vector3.one; }
		}
		s?.AdjustReelSize(cellHeight, cellWidth);
	}

	private void ApplyCellSizeToSlotForHeightChange(SlotsEngine s, float cellWidth, float cellHeight)
	{
		if (s?.ReelsRootTransform != null)
		{
			var rt = s.ReelsRootTransform.GetComponent<RectTransform>(); if (rt != null) { rt.sizeDelta = new Vector2(cellWidth, cellHeight); rt.localScale = Vector3.one; }
		}
		s?.AdjustReelSizeForHeightChange(cellHeight, cellWidth);
	}

	public void MovePageSlotsToGrid()
	{
		var page = currentSlotsDisplayPage; if (page == null) return; foreach (SlotsEngine slot in page.slotsToDisplay) slot.ReelsRootTransform.SetParent(page.gridGroup, false);
	}
	public void MovePageSlotsToDefaultCanvas() => MovePageSlotsToGrid();

	private void OnSlotReelChanged(GameReel reel, int index)
	{
		AdjustSlotCanvas(reel?.OwnerEngine);
	}

	private void OnSlotReelChangedEvent(object obj)
	{
		if (obj is GameReel gr) AdjustSlotCanvas(gr.OwnerEngine); else AdjustSlotsCanvases();
	}
}