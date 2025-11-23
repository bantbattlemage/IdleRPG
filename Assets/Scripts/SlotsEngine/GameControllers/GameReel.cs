using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class GameReel : MonoBehaviour
{
    private ReelData currentReelData; public ReelData CurrentReelData => currentReelData;
    private ReelStripData reelStrip; public ReelStripData ReelStrip => reelStrip;
    public int ID => id; private int id;
    public bool Spinning => spinning; private bool spinning = false;
    public List<GameSymbol> Symbols => symbols;
    private SlotsEngine ownerEngine; public SlotsEngine OwnerEngine => ownerEngine;
    private Transform symbolRoot; private Transform nextSymbolsRoot;
    private List<GameSymbol> symbols = new List<GameSymbol>();
    private List<GameSymbol> topDummySymbols = new List<GameSymbol>();
    private List<GameSymbol> bottomDummySymbols = new List<GameSymbol>();
    private List<GameSymbol> bufferSymbols = new List<GameSymbol>();
    private List<GameSymbol> bufferTopDummySymbols = new List<GameSymbol>();
    private List<GameSymbol> bufferBottomDummySymbols = new List<GameSymbol>();
    private bool completeOnNextSpin = false; private Tweener[] activeSpinTweens = new Tweener[2];
    private EventManager eventManager;
    private int lastTopDummyCount = 0; private int lastBottomDummyCount = 0;

    // --- New: persistent per-reel dummy pool ---
    private List<GameSymbol> allDummySymbols = new List<GameSymbol>();
    private HashSet<GameSymbol> allocatedDummySet = new HashSet<GameSymbol>();
    private Transform dummyContainer;

    public void InitializeReel(ReelData data, int reelID, EventManager slotsEventManager, ReelStripDefinition stripDefinition, SlotsEngine owner)
    {
        currentReelData = data; id = reelID; eventManager = slotsEventManager; reelStrip = stripDefinition.CreateInstance(); ownerEngine = owner;
        reelStrip.ResetSpinCounts();
        EnsureRootsCreated(); SpawnReel(currentReelData.CurrentSymbolData);
    }
    public void InitializeReel(ReelData data, int reelID, EventManager slotsEventManager, ReelStripData stripData, SlotsEngine owner)
    {
        currentReelData = data; id = reelID; eventManager = slotsEventManager; reelStrip = stripData; ownerEngine = owner;
        reelStrip.ResetSpinCounts();
        EnsureRootsCreated(); SpawnReel(currentReelData.CurrentSymbolData);
    }

    public SymbolData GetRandomSymbolFromStrip() => reelStrip.GetWeightedSymbol();
    public SymbolData GetRandomSymbolFromStrip(List<SymbolData> existingSelections) => reelStrip.GetWeightedSymbol(existingSelections);

    private void EnsureRootsCreated()
    {
        if (symbolRoot == null)
        {
            symbolRoot = new GameObject("SymbolRoot").transform; symbolRoot.parent = transform; symbolRoot.localScale = Vector3.one; symbolRoot.localPosition = Vector3.zero;
        }
        if (nextSymbolsRoot == null)
        {
            nextSymbolsRoot = new GameObject("NextSymbolsRoot").transform; nextSymbolsRoot.parent = transform; nextSymbolsRoot.localScale = Vector3.one; nextSymbolsRoot.localPosition = Vector3.zero;
        }
        if (dummyContainer == null)
        {
            dummyContainer = new GameObject("DummyPool").transform; dummyContainer.parent = transform; dummyContainer.localScale = Vector3.one; dummyContainer.localPosition = Vector3.zero;
        }
    }

    private void SpawnReel(List<SymbolData> existingSymbolData)
    {
        ReleaseAllSymbolsInRoot(symbolRoot); symbols.Clear();
        // Note: Do NOT release dummy instances here - they live in the persistent pool
        topDummySymbols.Clear(); bottomDummySymbols.Clear();
        float step = currentReelData.SymbolSpacing + currentReelData.SymbolSize; var selectedForThisReel = new List<SymbolData>();
        for (int i = 0; i < currentReelData.SymbolCount; i++)
        {
            GameSymbol sym = GameSymbolPool.Instance.Get(symbolRoot);
            SymbolData newSymbol = (existingSymbolData is { Count: > 0 }) ? existingSymbolData[i] : reelStrip.GetWeightedSymbol(selectedForThisReel);
            sym.InitializeSymbol(newSymbol, eventManager); sym.SetSizeAndLocalY(currentReelData.SymbolSize, step * i);
            symbols.Add(sym); if (newSymbol != null) selectedForThisReel.Add(newSymbol);
        }
        GenerateActiveDummies(selectedForThisReel);
    }

    private void ComputeDummyCounts(out int topCount, out int bottomCount)
    {
        int baseTop = currentReelData.SymbolCount; int baseBottom = currentReelData.SymbolCount - 1; if (baseBottom < 0) baseBottom = 0;
        int bufferSteps = 3; // bounce/spin buffer
        float step = currentReelData.SymbolSpacing + currentReelData.SymbolSize; float thisActiveHeight = currentReelData.SymbolCount * step; float maxActiveHeight = thisActiveHeight;
        if (ownerEngine != null)
        {
            foreach (var r in ownerEngine.CurrentReels)
            {
                if (r?.CurrentReelData == null) continue; float s = r.CurrentReelData.SymbolSpacing + r.CurrentReelData.SymbolSize; float h = r.CurrentReelData.SymbolCount * s; if (h > maxActiveHeight) maxActiveHeight = h;
            }
        }
        float ratio = thisActiveHeight > 0f ? maxActiveHeight / thisActiveHeight : 1f; if (ratio < 1f) ratio = 1f; if (ratio > 1.25f) { baseTop = Mathf.CeilToInt(baseTop * ratio); baseBottom = Mathf.CeilToInt(baseBottom * ratio); }
        topCount = baseTop + bufferSteps; bottomCount = baseBottom + bufferSteps; int maxClamp = currentReelData.SymbolCount * 6 + 12; if (topCount > maxClamp) topCount = maxClamp; if (bottomCount > maxClamp) bottomCount = maxClamp;
    }

    private void RecomputeAllPositions()
    {
        float step = currentReelData.SymbolSpacing + currentReelData.SymbolSize;
        for (int i = 0; i < symbols.Count; i++) { var gs = symbols[i]; if (gs == null) continue; gs.SetSizeAndLocalY(currentReelData.SymbolSize, step * i); }
        for (int i = 0; i < bottomDummySymbols.Count; i++) { var gs = bottomDummySymbols[i]; if (gs == null) continue; gs.SetSizeAndLocalY(currentReelData.SymbolSize, -step * (i + 1)); }
        int activeCount = currentReelData.SymbolCount; for (int i = 0; i < topDummySymbols.Count; i++) { var gs = topDummySymbols[i]; if (gs == null) continue; gs.SetSizeAndLocalY(currentReelData.SymbolSize, step * (activeCount + i)); }

        // Position buffer lists too if present
        for (int i = 0; i < bufferBottomDummySymbols.Count; i++) { var gs = bufferBottomDummySymbols[i]; if (gs == null) continue; gs.SetSizeAndLocalY(currentReelData.SymbolSize, -step * (i + 1)); }
        for (int i = 0; i < bufferTopDummySymbols.Count; i++) { var gs = bufferTopDummySymbols[i]; if (gs == null) continue; gs.SetSizeAndLocalY(currentReelData.SymbolSize, step * (activeCount + i)); }
    }

    private void GenerateActiveDummies(List<SymbolData> existingSelections)
    {
        ComputeDummyCounts(out int topCount, out int bottomCount); lastTopDummyCount = topCount; lastBottomDummyCount = bottomCount;
        // Ensure pool capacity covers active dummies
        EnsureDummyPoolSize(topCount + bottomCount);

        // Release any currently allocated active dummies back to pool (they will be reallocated)
        foreach (var d in topDummySymbols) ReleaseDummy(d);
        foreach (var d in bottomDummySymbols) ReleaseDummy(d);
        topDummySymbols.Clear(); bottomDummySymbols.Clear();

        // Use non-consuming selection for dummy symbols to avoid draining reserved counts prematurely.
        // Allocate bottom dummies first (positions below)
        for (int i = 0; i < bottomCount; i++)
        {
            var sym = AcquireFreeDummy();
            if (sym == null) // defensive fallback
            {
                sym = GameSymbolPool.Instance.Get(dummyContainer);
                sym.InitializeSymbol(reelStrip.GetWeightedSymbol(existingSelections, false), eventManager);
                allDummySymbols.Add(sym);
                allocatedDummySet.Add(sym);
            }
            SymbolData def = reelStrip.GetWeightedSymbol(existingSelections, false);
            sym.ApplySymbol(def);
            sym.transform.SetParent(symbolRoot, false);
            float step = currentReelData.SymbolSpacing + currentReelData.SymbolSize;
            float y = -step * (i + 1);
            sym.SetSizeAndLocalY(currentReelData.SymbolSize, y);
            var img = sym.CachedImage; if (img != null) { img.DOKill(); img.color = Color.gray; }
            if (def != null) existingSelections?.Add(def);
            bottomDummySymbols.Add(sym);
        }

        // Allocate top dummies
        int activeCount = currentReelData.SymbolCount;
        for (int i = 0; i < topCount; i++)
        {
            var sym = AcquireFreeDummy();
            if (sym == null)
            {
                sym = GameSymbolPool.Instance.Get(dummyContainer);
                sym.InitializeSymbol(reelStrip.GetWeightedSymbol(existingSelections, false), eventManager);
                allDummySymbols.Add(sym);
                allocatedDummySet.Add(sym);
            }
            SymbolData def = reelStrip.GetWeightedSymbol(existingSelections, false);
            sym.ApplySymbol(def);
            sym.transform.SetParent(symbolRoot, false);
            float step = currentReelData.SymbolSpacing + currentReelData.SymbolSize;
            float y = step * (activeCount + i);
            sym.SetSizeAndLocalY(currentReelData.SymbolSize, y);
            var img = sym.CachedImage; if (img != null) { img.DOKill(); img.color = Color.gray; }
            if (def != null) existingSelections?.Add(def);
            topDummySymbols.Add(sym);
        }

        RecomputeAllPositions();
    }

    private float ComputeFallOffsetY()
    {
        float step = currentReelData.SymbolSpacing + currentReelData.SymbolSize; int topCount = lastTopDummyCount > 0 ? lastTopDummyCount : currentReelData.SymbolCount; return step * (currentReelData.SymbolCount + topCount);
    }

    // New helper: stop any pre-existing tweens and reset visuals before spinning
    private void ClearPreSpinTweens()
    {
        void ClearList(List<GameSymbol> list)
        {
            if (list == null) return;
            foreach (var g in list)
            {
                if (g == null) continue;
                // Stop any active tweens and kill DOTween tweens targeting this symbol
                g.StopAndClearTweens();
                // Ensure image color/alpha reset to full white
                var img = g.CachedImage;
                if (img != null)
                {
                    img.DOKill();
                    img.color = Color.white;
                }
            }
        }

        ClearList(symbols);
        ClearList(topDummySymbols);
        ClearList(bottomDummySymbols);
        ClearList(bufferSymbols);
        ClearList(bufferTopDummySymbols);
        ClearList(bufferBottomDummySymbols);
    }

    public void BeginSpin(List<SymbolData> solution = null, float startDelay = 0f)
    {
        reelStrip.ResetSpinCounts(); // start fresh spin counts
        completeOnNextSpin = false; DOTween.Sequence().AppendInterval(startDelay).AppendCallback(() => { ClearPreSpinTweens(); BounceReel(Vector3.up, strength: 50f, peak: 0.8f, duration: 0.25f, onComplete: () => { FallOut(solution, true); spinning = true; eventManager.BroadcastEvent(SlotsEvent.ReelSpinStarted, ID); }); });
    }
    public void StopReel(float delay = 0f) { DOTween.Sequence().AppendInterval(delay).AppendCallback(() => { completeOnNextSpin = true; if (activeSpinTweens[0] != null) activeSpinTweens[0].timeScale = 4f; if (activeSpinTweens[1] != null) activeSpinTweens[1].timeScale = 4f; }); }

    private void SpawnNextReel(List<SymbolData> solution = null)
    {
        // Release any existing symbols under the next root first
        ReleaseAllSymbolsInRoot(nextSymbolsRoot);

        // Prepare new symbols for the incoming reel
        List<GameSymbol> newSymbols = new List<GameSymbol>();
        float step = currentReelData.SymbolSpacing + currentReelData.SymbolSize;
        var combinedExisting = new List<SymbolData>();
        if (symbols != null)
            foreach (var gs in symbols)
                if (gs?.CurrentSymbolData != null)
                    combinedExisting.Add(gs.CurrentSymbolData);

        for (int i = 0; i < currentReelData.SymbolCount; i++)
        {
            GameSymbol symbol = GameSymbolPool.Instance.Get(nextSymbolsRoot);
            SymbolData def = solution != null ? solution[i] : reelStrip.GetWeightedSymbol(combinedExisting);
            symbol.InitializeSymbol(def, eventManager);
            symbol.SetSizeAndLocalY(currentReelData.SymbolSize, step * i);
            newSymbols.Add(symbol);
            if (def != null) combinedExisting.Add(def);
        }

        bufferSymbols = newSymbols;

        // Compute dummy counts for the buffer (so we can position the next root correctly)
        ComputeDummyCounts(out int topCount, out int bottomCount);

        // Spawn buffer dummies under the next root using persistent dummy pool
        // Ensure there is capacity for both active and buffer dummies
        EnsureDummyPoolSize(Math.Max(allDummySymbols.Count, topCount + bottomCount + lastTopDummyCount + lastBottomDummyCount));

        // Acquire buffer bottom dummies
        bufferBottomDummySymbols.Clear();
        var combinedForBuffer = new List<SymbolData>(combinedExisting);
        for (int i = 0; i < bottomCount; i++)
        {
            var sym = AcquireFreeDummy();
            if (sym == null)
            {
                sym = GameSymbolPool.Instance.Get(dummyContainer);
                sym.InitializeSymbol(reelStrip.GetWeightedSymbol(combinedForBuffer, false), eventManager);
                allDummySymbols.Add(sym);
                allocatedDummySet.Add(sym);
            }
            SymbolData def = reelStrip.GetWeightedSymbol(combinedForBuffer, false);
            sym.ApplySymbol(def);
            sym.transform.SetParent(nextSymbolsRoot, false);
            float y = -step * (i + 1);
            sym.SetSizeAndLocalY(currentReelData.SymbolSize, y);
            var img = sym.CachedImage; if (img != null) { img.DOKill(); img.color = Color.white; }
            if (def != null) combinedForBuffer?.Add(def);
            bufferBottomDummySymbols.Add(sym);
        }

        // Acquire buffer top dummies
        bufferTopDummySymbols.Clear();
        for (int i = 0; i < topCount; i++)
        {
            var sym = AcquireFreeDummy();
            if (sym == null)
            {
                sym = GameSymbolPool.Instance.Get(dummyContainer);
                sym.InitializeSymbol(reelStrip.GetWeightedSymbol(combinedForBuffer, false), eventManager);
                allDummySymbols.Add(sym);
                allocatedDummySet.Add(sym);
            }
            SymbolData def = reelStrip.GetWeightedSymbol(combinedForBuffer, false);
            sym.ApplySymbol(def);
            sym.transform.SetParent(nextSymbolsRoot, false);
            float y = step * (currentReelData.SymbolCount + i);
            sym.SetSizeAndLocalY(currentReelData.SymbolSize, y);
            var img = sym.CachedImage; if (img != null) { img.DOKill(); img.color = Color.white; }
            if (def != null) combinedForBuffer?.Add(def);
            bufferTopDummySymbols.Add(sym);
        }

        // Position using rect extents so the vertical gap between strips equals the symbol spacing
        float separation = currentReelData.SymbolSpacing; // gap between groups should equal the in-group spacing

        // Find highest top edge of existing symbols (in symbolRoot local space)
        float highestExistingTop = float.MinValue;
        if (symbolRoot != null)
        {
            for (int i = 0; i < symbolRoot.childCount; i++)
            {
                var c = symbolRoot.GetChild(i);
                if (c == null) continue;
                RectTransform rt = null;
                if (c.TryGetComponent<GameSymbol>(out var gs) && gs != null) rt = gs.CachedRect;
                if (rt == null) rt = c as RectTransform ?? c.GetComponent<RectTransform>();
                if (rt == null) continue;
                float top = c.localPosition.y + (rt.rect.height * (1f - rt.pivot.y));
                if (top > highestExistingTop) highestExistingTop = top;
            }
        }
        if (highestExistingTop == float.MinValue) highestExistingTop = 0f;

        // Find lowest bottom edge of incoming symbols (in nextSymbolsRoot local space)
        float lowestIncomingBottom = float.MaxValue;
        if (nextSymbolsRoot != null)
        {
            for (int i = 0; i < nextSymbolsRoot.childCount; i++)
            {
                var c = nextSymbolsRoot.GetChild(i);
                if (c == null) continue;
                RectTransform rt = null;
                if (c.TryGetComponent<GameSymbol>(out var gs) && gs != null) rt = gs.CachedRect;
                if (rt == null) rt = c as RectTransform ?? c.GetComponent<RectTransform>();
                if (rt == null) continue;
                float bottom = c.localPosition.y - (rt.rect.height * rt.pivot.y);
                if (bottom < lowestIncomingBottom) lowestIncomingBottom = bottom;
            }
        }
        if (lowestIncomingBottom == float.MaxValue) lowestIncomingBottom = 0f;

        // Compute required next root local Y so that the gap between groups equals 'separation' (symbolSpacing)
        float symbolRootY = symbolRoot != null ? symbolRoot.localPosition.y : 0f;
        float requiredNextLocalY = symbolRootY + highestExistingTop - lowestIncomingBottom + separation;

        nextSymbolsRoot.localPosition = new Vector3(0f, requiredNextLocalY, 0f);
    }

    private List<GameSymbol> SpawnDummySymbols(Transform root, bool bottom, int count, bool dim, List<SymbolData> existingSelections, bool consume)
    {
        // This method kept for compatibility but now backed by persistent dummy pool logic.
        List<GameSymbol> dummies = new List<GameSymbol>(); if (count <= 0) return dummies; float step = currentReelData.SymbolSpacing + currentReelData.SymbolSize; int startIndex = bottom ? 1 : currentReelData.SymbolCount; int flip = bottom ? -1 : 1; Color dimColor = new Color(0.5f, 0.5f, 0.5f);

        for (int i = 0; i < count; i++)
        {
            var sym = AcquireFreeDummy();
            if (sym == null)
            {
                sym = GameSymbolPool.Instance.Get(root);
                sym.InitializeSymbol(reelStrip.GetWeightedSymbol(existingSelections, consume), eventManager);
                allDummySymbols.Add(sym);
                allocatedDummySet.Add(sym);
            }
            SymbolData def = reelStrip.GetWeightedSymbol(existingSelections, consume);
            sym.ApplySymbol(def);
            float y = (step * (i + startIndex)) * flip;
            sym.SetSizeAndLocalY(currentReelData.SymbolSize, y);
            sym.transform.SetParent(root, false);
            var img = sym.CachedImage; if (img != null) { img.DOKill(); img.color = dim ? dimColor : Color.white; }
            if (def != null) existingSelections?.Add(def);
            dummies.Add(sym);
        }
        return dummies;
    }

    private bool sequenceA = false, sequenceB = false;
    public void FallOut(List<SymbolData> solution = null, bool kickback = false)
    {
        ResetDimmedSymbols(); SpawnNextReel(solution); sequenceA = false; sequenceB = false; float fallDistance = -nextSymbolsRoot.transform.localPosition.y; float duration = currentReelData.ReelSpinDuration;
        activeSpinTweens[0] = symbolRoot.transform.DOLocalMoveY(fallDistance, duration).OnComplete(() => { sequenceA = true; CheckBeginLandingBounce(solution); }).SetEase(Ease.Linear);
        activeSpinTweens[1] = nextSymbolsRoot.transform.DOLocalMoveY(0, duration).OnComplete(() => { sequenceB = true; CheckBeginLandingBounce(solution); }).SetEase(Ease.Linear);
    }
    private void CheckBeginLandingBounce(List<SymbolData> solution) { if (sequenceA && sequenceB) { sequenceA = false; sequenceB = false; if (completeOnNextSpin) BounceReel(Vector3.down, peak: 0.25f, duration: 0.25f, onComplete: () => CompleteReelSpin(solution)); else CompleteReelSpin(solution); } }
    private void BounceReel(Vector3 direction, float strength = 100f, float duration = 0.5f, float sharpness = 0f, float peak = 0.4f, Action onComplete = null)
    { if (nextSymbolsRoot != null) nextSymbolsRoot.DOPulseUp(direction, strength, duration, sharpness, peak).SetEase(Ease.Linear); symbolRoot.DOPulseUp(direction, strength, duration, sharpness, peak).SetEase(Ease.Linear).OnComplete(() => { onComplete?.Invoke(); }); }
    private void CompleteReelSpin(List<SymbolData> solution)
    {
        ReleaseAllSymbolsInRoot(symbolRoot); var old = symbolRoot; symbolRoot = nextSymbolsRoot; nextSymbolsRoot = old; float offsetY = ComputeFallOffsetY(); nextSymbolsRoot.localPosition = new Vector3(0, offsetY, 0);
        if (bufferSymbols.Count > 0) { symbols = bufferSymbols; bufferSymbols = new List<GameSymbol>(); }
        if (bufferTopDummySymbols.Count > 0) { topDummySymbols = bufferTopDummySymbols; bufferTopDummySymbols = new List<GameSymbol>(); lastTopDummyCount = topDummySymbols.Count; }
        if (bufferBottomDummySymbols.Count > 0) { bottomDummySymbols = bufferBottomDummySymbols; bufferBottomDummySymbols = new List<GameSymbol>(); lastBottomDummyCount = bottomDummySymbols.Count; }
        if (!completeOnNextSpin) { FallOut(solution); }
        else { spinning = false; if ((Application.isEditor || Debug.isDebugBuild) && WinEvaluator.Instance != null && WinEvaluator.Instance.LoggingEnabled) { var names = symbols.Select(s => s?.CurrentSymbolData != null ? s.CurrentSymbolData.Name : "(null)").ToArray(); Debug.Log($"Reel {ID} landed symbols (bottom->top): [{string.Join(",", names)}]"); } for (int i = 0; i < symbols.Count; i++) eventManager.BroadcastEvent(SlotsEvent.SymbolLanded, symbols[i]); eventManager.BroadcastEvent(SlotsEvent.ReelCompleted, ID); }
    }

    public void DimDummySymbols() { Color dim = new Color(0.5f, 0.5f, 0.5f); foreach (GameSymbol g in topDummySymbols) { var img = g.CachedImage; if (img != null) img.DOColor(dim, 0.1f); } foreach (GameSymbol g in bottomDummySymbols) { var img = g.CachedImage; if (img != null) img.DOColor(dim, 0.1f); } }
    public void ResetDimmedSymbols() { foreach (GameSymbol g in topDummySymbols) { var image = g.CachedImage; if (image == null) continue; image.DOKill(); image.color = Color.white; } foreach (GameSymbol g in bottomDummySymbols) { var image = g.CachedImage; if (image == null) continue; image.DOKill(); image.color = Color.white; } }

    public void UpdateSymbolLayout(float newSymbolSize, float newSpacing)
    {
        if (currentReelData == null) return;
        currentReelData.SetSymbolSize(newSymbolSize, newSpacing);
        RecomputeAllPositions();
        RegenerateDummies();
        float offsetY = ComputeFallOffsetY();
        if (nextSymbolsRoot != null) nextSymbolsRoot.localPosition = new Vector3(0, offsetY, 0);
        if (!spinning) DimDummySymbols();
    }

    public void RegenerateDummies()
    {
        if (symbolRoot == null) return;

        // Return active dummies to pool (do not release to global pool - keep persistent per-reel)
        foreach (var d in topDummySymbols)
            if (d != null) ReleaseDummy(d);
        topDummySymbols.Clear();

        foreach (var d in bottomDummySymbols)
            if (d != null) ReleaseDummy(d);
        bottomDummySymbols.Clear();

        var existing = currentReelData.CurrentSymbolData != null ? new List<SymbolData>(currentReelData.CurrentSymbolData) : new List<SymbolData>();
        GenerateActiveDummies(existing);

        float offsetY = ComputeFallOffsetY();
        if (nextSymbolsRoot != null && !spinning) nextSymbolsRoot.localPosition = new Vector3(0, offsetY, 0);
        if (!spinning) DimDummySymbols();
    }

    private void ReleaseAllSymbolsInRoot(Transform root)
    {
        if (root == null) return;

        // Collect children first to avoid modifying while iterating
        var children = new List<Transform>();
        for (int i = 0; i < root.childCount; i++) children.Add(root.GetChild(i));

        foreach (var child in children)
        {
            if (child == null) continue;
            if (child.TryGetComponent<GameSymbol>(out var symbol))
            {
                // remove any stale references to this symbol from internal lists before releasing
                RemoveFromAllLists(symbol);

                // If this symbol is part of the persistent per-reel dummy pool, return it to the dummy container
                if (allDummySymbols != null && allDummySymbols.Contains(symbol))
                {
                    // mark it free and reparent to dummy container rather than releasing to global pool
                    ReleaseDummy(symbol);
                }
                else
                {
                    GameSymbolPool.Instance.Release(symbol);
                }
            }
            else
            {
                GameObject.Destroy(child.gameObject);
            }
        }
    }

    // Remove symbol references from all tracked lists to avoid stale references pointing to pooled instances
    private void RemoveFromAllLists(GameSymbol symbol)
    {
        if (symbol == null) return;

        // Use RemoveAll to handle potential multiple occurrences (defensive)
        symbols.RemoveAll(s => s == symbol);
        topDummySymbols.RemoveAll(s => s == symbol);
        bottomDummySymbols.RemoveAll(s => s == symbol);
        bufferSymbols.RemoveAll(s => s == symbol);
        bufferTopDummySymbols.RemoveAll(s => s == symbol);
        bufferBottomDummySymbols.RemoveAll(s => s == symbol);

        // Also ensure it is not allocated in our allocation set
        if (allocatedDummySet.Contains(symbol)) allocatedDummySet.Remove(symbol);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
// Validation helper to detect if the same GameSymbol instance exists in both roots (debugging only)
private void ValidateNoSharedInstances()
{
    if (symbolRoot == null || nextSymbolsRoot == null) return;

    var currentSet = new HashSet<GameSymbol>(symbols.Where(s => s != null));
    var bufferSet = new HashSet<GameSymbol>(bufferSymbols.Where(s => s != null));

    foreach (var shared in currentSet.Intersect(bufferSet))
    {
        Debug.LogWarning($"GameReel {ID}: GameSymbol instance present in both symbolRoot and nextSymbolsRoot: {shared.name}");
    }
}
#endif

    public void SetSymbolCount(int newCount, bool incremental = true)
    {
        if (newCount < 1) newCount = 1;
        if (spinning) { Debug.LogWarning("Cannot change symbol count while reel is spinning."); return; }
        int oldCount = currentReelData.SymbolCount;
        if (newCount == oldCount) return;

        // Check if this change will affect other reels' dummy counts
        bool affectsOtherReels = WillHeightChangeAffectOtherReels(oldCount, newCount);

        currentReelData.SetSymbolCount(newCount);
        var dataList = currentReelData.CurrentSymbolData ?? new List<SymbolData>();
        if (dataList.Count > newCount) dataList.RemoveRange(newCount, dataList.Count - newCount);
        else if (dataList.Count < newCount) { for (int i = dataList.Count; i < newCount; i++) dataList.Add(reelStrip.GetWeightedSymbol(dataList)); }
        currentReelData.SetCurrentSymbolData(dataList);
        if (!incremental) { SpawnReel(currentReelData.CurrentSymbolData); SlotsEngineManager.Instance.AdjustSlotCanvas(ownerEngine); return; }
        if (newCount > oldCount) { for (int i = oldCount; i < newCount; i++) { GameSymbol sym = GameSymbolPool.Instance.Get(symbolRoot); SymbolData def = (currentReelData.CurrentSymbolData.Count > i) ? currentReelData.CurrentSymbolData[i] : reelStrip.GetWeightedSymbol(currentReelData.CurrentSymbolData); sym.InitializeSymbol(def, eventManager); symbols.Add(sym); } }
        else { for (int i = oldCount - 1; i >= newCount; i--) { if (i < 0 || i >= symbols.Count) continue; var sym = symbols[i]; if (sym != null) GameSymbolPool.Instance.Release(sym); symbols.RemoveAt(i); } }
        RecomputeAllPositions();

        if (affectsOtherReels)
        {
            // First regenerate dummies for all reels (since max height changed)
            if (ownerEngine != null) ownerEngine.RegenerateAllReelDummiesForHeightChange();
            // Then trigger layout rescaling through the manager (which will NOT regenerate dummies again)
            if (SlotsEngineManager.Instance != null) SlotsEngineManager.Instance.AdjustSlotCanvasForHeightChange(ownerEngine);
        }
        else
        {
            // Only regenerate this reel's dummies - no rescaling needed
            RegenerateDummies();
        }
    }

    /// <summary>
    /// Determines if changing from oldCount to newCount will affect other reels' dummy counts.
    /// This happens when:
    /// 1. This reel was the tallest and is getting shorter
    /// 2. This reel is becoming the tallest
    /// </summary>
    private bool WillHeightChangeAffectOtherReels(int oldCount, int newCount)
    {
        if (ownerEngine == null || ownerEngine.CurrentReels == null) return false;

        float step = currentReelData.SymbolSpacing + currentReelData.SymbolSize;
        float oldHeight = oldCount * step;
        float newHeight = newCount * step;

        // Find the current max height among all reels
        float currentMaxHeight = 0f;
        bool thisReelWasTallest = false;

        foreach (var r in ownerEngine.CurrentReels)
        {
            if (r == null || r.CurrentReelData == null) continue;
            float s = r.CurrentReelData.SymbolSpacing + r.CurrentReelData.SymbolSize;
            float h = r.CurrentReelData.SymbolCount * s;
            if (h > currentMaxHeight) currentMaxHeight = h;
            if (r == this && Mathf.Approximately(h, currentMaxHeight)) thisReelWasTallest = true;
        }

        // If this reel was the tallest and is getting shorter, other reels might need fewer dummies
        if (thisReelWasTallest && newHeight < oldHeight) return true;

        // If this reel is becoming taller than the current max, other reels need more dummies
        if (newHeight > currentMaxHeight) return true;

        return false;
    }

    // --- Dummy pool helpers ---
    private void EnsureDummyPoolSize(int totalNeeded)
    {
        if (totalNeeded <= 0) return;
        // if already sufficient, nothing to do
        if (allDummySymbols.Count >= totalNeeded) return;
        EnsureRootsCreated();
        int toCreate = totalNeeded - allDummySymbols.Count;
        for (int i = 0; i < toCreate; i++)
        {
            var sym = GameSymbolPool.Instance.Get(dummyContainer);
            // initialize with a harmless non-consuming pick so the symbol has a sprite
            SymbolData def = reelStrip != null ? reelStrip.GetWeightedSymbol(new List<SymbolData>(), false) : null;
            if (def != null) sym.InitializeSymbol(def, eventManager); else sym.InitializeSymbol(reelStrip?.GetWeightedSymbol(null), eventManager);
            sym.SetSizeAndLocalY(currentReelData != null ? currentReelData.SymbolSize : 100, 0);
            allDummySymbols.Add(sym);
            // newly created symbol is free until allocated
            if (allocatedDummySet.Contains(sym)) allocatedDummySet.Remove(sym);
        }
    }

    private GameSymbol AcquireFreeDummy()
    {
        for (int i = 0; i < allDummySymbols.Count; i++)
        {
            var s = allDummySymbols[i];
            if (s == null) continue;
            if (!allocatedDummySet.Contains(s))
            {
                allocatedDummySet.Add(s);
                return s;
            }
        }
        return null;
    }

    private void ReleaseDummy(GameSymbol s)
    {
        if (s == null) return;
        allocatedDummySet.Remove(s);
        // return to dummy container so it's out of the way until reused
        if (dummyContainer != null) s.transform.SetParent(dummyContainer, true);
    }
}