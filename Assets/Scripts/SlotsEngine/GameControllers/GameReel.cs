using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Collections;

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
    private bool completeOnNextSpin = false; private Coroutine[] activeSpinCoroutines = new Coroutine[2];
    private EventManager eventManager;
    private int lastTopDummyCount = 0; private int lastBottomDummyCount = 0;

    // spin speed multiplier used to accelerate the coroutine-based move when Stop is requested
    private float spinSpeedMultiplier = 1f;

    // --- New: persistent per-reel pooled symbols (dummies + active + buffer)
    private List<GameSymbol> allPooledSymbols = new List<GameSymbol>();
    // Added: hash set mirror for O(1) membership checks (avoids linear .Contains in tight loops)
    private HashSet<GameSymbol> allPooledSet = new HashSet<GameSymbol>();
    private HashSet<GameSymbol> allocatedPooledSet = new HashSet<GameSymbol>();
    // Free-list stack to avoid linear scans when allocating
    private Stack<GameSymbol> freePooledStack = new Stack<GameSymbol>();
    private Transform dummyContainer;

    // Cached singleton references to avoid repeated Instance lookups
    private GameSymbolPool cachedSymbolPool;

    // Helper to resolve persisted SymbolData candidate, attempt to recover missing sprite by name,
    // and persist into SymbolDataManager when possible. Falls back to reelStrip selection when
    // candidate is null or sprite could not be resolved.
    private SymbolData ResolveAndPersistSymbol(SymbolData candidate, List<SymbolData> existingSelections)
    {
        SymbolData result = candidate;
        try
        {
            if (result != null)
            {
                if (result.Sprite == null && !string.IsNullOrEmpty(result.Name))
                {
                    try { result.Sprite = AssetResolver.ResolveSprite(result.Name); } catch { }
                }
                if (result.Sprite == null)
                {
                    // fallback to strip selection
                    result = reelStrip != null ? reelStrip.GetWeightedSymbol(existingSelections) : result;
                }
                else
                {
                    try { if (SymbolDataManager.Instance != null && result.AccessorId == 0) SymbolDataManager.Instance.AddNewData(result); } catch { }
                }
            }
            else
            {
                result = reelStrip != null ? reelStrip.GetWeightedSymbol(existingSelections) : null;
            }
        }
        catch { /* swallow - caller expects best-effort resolution */ }
        return result;
    }

    // Coroutine handles for delayed operations to avoid DOTween.Sequence allocations
    private Coroutine beginSpinCoroutine;
    private Coroutine stopReelCoroutine;

    // Pending solution holder to avoid allocating closures for per-tween OnComplete
    private List<SymbolData> pendingLandingSolution;

    // Landing completion counter to wait for both tweens to finish (safer than single OnComplete)
    private int landingPendingCount = 0;

    // Shared empty list to avoid transient allocations where a non-consuming empty selection is required
    private static readonly List<SymbolData> s_emptySelection = new List<SymbolData>(0);

    // Per-instance temporary lists reused across calls to avoid frequent allocations during spins
    private List<SymbolData> tmpSelectedForThisReel = new List<SymbolData>();
    private List<SymbolData> tmpCombinedExisting = new List<SymbolData>();
    private List<SymbolData> tmpCombinedForBuffer = new List<SymbolData>();
    // Reuse list for next reel symbols to avoid per-spin allocation
    private List<GameSymbol> tmpNextReelSymbols = new List<GameSymbol>();

    private const float ResumeStaggerStep = 0.025f; // NEW: per-index stagger applied when resuming from page suspension
    private const float KickupDuration = 0.25f; // NEW: unified duration used for initial kickup and landing bounce timing

    public void InitializeReel(ReelData data, int reelID, EventManager slotsEventManager, ReelStripDefinition stripDefinition, SlotsEngine owner)
    {
        currentReelData = data; id = reelID; eventManager = slotsEventManager; reelStrip = stripDefinition.CreateInstance(); ownerEngine = owner;
        cachedSymbolPool = GameSymbolPool.Instance;
        reelStrip.ResetSpinCounts();
        EnsureRootsCreated();
        int est = EstimateNeededPooledCapacity();
        EnsurePooledSymbolCapacity(est);
        SpawnReel(currentReelData.CurrentSymbolData);
    }
    public void InitializeReel(ReelData data, int reelID, EventManager slotsEventManager, ReelStripData stripData, SlotsEngine owner)
    {
        currentReelData = data; id = reelID; eventManager = slotsEventManager; reelStrip = stripData; ownerEngine = owner;
        cachedSymbolPool = GameSymbolPool.Instance;
        reelStrip.ResetSpinCounts();
        EnsureRootsCreated();
        int est = EstimateNeededPooledCapacity();
        EnsurePooledSymbolCapacity(est);
        SpawnReel(currentReelData.CurrentSymbolData);
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
        // Ensure we have at least enough pooled symbols for the visible rows
        EnsurePooledSymbolCapacity(currentReelData != null ? currentReelData.SymbolCount : 0);
        float step = currentReelData.SymbolSpacing + currentReelData.SymbolSize;

        tmpSelectedForThisReel.Clear();
        for (int i = 0; i < currentReelData.SymbolCount; i++)
        {
            // Acquire a pooled symbol (creates enough if necessary). Caller will apply/initialize as appropriate.
            GameSymbol sym = AcquireFreePooledSymbol();
            if (sym == null)
            {
                // As a last-resort ensure capacity and try again
                EnsurePooledSymbolCapacity(currentReelData != null ? currentReelData.SymbolCount : 1);
                sym = AcquireFreePooledSymbol();
                if (sym == null)
                {
                    // Fallback to global pool or create directly to avoid throwing NRE
                    sym = CreateSymbolInstance(symbolRoot);
                }
            }

            // Resolve persisted candidate or pick from strip
            SymbolData candidate = (existingSymbolData != null && existingSymbolData.Count > i) ? existingSymbolData[i] : null;
            SymbolData newSymbol = ResolveAndPersistSymbol(candidate, tmpSelectedForThisReel);

            // If this symbol was freshly created it has been initialized. Apply the new data for reuse.
            if (sym != null) sym.ApplySymbol(newSymbol);
            // Ensure visible symbols are shown with full color (not dimmed)
            var visImg = sym?.CachedImage; if (visImg != null) visImg.color = Color.white;
            if (sym != null) sym.SetSizeAndLocalY(currentReelData.SymbolSize, step * i);
            // ensure proper parenting
            if (sym != null) sym.transform.SetParent(symbolRoot, false);
            symbols.Add(sym); if (newSymbol != null) tmpSelectedForThisReel.Add(newSymbol);
        }
        GenerateActiveDummies(tmpSelectedForThisReel);
    }

    private void ComputeDummyCounts(out int topCount, out int bottomCount)
    {
        // Default safe values
        topCount = 0;
        bottomCount = 0;

        if (currentReelData == null)
        {
            return;
        }

        // Determine the pitch (height) of one symbol including spacing
        float symbolPitch = Mathf.Max(1f, currentReelData.SymbolSize + currentReelData.SymbolSpacing);

        // Try to find the visible height for this reel. Prefer the nearest RectTransform parent that likely represents the reel viewport.
        float visibleHeight = 0f;
        try
        {
            if (symbolRoot != null)
            {
                var rt = symbolRoot.GetComponentInParent<RectTransform>();
                if (rt != null)
                {
                    visibleHeight = Mathf.Abs(rt.rect.height);
                }
            }
            // Fallback to owner engine's reels root transform (if present)
            if (visibleHeight <= 0f && ownerEngine != null)
            {
                var ownerRt = ownerEngine.ReelsRootTransform as RectTransform;
                if (ownerRt != null) visibleHeight = Mathf.Abs(ownerRt.rect.height);
            }
        }
        catch { visibleHeight = 0f; }

        // If we couldn't determine a visible height, fallback to using configured symbol count
        if (visibleHeight <= 0f)
        {
            int configured = Mathf.Max(1, currentReelData.SymbolCount);
            // leave room for a couple extra dummies to avoid gaps during motion
            int totalNeeded = configured + 4;
            topCount = totalNeeded;
            bottomCount = totalNeeded;
            lastTopDummyCount = topCount;
            lastBottomDummyCount = bottomCount;
            return;
        }

        // Compute how many symbols are required to cover visible height plus a safety buffer on each side
        int visibleRows = Mathf.CeilToInt(visibleHeight / symbolPitch);
        int safetyBuffer = 2; // extra rows above and below to ensure continuous coverage during movement
        int totalPerSide = visibleRows + safetyBuffer;

        // Distribute into top and bottom; make them equal for simplicity
        topCount = totalPerSide;
        bottomCount = totalPerSide;

        // Ensure we never shrink too aggressively compared to last known (avoid popping)
        if (lastTopDummyCount > 0) topCount = Mathf.Max(topCount, lastTopDummyCount);
        if (lastBottomDummyCount > 0) bottomCount = Mathf.Max(bottomCount, lastBottomDummyCount);

        lastTopDummyCount = topCount;
        lastBottomDummyCount = bottomCount;
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
        // Ensure capacity for visible symbols + active dummies
        int visible = currentReelData != null ? currentReelData.SymbolCount : 0;
        EnsurePooledSymbolCapacity(visible + topCount + bottomCount);

        // Release any currently allocated active dummies back to pool (they will be reallocated)
        foreach (var d in topDummySymbols) ReleasePooledSymbol(d);
        foreach (var d in bottomDummySymbols) ReleasePooledSymbol(d);
        topDummySymbols.Clear(); bottomDummySymbols.Clear();

        // Calculate step size for positioning
        float step = currentReelData.SymbolSpacing + currentReelData.SymbolSize;

        // Allocate bottom dummies first (positions below)
        for (int i = 0; i < bottomCount; i++)
        {
            var sym = AcquireFreePooledSymbol();
            if (sym == null) // defensive fallback
            {
                sym = cachedSymbolPool != null ? cachedSymbolPool.Get(dummyContainer) : GameSymbolPool.Instance?.Get(dummyContainer);
                try { sym?.SetOwnerReel(this); } catch { }
                SymbolData initDef = ResolveAndPersistSymbol(null, existingSelections);
                sym.InitializeSymbol(initDef, eventManager);
                // Ensure it's hidden until allocated properly
                sym.gameObject.SetActive(false);
                allPooledSymbols.Add(sym); allPooledSet.Add(sym);
                allocatedPooledSet.Add(sym);
            }
            SymbolData def = ResolveAndPersistSymbol(null, existingSelections);
            sym.ApplySymbol(def);
            sym.transform.SetParent(symbolRoot, false);
            float y = -step * (i + 1);
            sym.SetSizeAndLocalY(currentReelData.SymbolSize, y);
            // ensure dummy visuals are dimmed when not spinning to match presentation state
            var imgBottom = sym.CachedImage; if (imgBottom != null) { if (!spinning) imgBottom.color = new Color(0.5f, 0.5f, 0.5f); else imgBottom.color = Color.white; }
            if (def != null) existingSelections?.Add(def);
            bottomDummySymbols.Add(sym);
        }

        // Allocate top dummies
        int activeCount = currentReelData.SymbolCount;
        for (int i = 0; i < topCount; i++)
        {
            var sym = AcquireFreePooledSymbol();
            if (sym == null)
            {
                sym = cachedSymbolPool != null ? cachedSymbolPool.Get(dummyContainer) : GameSymbolPool.Instance?.Get(dummyContainer);
                try { sym?.SetOwnerReel(this); } catch { }
                SymbolData initDef = ResolveAndPersistSymbol(null, existingSelections);
                sym.InitializeSymbol(initDef, eventManager);
                sym.gameObject.SetActive(false);
                allPooledSymbols.Add(sym); allPooledSet.Add(sym);
                allocatedPooledSet.Add(sym);
            }
            SymbolData def = ResolveAndPersistSymbol(null, existingSelections);
            sym.ApplySymbol(def);
            sym.transform.SetParent(symbolRoot, false);
            float y = step * (activeCount + i);
            sym.SetSizeAndLocalY(currentReelData.SymbolSize, y);
            // ensure dummy visuals are dimmed when not spinning to match presentation state
            var imgTop = sym.CachedImage; if (imgTop != null) { if (!spinning) imgTop.color = new Color(0.5f, 0.5f, 0.5f); else imgTop.color = Color.white; }
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
            for (int i = 0; i < list.Count; i++)
            {
                var g = list[i];
                if (g == null) continue;
                // Stop any active tweens and kill DOTween tweens targeting this symbol
                g.StopAndClearTweens();
                // Ensure image color/alpha reset to full white
                var img = g.CachedImage;
                if (img != null)
                {
                    img.color = Color.white; // No tween kill needed if we no longer tween colors during spin
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

    // Public method to immediately begin spin without scheduling a coroutine (used by SlotsEngine single-coroutine stagger)
    public void BeginSpinImmediate(List<SymbolData> solution = null)
    {
        // Reset per-reel state that would normally be set in BeginSpin
        // Ensure per-reel pool is large enough before spin starts to prevent transient gaps
        int est = EstimateNeededPooledCapacity();
        EnsurePooledSymbolCapacity(est);
        reelStrip.ResetSpinCounts(); // start fresh spin counts

        // Cancel any pending stop so a leftover stop doesn't apply mid-spin
        if (stopReelCoroutine != null) { StopCoroutine(stopReelCoroutine); stopReelCoroutine = null; }

        // Clear any previous active coroutines to avoid interacting with stale references
        try
        {
            if (activeSpinCoroutines[0] != null) { StopCoroutine(activeSpinCoroutines[0]); activeSpinCoroutines[0] = null; }
        }
        catch { activeSpinCoroutines[0] = null; }
        try
        {
            if (activeSpinCoroutines[1] != null) { StopCoroutine(activeSpinCoroutines[1]); activeSpinCoroutines[1] = null; }
        }
        catch { activeSpinCoroutines[1] = null; }

        // reset speed multiplier
        spinSpeedMultiplier = 1f;

        // Always start in continuous mode unless an explicit StopReel sets this later
        completeOnNextSpin = false;

        ClearPreSpinTweens();

        if (IsEffectivelyPageActive())
        {
            // normal visual kickup
            // capture whether a stop was already requested before the kickup began so we can
            // decide whether to short-circuit on completion. This avoids treating a stop that
            // occurs after visible kickup as if it happened before the kickup.
            stopRequestedBeforeKickup = completeOnNextSpin;
            BounceReel(Vector3.up, strength: 50f, peak: 0.8f, duration: KickupDuration, onComplete: () =>
            {
                // Only treat this as "stop during kickup" when the stop was already requested
                // before the kickup began. If the stop was requested after kickup completed we
                // should proceed with the normal fallout flow and let landing logic handle completion.
                if (stopRequestedBeforeKickup && completeOnNextSpin)
                {
                    spinning = false;
                    // notify landed for visible symbols and mark reel completed so engine can progress
                    if (eventManager != null)
                    {
                        for (int i = 0; i < symbols.Count; i++)
                        {
                            try { eventManager.BroadcastEvent(SlotsEvent.SymbolLanded, symbols[i]); } catch { }
                        }
                        try { eventManager.BroadcastEvent(SlotsEvent.ReelCompleted, ID); } catch { }
                    }
                    stopRequestedBeforeKickup = false;
                    return;
                }

                stopRequestedBeforeKickup = false;
                spinning = true;
                FallOut(solution, true);
                eventManager.BroadcastEvent(SlotsEvent.ReelSpinStarted, ID);
            });
        }
        else
        {
            // skip tween, but preserve timing before broadcasting ReelSpinStarted and starting fallout (or suspending)
            StartCoroutine(SkipKickupDelay(solution));
        }
    }

    private IEnumerator SkipKickupDelay(List<SymbolData> solution)
    {
        // If the page is active we preserve the kickup delay so timing matches visible reels; if it's inactive
        // we skip the visual delay entirely to avoid deferred state transitions that have no visual effect.
        if (IsEffectivelyPageActive())
        {
            yield return new WaitForSeconds(KickupDuration);
        }

        // If a stop was requested before the kickup finished, complete immediately (no visual bounce) so
        // the engine doesn't wait on a reel that will never visibly animate.
        if (completeOnNextSpin)
        {
            spinning = false;
            if (eventManager != null)
            {
                for (int i = 0; i < symbols.Count; i++)
                {
                    try { eventManager.BroadcastEvent(SlotsEvent.SymbolLanded, symbols[i]); } catch { }
                }
                try { eventManager.BroadcastEvent(SlotsEvent.ReelCompleted, ID); } catch { }
            }
            yield break;
        }

        spinning = true;
        if (!IsEffectivelyPageActive())
        {
            pendingLandingSolution = solution; // store intended first solution
            suspendedAwaitingResume = true;
            if (waitForPageResumeCoroutine != null) StopCoroutine(waitForPageResumeCoroutine);
            waitForPageResumeCoroutine = StartCoroutine(WaitForInitialPageActivation());
        }
        else
        {
            FallOut(solution, true);
        }
        eventManager.BroadcastEvent(SlotsEvent.ReelSpinStarted, ID);
    }

    private IEnumerator WaitForInitialPageActivation()
    {
        // Wait until page becomes active OR a stop is requested before first FallOut
        while (!IsEffectivelyPageActive() && !completeOnNextSpin)
        {
            yield return null;
        }
        var sol = pendingLandingSolution;
        pendingLandingSolution = null;
        waitForPageResumeCoroutine = null;
        if (completeOnNextSpin)
        {
            // Stop requested before initial fallout: mark complete immediately
            spinning = false;
            for (int i = 0; i < symbols.Count; i++) eventManager.BroadcastEvent(SlotsEvent.SymbolLanded, symbols[i]);
            eventManager.BroadcastEvent(SlotsEvent.ReelCompleted, ID);
            suspendedAwaitingResume = false;
        }
        else
        {
            suspendedAwaitingResume = false;
            float delay = ID * ResumeStaggerStep;
            StartCoroutine(ResumeFallOutAfterDelay(sol, delay, true));
        }
    }

    private IEnumerator ResumeFallOutAfterDelay(List<SymbolData> solution, float delay, bool initialKickback)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        try { FallOut(solution, initialKickback); } catch { }
    }

    public void BeginSpin(List<SymbolData> solution = null, float startDelay = 0f)
    {
        // Ensure per-reel pool is large enough before spin starts to prevent transient gaps
        int est = EstimateNeededPooledCapacity();
        EnsurePooledSymbolCapacity(est);
        reelStrip.ResetSpinCounts(); // start fresh spin counts
        completeOnNextSpin = false;

        // Cancel any existing begin coroutine for this reel and start a lightweight coroutine to schedule the delayed start
        if (beginSpinCoroutine != null) StopCoroutine(beginSpinCoroutine);
        beginSpinCoroutine = StartCoroutine(DelayedBeginSpin(startDelay, solution));
    }

    private IEnumerator DelayedBeginSpin(float delay, List<SymbolData> solution)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        // Use the immediate entrypoint to avoid duplicating logic
        BeginSpinImmediate(solution);
        beginSpinCoroutine = null;
    }

    public void StopReel(float delay = 0f)
    {
        // If this reel is not effectively active (page hidden), perform a very lightweight stop
        // so the engine can remain in-sync without doing heavy visual work. Otherwise fall back
        // to the existing delayed stop behavior which accelerates visual motion.
        if (!IsEffectivelyPageActive())
        {
            // Cancel any existing stop coroutine
            if (stopReelCoroutine != null) { StopCoroutine(stopReelCoroutine); stopReelCoroutine = null; }
            if (delay > 0f)
            {
                stopReelCoroutine = StartCoroutine(DelayedLightStop(delay));
            }
            else
            {
                LightStopImmediate();
            }
            return;
        }

        EnsureUnpausedForStop();
        // Cancel any existing stop coroutine and schedule a lightweight delayed stop to avoid DOTween.Sequence allocations
        if (stopReelCoroutine != null) StopCoroutine(stopReelCoroutine);
        stopReelCoroutine = StartCoroutine(DelayedStopReel(delay));
    }

    private IEnumerator DelayedStopReel(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        // mark requested stop (fallout will complete the reel)
        completeOnNextSpin = true;
        // accelerate coroutine-based spin movement
        spinSpeedMultiplier = 4f;
        stopReelCoroutine = null;
    }

    private IEnumerator DelayedLightStop(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        LightStopImmediate();
        stopReelCoroutine = null;
    }

    // Lightweight immediate stop used for reels that are off-page. Avoids heavy tweens/coroutines;
    // broadcasts landed/completed so engine state remains consistent.
    private void LightStopImmediate()
    {
        // Cancel spin/landing coroutines
        try { if (beginSpinCoroutine != null) { StopCoroutine(beginSpinCoroutine); beginSpinCoroutine = null; } } catch { beginSpinCoroutine = null; }
        try { if (stopReelCoroutine != null) { StopCoroutine(stopReelCoroutine); stopReelCoroutine = null; } } catch { stopReelCoroutine = null; }
        try { if (activeSpinCoroutines[0] != null) { StopCoroutine(activeSpinCoroutines[0]); activeSpinCoroutines[0] = null; } } catch { activeSpinCoroutines[0] = null; }
        try { if (activeSpinCoroutines[1] != null) { StopCoroutine(activeSpinCoroutines[1]); activeSpinCoroutines[1] = null; } } catch { activeSpinCoroutines[1] = null; }

        // Capture pending landing solution and clear transient state
        var sol = pendingLandingSolution;
        pendingLandingSolution = null;
        suspendedAwaitingResume = false;
        landingPendingCount = 0;

        // Mark reel as completing and stop further spin progression
        completeOnNextSpin = true;
        spinSpeedMultiplier = 4f;
        spinning = false;

        // Determine whether a buffered 'next' strip exists (was prepared by FallOut/SpawnNextReel).
        bool hasBuffer = (bufferSymbols != null && bufferSymbols.Count > 0) ||
                         (bufferTopDummySymbols != null && bufferTopDummySymbols.Count > 0) ||
                         (bufferBottomDummySymbols != null && bufferBottomDummySymbols.Count > 0) ||
                         (nextSymbolsRoot != null && nextSymbolsRoot.childCount > 0);

        // Ensure any DOTween tweens affecting the roots are killed so they can't later override our forced positions
        try { DG.Tweening.DOTween.Kill(symbolRoot, false); } catch { }
        try { DG.Tweening.DOTween.Kill(nextSymbolsRoot, false); } catch { }

        // If coroutines were paused while the page was inactive the roots may be at intermediate positions.
        // Force the roots to the final landed positions so visuals are correct when the page is later shown.
        try
        {
            if (symbolRoot != null && nextSymbolsRoot != null)
            {
                if (hasBuffer)
                {
                    // When a buffer exists the final targeted positions during fallout are:
                    // symbolRoot -> fallDistance (which equals -nextSymbolsRoot.localPosition.y)
                    // nextSymbolsRoot -> 0
                    float desiredSymbolY = -nextSymbolsRoot.localPosition.y;
                    var sLp = symbolRoot.localPosition; sLp.y = desiredSymbolY; symbolRoot.localPosition = sLp;
                    var nLp = nextSymbolsRoot.localPosition; nLp.y = 0f; nextSymbolsRoot.localPosition = nLp;
                }
                else
                {
                    // No buffer: ensure active root is reset to origin so visible symbols are aligned
                    var sLp = symbolRoot.localPosition; sLp.y = 0f; symbolRoot.localPosition = sLp;
                }
            }
        }
        catch { }

        if (hasBuffer)
        {
            // Safe to finalize using the full completion path which swaps roots and adopts the buffer.
            try
            {
                CompleteReelSpin(sol);
            }
            catch
            {
                // Fallback: broadcast minimal events so engine isn't left waiting
                if (eventManager != null)
                {
                    for (int i = 0; i < symbols.Count; i++) { try { eventManager.BroadcastEvent(SlotsEvent.SymbolLanded, symbols[i]); } catch { } }
                    try { eventManager.BroadcastEvent(SlotsEvent.ReelCompleted, ID); } catch { }
                }
            }
        }
        else
        {
            // No prepared buffer: avoid swapping roots. Simply mark visible symbols landed and complete.
            if (eventManager != null)
            {
                for (int i = 0; i < symbols.Count; i++)
                {
                    try { eventManager.BroadcastEvent(SlotsEvent.SymbolLanded, symbols[i]); } catch { }
                }
                try { eventManager.BroadcastEvent(SlotsEvent.ReelCompleted, ID); } catch { }
            }
        }
    }

    private void SpawnNextReel(List<SymbolData> solution = null)
    {
        // Release any existing symbols under the next root first
        ReleaseAllSymbolsInRoot(nextSymbolsRoot);

        // Prepare new symbols for the incoming reel
        tmpNextReelSymbols.Clear();
        float step = currentReelData.SymbolSpacing + currentReelData.SymbolSize;

        tmpCombinedExisting.Clear();
        if (symbols != null)
            for (int i = 0; i < symbols.Count; i++)
                if (symbols[i]?.CurrentSymbolData != null)
                    tmpCombinedExisting.Add(symbols[i].CurrentSymbolData);

        // Ensure we have pooled symbols for the visible buffer rows
        EnsurePooledSymbolCapacity(currentReelData != null ? currentReelData.SymbolCount : 0);

        for (int i = 0; i < currentReelData.SymbolCount; i++)
        {
            // Acquire a pooled symbol for the visible buffer symbols
            GameSymbol symbol = AcquireFreePooledSymbol();
            if (symbol == null)
            {
                EnsurePooledSymbolCapacity(currentReelData != null ? currentReelData.SymbolCount : 1);
                symbol = AcquireFreePooledSymbol();
                if (symbol == null)
                {
                    symbol = CreateSymbolInstance(nextSymbolsRoot);
                }
            }
            // Resolve provided solution candidate or pick from strip
            SymbolData candidate = (solution != null && solution.Count > i) ? solution[i] : null;
            SymbolData def = ResolveAndPersistSymbol(candidate, tmpCombinedExisting);
            if (symbol != null) symbol.ApplySymbol(def);
            // Explicitly ensure the incoming visible buffer symbols have correct color state
            try { var sImg = symbol?.CachedImage; if (sImg != null) sImg.color = spinning ? Color.white : new Color(0.5f, 0.5f, 0.5f); } catch { }
            if (symbol != null) symbol.SetSizeAndLocalY(currentReelData.SymbolSize, step * i);
            if (symbol != null) symbol.transform.SetParent(nextSymbolsRoot, false);
            tmpNextReelSymbols.Add(symbol);
            if (def != null) tmpCombinedExisting.Add(def);
        }

        bufferSymbols = tmpNextReelSymbols;

        // Compute dummy counts for the buffer (so we can position the next root correctly)
        ComputeDummyCounts(out int topCount, out int bottomCount);

        // Spawn buffer dummies under the next root using persistent dummy pool
        // Ensure there is capacity for both active and buffer dummies (use estimate to be safe)
        int estCap = EstimateNeededPooledCapacity();
        EnsurePooledSymbolCapacity(Math.Max(allPooledSymbols.Count, estCap));

        // Acquire buffer bottom dummies
        bufferBottomDummySymbols.Clear();
        tmpCombinedForBuffer.Clear();
        tmpCombinedForBuffer.AddRange(tmpCombinedExisting);
        for (int i = 0; i < bottomCount; i++)
        {
            var sym = AcquireFreePooledSymbol();
            if (sym == null) // defensive fallback
            {
                sym = cachedSymbolPool != null ? cachedSymbolPool.Get(dummyContainer) : GameSymbolPool.Instance?.Get(dummyContainer);
                try { sym?.SetOwnerReel(this); } catch { }
                SymbolData initDef = ResolveAndPersistSymbol(null, tmpCombinedForBuffer);
                sym.InitializeSymbol(initDef, eventManager);
                sym.gameObject.SetActive(false);
                allPooledSymbols.Add(sym); allPooledSet.Add(sym);
                allocatedPooledSet.Add(sym);
            }
            SymbolData def = ResolveAndPersistSymbol(null, tmpCombinedForBuffer);
            sym.ApplySymbol(def);
            sym.transform.SetParent(nextSymbolsRoot, false);
            float y = -step * (i + 1);
            sym.SetSizeAndLocalY(currentReelData.SymbolSize, y);
            // ensure dummy visuals are dimmed when not spinning to match presentation state
            var img = sym.CachedImage; if (img != null) { img.color = !spinning ? new Color(0.5f, 0.5f, 0.5f) : Color.white; }
            if (def != null) tmpCombinedForBuffer.Add(def);
            bufferBottomDummySymbols.Add(sym);
        }

        // Acquire buffer top dummies
        bufferTopDummySymbols.Clear();
        for (int i = 0; i < topCount; i++)
        {
            var sym = AcquireFreePooledSymbol();
            if (sym == null)
            {
                sym = cachedSymbolPool != null ? cachedSymbolPool.Get(dummyContainer) : GameSymbolPool.Instance?.Get(dummyContainer);
                try { sym?.SetOwnerReel(this); } catch { }
                SymbolData initDef = ResolveAndPersistSymbol(null, tmpCombinedForBuffer);
                sym.InitializeSymbol(initDef, eventManager);
                sym.gameObject.SetActive(false);
                allPooledSymbols.Add(sym); allPooledSet.Add(sym);
                allocatedPooledSet.Add(sym);
            }
            SymbolData def = ResolveAndPersistSymbol(null, tmpCombinedForBuffer);
            sym.ApplySymbol(def);
            sym.transform.SetParent(nextSymbolsRoot, false);
            float y = step * (currentReelData.SymbolCount + i);
            sym.SetSizeAndLocalY(currentReelData.SymbolSize, y);
            // ensure dummy visuals are dimmed when not spinning to match presentation state
            var img = sym.CachedImage; if (img != null) { img.color = !spinning ? new Color(0.5f, 0.5f, 0.5f) : Color.white; }
            if (def != null) tmpCombinedForBuffer.Add(def);
            bufferTopDummySymbols.Add(sym);
        }

        // Position using rect extents so the vertical gap between strips equals the symbol spacing
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
                if (c.TryGetComponent<GameSymbol>(out var gs2) && gs2 != null) rt = gs2.CachedRect;
                if (rt == null) rt = c as RectTransform ?? c.GetComponent<RectTransform>();
                if (rt == null) continue;
                float bottom = c.localPosition.y - (rt.rect.height * rt.pivot.y);
                if (bottom < lowestIncomingBottom) lowestIncomingBottom = bottom;
            }
        }
        if (lowestIncomingBottom == float.MaxValue) lowestIncomingBottom = 0f;

        // Compute required next root local Y so that the gap between groups equals 'separation' (symbolSpacing)
        float separation = currentReelData != null ? currentReelData.SymbolSpacing : 0f;
        float symbolRootY = symbolRoot != null ? symbolRoot.localPosition.y : 0f;
        float requiredNextLocalY = symbolRootY + highestExistingTop - lowestIncomingBottom + separation;

        nextSymbolsRoot.localPosition = new Vector3(0f, requiredNextLocalY, 0f);
    }

    private List<GameSymbol> SpawnDummySymbols(Transform root, bool bottom, int count, bool dim, List<SymbolData> existingSelections, bool consume)
    {
        List<GameSymbol> dummies = new List<GameSymbol>(); if (count <= 0) return dummies; float step = currentReelData.SymbolSpacing + currentReelData.SymbolSize; int startIndex = bottom ? 1 : currentReelData.SymbolCount; int flip = bottom ? -1 : 1; Color dimColor = new Color(0.5f, 0.5f, 0.5f);

        for (int i = 0; i < count; i++)
        {
            var sym = AcquireFreePooledSymbol();
            if (sym == null)
            {
                sym = cachedSymbolPool != null ? cachedSymbolPool.Get(root) : GameSymbolPool.Instance?.Get(root);
                try { sym?.SetOwnerReel(this); } catch { }
                SymbolData initDef = reelStrip.GetWeightedSymbol(existingSelections, consume);
                sym.InitializeSymbol(initDef, eventManager);
                sym.gameObject.SetActive(false);
                allPooledSymbols.Add(sym); allPooledSet.Add(sym);
                allocatedPooledSet.Add(sym);
            }
            SymbolData def = reelStrip.GetWeightedSymbol(existingSelections, consume);
            sym.ApplySymbol(def);
            float y = (step * (i + startIndex)) * flip;
            sym.SetSizeAndLocalY(currentReelData.SymbolSize, y);
            sym.transform.SetParent(root, false);
            var img = sym.CachedImage; if (img != null) { img.color = dim ? dimColor : Color.white; }
            if (def != null) existingSelections?.Add(def);
            dummies.Add(sym);
        }
        return dummies;
    }

    public void FallOut(List<SymbolData> solution = null, bool kickback = false)
    {
        // Removed duplicated calls & duplicate coroutine starts to reduce CPU & GC overhead
        ResetDimmedSymbols();

        // baseline offset used to preserve original perceived speed regardless of how the next strip
        // was positioned (we may compute precise rect extents when positioning nextSymbolsRoot).
        float baselineOffset = ComputeFallOffsetY();

        SpawnNextReel(solution);
        float fallDistance = -nextSymbolsRoot.transform.localPosition.y;

        // If the actual fall distance differs from the baseline compute a scaled duration so
        // the visual speed (units/sec) remains consistent with the configured ReelSpinDuration.
        float duration = currentReelData.ReelSpinDuration;
        if (baselineOffset > 0f)
        {
            // scale duration in proportion to distance so smaller/larger gaps still move at
            // the same units/sec defined by ReelSpinDuration over the baseline offset.
            duration = duration * (Mathf.Abs(fallDistance) / baselineOffset);
            // protect against very small/large values
            duration = Mathf.Max(0.01f, duration);
        }

        pendingLandingSolution = solution;
        landingPendingCount = 2;

        if (activeSpinCoroutines[0] != null) { StopCoroutine(activeSpinCoroutines[0]); activeSpinCoroutines[0] = null; }
        if (activeSpinCoroutines[1] != null) { StopCoroutine(activeSpinCoroutines[1]); activeSpinCoroutines[1] = null; }
        spinSpeedMultiplier = 1f;

        if (symbolRoot != null)
            activeSpinCoroutines[0] = StartCoroutine(MoveLocalY(symbolRoot, fallDistance, duration, 0));
        if (nextSymbolsRoot != null)
            activeSpinCoroutines[1] = StartCoroutine(MoveLocalY(nextSymbolsRoot, 0f, duration, 1));
    }

    private IEnumerator MoveLocalY(Transform t, float targetY, float duration, int index)
    {
        if (t == null)
        {
            LandingPartCompleted();
            yield break;
        }

        float startY = t.localPosition.y;
        float elapsed = 0f;
        if (duration <= 0f)
        {
            var lpImmediate = t.localPosition; lpImmediate.y = targetY; t.localPosition = lpImmediate;
        }
        else
        {
            while (elapsed < duration)
            {
                // NEW: pause progress while page inactive (coroutine keeps yielding but elapsed doesn't advance)
                if (!IsEffectivelyPageActive())
                {
                    yield return null;
                    continue;
                }
                float dt = Time.deltaTime * spinSpeedMultiplier;
                elapsed += dt;
                float p = Mathf.Clamp01(duration > 0f ? (elapsed / duration) : 1f);
                float newY = Mathf.Lerp(startY, targetY, p);
                var lp = t.localPosition; lp.y = newY; t.localPosition = lp;
                yield return null;
            }
        }

        // Ensure final position
        var finalLp = t.localPosition; finalLp.y = targetY; t.localPosition = finalLp;

        // clear coroutine handle
        if (index >= 0 && index < activeSpinCoroutines.Length) activeSpinCoroutines[index] = null;

        LandingPartCompleted();
    }

    // --- New: page visibility suspension support ---
    private bool pageActive = true; // new: whether this reel's page is currently visible
    // Consider engine-level page active as well to avoid race conditions where reel-local flag
    // differs from the containing engine's known visibility. A reel is effectively active only
    // when both its local flag and the owner's IsPageActive are true.
    private bool IsEffectivelyPageActive() => pageActive && (ownerEngine == null || ownerEngine.IsPageActive);
    private bool suspendedAwaitingResume = false; // new: indicates post-landing or pre-first-fallout suspension between spins
    private Coroutine waitForPageResumeCoroutine; // new: coroutine waiting for page to become active
    // Tracks whether a Stop was already requested before the kickup animation began.
    private bool stopRequestedBeforeKickup = false;

    public void SetPageActive(bool active)
    {
        pageActive = active;
        if (active)
        {
            if (suspendedAwaitingResume && spinning && !completeOnNextSpin)
            {
                // resume next fallout cycle now (covers both post-landing and initial suspension cases)
                suspendedAwaitingResume = false;
                if (waitForPageResumeCoroutine != null)
                {
                    StopCoroutine(waitForPageResumeCoroutine);
                    waitForPageResumeCoroutine = null;
                }
                var sol = pendingLandingSolution; pendingLandingSolution = null;
                float delay = ID * ResumeStaggerStep;
                StartCoroutine(ResumeFallOutAfterDelay(sol, delay, false));
            }
        }
        else
        {
            // If we turn page inactive mid-spin we simply let current coroutines freeze (MoveLocalY checks pageActive)
            // landing will still occur but subsequent continuous spin cycles will be suspended.
        }
    }

    /// <summary>
    /// Returns true when the reel has no outstanding motion or pending resume work and can be
    /// considered fully settled for spin-completion purposes.
    /// </summary>
    public bool IsMotionComplete()
    {
        // not spinning, not suspended waiting for page resume, and no active spin coroutines
        bool coroutinesIdle = (activeSpinCoroutines[0] == null) && (activeSpinCoroutines[1] == null);
        return !spinning && !suspendedAwaitingResume && coroutinesIdle;
    }

    // Called when Stop is requested; ensure we unpause if hidden so stop can complete promptly
    private void EnsureUnpausedForStop()
    {
        if (!pageActive)
        {
            SetPageActive(true);
        }
    }

    private void LandingTweenCompleted()
    {
        // Capture current pending solution and clear it to avoid unintended reuse
        var sol = pendingLandingSolution;
        pendingLandingSolution = null;

        if (completeOnNextSpin)
        {
            if (IsEffectivelyPageActive())
            {
                // Play landing bounce when visible
                BounceReel(Vector3.down, peak: 0.25f, duration: KickupDuration, onComplete: () => CompleteReelSpin(sol));
            }
            else
            {
                // When not visible, skip any bounce/delay and complete immediately to avoid deferred state
                // transitions that would otherwise leave the engine waiting for non-existent visuals.
                CompleteReelSpin(sol);
            }
        }
        else
        {
            if (!IsEffectivelyPageActive())
            {
                suspendedAwaitingResume = true;
                if (waitForPageResumeCoroutine != null) StopCoroutine(waitForPageResumeCoroutine);
                waitForPageResumeCoroutine = StartCoroutine(WaitForPageResume(sol));
            }
            else
            {
                CompleteReelSpin(sol);
            }
        }
    }

    private IEnumerator SkipLandingBounceDelay(List<SymbolData> solution)
    {
        yield return new WaitForSeconds(KickupDuration);
        CompleteReelSpin(solution);
    }

    private void CompleteReelSpin(List<SymbolData> solution)
    {
        ReleaseAllSymbolsInRoot(symbolRoot); var old = symbolRoot; symbolRoot = nextSymbolsRoot; nextSymbolsRoot = old; float offsetY = ComputeFallOffsetY(); nextSymbolsRoot.localPosition = new Vector3(0, offsetY, 0);
        if (bufferSymbols.Count > 0)
        {
            // Copy buffer contents into the active list to avoid aliasing the same list instance
            symbols.Clear();
            symbols.AddRange(bufferSymbols);
            bufferSymbols = new List<GameSymbol>();
        }
        if (bufferTopDummySymbols.Count > 0)
        {
            topDummySymbols.Clear();
            topDummySymbols.AddRange(bufferTopDummySymbols);
            bufferTopDummySymbols = new List<GameSymbol>();
            lastTopDummyCount = topDummySymbols.Count;
        }
        if (bufferBottomDummySymbols.Count > 0)
        {
            bottomDummySymbols.Clear();
            bottomDummySymbols.AddRange(bufferBottomDummySymbols);
            bufferBottomDummySymbols = new List<GameSymbol>();
            lastBottomDummyCount = bottomDummySymbols.Count;
        }
        if (!completeOnNextSpin) { FallOut(solution); }
        else { spinning = false; if ((Application.isEditor || Debug.isDebugBuild) && WinEvaluator.Instance != null && WinEvaluator.Instance.LoggingEnabled) { var names = symbols.Select(s => s?.CurrentSymbolData != null ? s.CurrentSymbolData.Name : "(null)").ToArray(); Debug.Log($"Reel {ID} landed symbols (bottom->top): [{string.Join(",", names)}]"); } for (int i = 0; i < symbols.Count; i++) eventManager.BroadcastEvent(SlotsEvent.SymbolLanded, symbols[i]); eventManager.BroadcastEvent(SlotsEvent.ReelCompleted, ID); }

#if UNITY_EDITOR
        // Run pool integrity diagnostics in editor/dev to help catch reuse or corruption issues
        if ((Application.isEditor || Debug.isDebugBuild))
        {
            ValidatePoolIntegrity("CompleteReelSpin_end");
        }
#endif
    }

    public void DimDummySymbols() { Color dim = new Color(0.5f, 0.5f, 0.5f); foreach (GameSymbol g in topDummySymbols) { var img = g.CachedImage; if (img != null) img.color = dim; } foreach (GameSymbol g in bottomDummySymbols) { var img = g.CachedImage; if (img != null) img.color = dim; } }
    public void ResetDimmedSymbols() { foreach (GameSymbol g in topDummySymbols) { var image = g.CachedImage; if (image == null) continue; image.color = Color.white; } foreach (GameSymbol g in bottomDummySymbols) { var image = g.CachedImage; if (image == null) continue; image.color = Color.white; } }

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

    // Public API: update visual sizes/positions without regenerenerating or reassigning symbols/dummies
    public void ResizeVisuals(float newSymbolSize, float newSpacing)
    {
        if (currentReelData == null) return;

        // update stored sizes so other logic remains consistent
        currentReelData.SetSymbolSize(newSymbolSize, newSpacing);

        // recompute positions for active and dummy lists
        RecomputeAllPositions();

        // reposition nextSymbolsRoot to preserve correct fall offset
        float offsetY = ComputeFallOffsetY();
        if (nextSymbolsRoot != null) nextSymbolsRoot.localPosition = new Vector3(0, offsetY, 0);

        // if not spinning, ensure dummies are reset to normal visual state
        // After a visuals-only resize we should keep dummies dimmed when not spinning
        if (!spinning) DimDummySymbols();
    }

    public void RegenerateDummies()
    {
        if (symbolRoot == null) return;

        foreach (var d in topDummySymbols)
            if (d != null) ReleasePooledSymbol(d);
        topDummySymbols.Clear();

        foreach (var d in bottomDummySymbols)
            if (d != null) ReleasePooledSymbol(d);
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

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            if (child == null) continue;
            if (child.TryGetComponent<GameSymbol>(out var symbol))
            {
                // remove any stale references to this symbol from internal lists before releasing
                RemoveFromAllLists(symbol);

                // If this symbol is part of the persistent per-reel dummy pool, return it to the dummy container
                if (allPooledSymbols != null && allPooledSet.Contains(symbol))
                {
                    // mark it free and reparent to dummy container rather than releasing to global pool
                    ReleasePooledSymbol(symbol);
                }
                else
                {
                    // use cached reference when available
                    if (cachedSymbolPool != null) cachedSymbolPool.Release(symbol); else GameSymbolPool.Instance?.Release(symbol);
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

        // Manual removal to avoid predicate allocations from List.RemoveAll
        for (int i = symbols.Count - 1; i >= 0; i--) if (symbols[i] == symbol) symbols.RemoveAt(i);
        for (int i = topDummySymbols.Count - 1; i >= 0; i--) if (topDummySymbols[i] == symbol) topDummySymbols.RemoveAt(i);
        for (int i = bottomDummySymbols.Count - 1; i >= 0; i--) if (bottomDummySymbols[i] == symbol) bottomDummySymbols.RemoveAt(i);
        for (int i = bufferSymbols.Count - 1; i >= 0; i--) if (bufferSymbols[i] == symbol) bufferSymbols.RemoveAt(i);
        for (int i = bufferTopDummySymbols.Count - 1; i >= 0; i--) if (bufferTopDummySymbols[i] == symbol) bufferTopDummySymbols.RemoveAt(i);
        for (int i = bufferBottomDummySymbols.Count - 1; i >= 0; i--) if (bufferBottomDummySymbols[i] == symbol) bufferBottomDummySymbols.RemoveAt(i);

        if (allocatedPooledSet.Contains(symbol)) allocatedPooledSet.Remove(symbol);
    }

    public void SetSymbolCount(int newCount, bool incremental = true)
    {
        if (newCount < 1) newCount = 1;
        if (spinning) { return; }
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
        if (newCount > oldCount)
        {
            for (int i = oldCount; i < newCount; i++)
            {
                GameSymbol sym = cachedSymbolPool != null ? cachedSymbolPool.Get(symbolRoot) : GameSymbolPool.Instance?.Get(symbolRoot);
                SymbolData candidate = (currentReelData.CurrentSymbolData.Count > i) ? currentReelData.CurrentSymbolData[i] : null;
                SymbolData def = ResolveAndPersistSymbol(candidate, currentReelData.CurrentSymbolData);

                sym.InitializeSymbol(def, eventManager);
                symbols.Add(sym);
            }
        }
        else { for (int i = oldCount - 1; i >= newCount; i--) { if (i < 0 || i >= symbols.Count) continue; var sym = symbols[i]; if (sym != null) { if (cachedSymbolPool != null) cachedSymbolPool.Release(sym); else GameSymbolPool.Instance?.Release(sym); } symbols.RemoveAt(i); } }
        RecomputeAllPositions();

        if (affectsOtherReels)
        {
            // First regenerate dummies for all reels (since max height changed)
            if (ownerEngine != null) ownerEngine.RegenerateAllReelDummies();
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
        if (thisReelWasTallest && isGettingShorter(oldHeight, newHeight)) return true;

        // If this reel is becoming taller than the current max, other reels need more dummies
        if (newHeight > currentMaxHeight) return true;

        return false;
    }

    private bool isGettingShorter(float oldHeight, float newHeight)
    {
        return newHeight < oldHeight;
    }

    // --- Per-reel pooled symbol helpers (covers visible symbols, buffer symbols and dummies) ---
    private void EnsurePooledSymbolCapacity(int totalNeeded)
    {
        if (totalNeeded <= 0) return;
        // if already sufficient, nothing to do
        if (allPooledSymbols.Count >= totalNeeded) return;
        EnsureRootsCreated();
        int toCreate = totalNeeded - allPooledSymbols.Count;
        for (int i = 0; i < toCreate; i++)
        {
            var sym = CreateSymbolInstance(dummyContainer);
            // initialize with a harmless non-consuming pick so the symbol has a sprite and registers events
            SymbolData def = reelStrip != null ? reelStrip.GetWeightedSymbol(s_emptySelection, false) : null;
            if (sym != null)
            {
                if (def != null) sym.InitializeSymbol(def, eventManager); else sym.InitializeSymbol(reelStrip?.GetWeightedSymbol(null), eventManager);
                sym.SetSizeAndLocalY(currentReelData != null ? currentReelData.SymbolSize : 100, 0);
                // keep pool symbols inactive until allocated
                if (sym.gameObject.activeSelf) sym.gameObject.SetActive(false);
                allPooledSymbols.Add(sym); allPooledSet.Add(sym);
                // newly created symbol is free until allocated
                if (allocatedPooledSet.Contains(sym)) allocatedPooledSet.Remove(sym);
                freePooledStack.Push(sym);
            }
         }
     }

    // Estimate conservative capacity needed for pooling to avoid gaps: visible + top/bottom dummies + buffer
    private int EstimateNeededPooledCapacity()
    {
        if (currentReelData == null) return 0;
        ComputeDummyCounts(out int topCount, out int bottomCount);
        int visible = currentReelData.SymbolCount;
        int buffer = 2 * (currentReelData.SymbolCount); // safe buffer for incoming/outgoing
        int estimate = visible + topCount + bottomCount + buffer;
        // clamp to a reasonable max
        int maxReasonable = currentReelData.SymbolCount * 6 + 24;
        return Mathf.Min(estimate, maxReasonable);
    }

    private GameSymbol AcquireFreePooledSymbol()
    {
        // Fast path: use stack to pop free symbols
        while (freePooledStack.Count > 0)
        {
            var s = freePooledStack.Pop();
            if (s == null) continue;
            if (allocatedPooledSet.Contains(s)) continue; // defensive
            allocatedPooledSet.Add(s);
            if (!s.gameObject.activeSelf) s.gameObject.SetActive(true);

            // Ensure reused symbol has clean visual and tween state so it doesn't carry over a dim/rotated state
            try { s.StopAndClearTweens(); } catch { }
            try { var img = s.CachedImage; if (img != null) img.color = Color.white; } catch { }

            // NEW: cache owner reel reference to avoid GetComponentInParent in GameSymbol
            try { s.SetOwnerReel(this); } catch { }

            return s;
        }

        // No free symbol available: create one and return it
        EnsureRootsCreated();
        var newSym = CreateSymbolInstance(dummyContainer);
        if (newSym != null)
        {
            // NEW: assign owner before initialization so symbol can reference it immediately
            try { newSym.SetOwnerReel(this); } catch { }

            // initialize with non-consuming pick to get a sprite
            SymbolData def = reelStrip != null ? reelStrip.GetWeightedSymbol(s_emptySelection, false) : null;
            if (def != null) newSym.InitializeSymbol(def, eventManager);
            if (!newSym.gameObject.activeSelf) newSym.gameObject.SetActive(true);
            allPooledSymbols.Add(newSym); allPooledSet.Add(newSym);
            allocatedPooledSet.Add(newSym);

            // newly created symbol should start clean
            try { newSym.StopAndClearTweens(); } catch { }
            try { var img2 = newSym.CachedImage; if (img2 != null) img2.color = Color.white; } catch { }
            return newSym;
        }
        return null;
    }

    // Robust symbol creation: prefer GameSymbolPool, fallback to manual GameObject if needed
    private GameSymbol CreateSymbolInstance(Transform parent)
    {
        try
        {
            if (cachedSymbolPool != null)
            {
                var s = cachedSymbolPool.Get(parent);
                if (s != null)
                {
                    // assign owner when pulled from pool
                    try { s.SetOwnerReel(this); } catch { }
                    return s;
                }
            }
        }
        catch (Exception)
        {
            // ignore and fallback to manual creation
        }

        // Fallback: create GameObject and add GameSymbol
        var go = new GameObject("PooledGameSymbol");
        if (parent != null) go.transform.SetParent(parent, false);
        var gs = go.AddComponent<GameSymbol>();
        // ensure it has an Image & RectTransform for layout - add minimal components if missing
        if (go.GetComponent<UnityEngine.UI.Image>() == null) go.AddComponent<UnityEngine.UI.Image>();
        if (go.GetComponent<RectTransform>() == null) go.AddComponent<RectTransform>();

        // assign owner for newly created instance
        try { gs.SetOwnerReel(this); } catch { }

        return gs;
    }

    private void ReleasePooledSymbol(GameSymbol s)
    {
        if (s == null) return;

        // clear tweens and visual state before returning to pool
        try { s.StopAndClearTweens(); } catch { }
        try { var img = s.CachedImage; if (img != null) img.color = Color.white; } catch { }

        allocatedPooledSet.Remove(s);
        // hide the pooled symbol to avoid visual orphaning
        if (s.gameObject.activeSelf) s.gameObject.SetActive(false);
        // return to dummy container so it's out of the way until reused
        if (dummyContainer != null) s.transform.SetParent(dummyContainer, true);
        // push back to free stack for fast future reuse
        freePooledStack.Push(s);
    }

    // Public API: prewarm per-reel pooled symbols (used by SlotsEngine)
    public void PrewarmPooledSymbols()
    {
        EnsureRootsCreated();
        int est = EstimateNeededPooledCapacity();
        if (est > 0) EnsurePooledSymbolCapacity(est);
    }
    private void LandingPartCompleted()
    {
        landingPendingCount--;
        if (landingPendingCount <= 0)
        {
            landingPendingCount = 0;
            LandingTweenCompleted();
        }
    }

    private void BounceReel(Vector3 direction, float strength = 100f, float duration = 0.5f, float sharpness = 0f, float peak = 0.4f, Action onComplete = null)
    {
        try
        {
            if (nextSymbolsRoot != null)
            {
                try { nextSymbolsRoot.DOPulseUp(direction, strength, duration, sharpness, peak).SetEase(Ease.Linear); } catch { }
            }
            if (symbolRoot != null)
            {
                try { symbolRoot.DOPulseUp(direction, strength, duration, sharpness, peak).SetEase(Ease.Linear).OnComplete(() => { onComplete?.Invoke(); }); }
                catch { onComplete?.Invoke(); }
            }
            else
            {
                onComplete?.Invoke();
            }
        }
        catch
        {
            try { onComplete?.Invoke(); } catch { }
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void ValidatePoolIntegrity(string context)
    {
        try
        {
            var subsets = new Dictionary<string, IEnumerable<GameSymbol>>()
            {
                { "symbols", symbols },
                { "topDummySymbols", topDummySymbols },
                { "bottomDummySymbols", bottomDummySymbols },
                { "bufferSymbols", bufferSymbols },
                { "bufferTopDummySymbols", bufferTopDummySymbols },
                { "bufferBottomDummySymbols", bufferBottomDummySymbols }
            };
            foreach (var kv in subsets)
            {
                var dup = kv.Value.GroupBy(x => x).Where(g => g.Key != null && g.Count() > 1).ToList();
                foreach (var g in dup)
                    Debug.LogWarning($"[GameReel {ID}] PoolIntegrity duplicate in {kv.Key} count={g.Count()} name={g.Key?.name} ctx={context}");
            }
            var subsetNames = subsets.Keys.ToList();
            for (int a = 0; a < subsetNames.Count; a++)
            {
                for (int b = a + 1; b < subsetNames.Count; b++)
                {
                    var setA = new HashSet<GameSymbol>(subsets[subsetNames[a]].Where(s => s != null));
                    var setB = new HashSet<GameSymbol>(subsets[subsetNames[b]].Where(s => s != null));
                    setA.IntersectWith(setB);
                    foreach (var s in setA)
                        Debug.LogWarning($"[GameReel {ID}] PoolIntegrity symbol in multiple lists ({subsetNames[a]};{subsetNames[b]}) name={s?.name} ctx={context}");
                }
            }
            var pooledDups = allPooledSymbols.GroupBy(x => x).Where(g => g.Key != null && g.Count() > 1).ToList();
            foreach (var g in pooledDups)
                Debug.LogWarning($"[GameReel {ID}] PoolIntegrity duplicate pooled symbol count={g.Count()} name={g.Key?.name} ctx={context}");
            foreach (var aSym in allocatedPooledSet)
                if (!allPooledSet.Contains(aSym))
                    Debug.LogWarning($"[GameReel {ID}] PoolIntegrity allocated symbol not in allPooledSet name={aSym?.name} ctx={context}");
            foreach (var s in freePooledStack.ToArray())
            {
                if (!allPooledSet.Contains(s)) Debug.LogWarning($"[GameReel {ID}] PoolIntegrity free symbol not in allPooledSet name={s?.name} ctx={context}");
                if (allocatedPooledSet.Contains(s)) Debug.LogWarning($"[GameReel {ID}] PoolIntegrity free symbol marked allocated name={s?.name} ctx={context}");
            }
            if (allocatedPooledSet.Count > allPooledSymbols.Count)
                Debug.LogWarning($"[GameReel {ID}] PoolIntegrity allocCount > total ctx={context}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameReel {ID}] ValidatePoolIntegrity failed: {ex.Message}");
        }
    }
#endif

    // Ensure WaitForPageResume is declared before usage in LandingTweenCompleted
    private IEnumerator WaitForPageResume(List<SymbolData> solution)
    {
        while (!IsEffectivelyPageActive() && !completeOnNextSpin) yield return null;
        suspendedAwaitingResume = false; waitForPageResumeCoroutine = null;
        if (completeOnNextSpin)
        {
            spinning = false;
            for (int i = 0; i < symbols.Count; i++) eventManager.BroadcastEvent(SlotsEvent.SymbolLanded, symbols[i]);
            eventManager.BroadcastEvent(SlotsEvent.ReelCompleted, ID);
        }
        else
        {
            float delay = ID * ResumeStaggerStep;
            StartCoroutine(ResumeFallOutAfterDelay(solution, delay, false));
        }
    }
}