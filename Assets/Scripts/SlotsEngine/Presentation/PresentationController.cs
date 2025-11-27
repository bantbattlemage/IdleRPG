using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PresentationController : Singleton<PresentationController>
{
    private List<SlotsPresentationData> slots = new List<SlotsPresentationData>();
    private bool presentationSessionActive = false;

    public void AddSlotsToPresentation(EventManager eventManager, SlotsEngine parentSlots)
    {
        var newData = new SlotsPresentationData() { slotsEventManager = eventManager, slotsEngine = parentSlots };
        slots.Add(newData);

        eventManager.RegisterEvent(SlotsEvent.BeginSlotPresentation, OnBeginSlotPresentation);
        // listen for per-engine presentation complete so we can track group completion
        eventManager.RegisterEvent(SlotsEvent.PresentationComplete, (obj) => OnSlotPresentationComplete(parentSlots));
    }

    private void OnBeginSlotPresentation(object obj)
    {
        SlotsEngine slotsToPresent = (SlotsEngine)obj;
        if (slotsToPresent == null) return;

        var sd = slots.FirstOrDefault(x => x.slotsEngine == slotsToPresent);
        if (sd == null) return;

        var reels = slotsToPresent.CurrentReels;
        if (reels == null || reels.Count == 0) return;

        List<GameSymbol[]> visualColumns = new List<GameSymbol[]>();
        for (int c = 0; c < reels.Count; c++)
        {
            var symbolsList = reels[c].Symbols;
            visualColumns.Add(symbolsList != null ? symbolsList.ToArray() : new GameSymbol[0]);
        }

        GameSymbol[] currentSymbolGrid = Helpers.CombineColumnsToGrid(visualColumns);

        List<WinData> winData = WinEvaluator.Instance != null && WinEvaluator.Instance.CurrentSpinWinData != null
            ? WinEvaluator.Instance.CurrentSpinWinData
            : new List<WinData>();

        // stash per-slot presentation data and mark ready
        sd.SetCurrentGrid(currentSymbolGrid);
        sd.SetCurrentWinData(winData);
        sd.SetReadyToPresent(true);
        sd.SetPresentationCompleted(false);
        sd.ResetPendingCallbacks();

        // Determine participants (those currently in Presentation state)
        var presentingEngines = slots.Where(s => s.slotsEngine != null && s.slotsEngine.CurrentState == State.Presentation).ToList();
        bool allReady = presentingEngines.Count > 0 && presentingEngines.All(s => s.readyToPresent);

        if (!presentationSessionActive)
        {
            if (allReady)
            {
                StartGroupPresentation(presentingEngines);
            }
            // otherwise wait for remaining engines to call BeginSlotPresentation
        }
        else
        {
            // if session already active, start this slot immediately as part of the session
            if (sd.currentWinData != null) PlayWinlines(sd, sd.slotsEngine, sd.currentGrid, sd.currentWinData);
            float d = (sd.currentWinData != null && sd.currentWinData.Count > 0) ? 1f : 0f;
            StartCoroutine(NotifyPresentationCompleteAfterDelay(sd, d));
        }
    }

    private void PlayWinlines(SlotsPresentationData sd, SlotsEngine slotsToPresent, GameSymbol[] grid, List<WinData> winData)
    {
        int gridLen = grid != null ? grid.Length : 0;
        foreach (WinData w in winData)
        {
            if (w.WinningSymbolIndexes != null)
            {
                // collect distinct triggers for this evaluated win so each is executed once
                var distinctTriggers = new List<IEventTriggerScript>();

                foreach (int index in w.WinningSymbolIndexes)
                {
                    if (index < 0 || index >= gridLen) continue; // skip invalid indexes
                    var gs = grid[index]; if (gs == null) continue; // skip null slots

                    try
                    {
                        bool doShake = true;
                        try { doShake = gs.OwnerReel != null ? gs.OwnerReel.OwnerEngine.IsPageActive : true; } catch { }
                        Color hi = Color.green;
                        if (gs.CurrentSymbolData != null)
                        {
                            switch (gs.CurrentSymbolData.WinMode)
                            {
                                case EvaluatorCore.SymbolWinMode.LineMatch: hi = Color.green; break;
                                case EvaluatorCore.SymbolWinMode.SingleOnReel: hi = Color.yellow; break;
                                case EvaluatorCore.SymbolWinMode.TotalCount: hi = Color.red; break;
                            }
                        }

                        // determine if this symbol should play the standard highlight
                        var runtimeTrigger = gs.CurrentSymbolData?.RuntimeEventTrigger;
                        bool playStandard = true;
                        if (runtimeTrigger is WinEventTrigger et)
                        {
                            playStandard = et.PlayStandardPresentation;
                        }

                        if (playStandard)
                        {
                            gs.HighlightForWin(hi, doShake);
                        }

                        // collect distinct triggers to execute once-per-win
                        if (runtimeTrigger != null && !distinctTriggers.Contains(runtimeTrigger))
                        {
                            distinctTriggers.Add(runtimeTrigger);
                        }
                    }
                    catch { }
                }

                // execute each distinct trigger once for this evaluated win
                foreach (var trigger in distinctTriggers)
                {
                    sd.IncrementPendingCallbacks();
                    try
                    {
                        trigger.Execute(w, () => { sd.DecrementPendingCallbacks(); });
                    }
                    catch
                    {
                        sd.DecrementPendingCallbacks();
                    }
                }
            }
            string individualMsg = w.LineIndex >= 0 ? $"Won {w.WinValue} on line {w.LineIndex}!" : (w.WinningSymbolIndexes?.Length <= 1 ? $"Won {w.WinValue}!" : $"Won {w.WinValue} ({w.WinningSymbolIndexes.Length} symbols)!");
            SlotConsoleController.Instance?.AppendPresentationMessage(slotsToPresent, individualMsg);
        }
        int totalWin = winData.Sum(x => x.WinValue);
        GamePlayer.Instance.AddCredits(totalWin);
        SlotConsoleController.Instance?.SetWinText(totalWin);
        string summary = (winData.Count == 1) ? $"Won {winData[0].WinValue} on line {winData[0].LineIndex}!" : $"Won {totalWin} on {winData.Count} line(s)!";
        SlotConsoleController.Instance?.AppendPresentationMessage(slotsToPresent, summary);
    }

    private void OnSlotPresentationComplete(SlotsEngine engine)
    {
        if (engine == null) return;
        var sd = slots.FirstOrDefault(x => x.slotsEngine == engine);
        if (sd == null) return;
        if (sd.presentationCompleted) return;
        sd.SetPresentationCompleted(true);
        sd.ClearCurrentWinData();
        CheckGroupCompletion();
    }

    private void CheckGroupCompletion()
    {
        if (!presentationSessionActive) return;

        var participants = slots.Where(s => s.readyToPresent).ToList();
        if (participants.Count == 0)
        {
            presentationSessionActive = false;
            SlotConsoleController.Instance?.EndPresentationSession();
            try { WinEvaluator.Instance?.ClearCurrentSpinWinData(); } catch { }
            return;
        }

        if (participants.All(p => p.presentationCompleted))
        {
            presentationSessionActive = false;
            SlotConsoleController.Instance?.EndPresentationSession();

            // Reset flags
            foreach (var p in participants)
            {
                p.SetPresentationCompleted(false);
                p.SetReadyToPresent(false);
                p.ClearCurrentGrid();
            }

            // Clear stored win data now that the presentation session has fully completed so it won't be reused later
            try { WinEvaluator.Instance?.ClearCurrentSpinWinData(); } catch { }
        }
    }

    private void StartGroupPresentation(List<SlotsPresentationData> participants)
    {
        if (participants == null || participants.Count == 0) return;
        presentationSessionActive = true;
        SlotConsoleController.Instance?.BeginPresentationSession();

        foreach (var p in participants)
        {
            if (p == null) continue;
            if (p.currentWinData != null && p.currentWinData.Count > 0)
            {
                PlayWinlines(p, p.slotsEngine, p.currentGrid, p.currentWinData);
                StartCoroutine(NotifyPresentationCompleteAfterDelay(p, 1f));
            }
            else
            {
                StartCoroutine(NotifyPresentationCompleteAfterDelay(p, 0f));
            }
        }
    }

    private System.Collections.IEnumerator NotifyPresentationCompleteAfterDelay(SlotsPresentationData sd, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        // wait for any event trigger executions to complete
        while (sd.PendingCallbacks > 0)
        {
            yield return null;
        }

        try { sd.slotsEngine.BroadcastSlotsEvent(SlotsEvent.PresentationComplete); } catch { }
        sd.SetPresentationCompleted(true);
        sd.ClearCurrentWinData();
        CheckGroupCompletion();
        yield break;
    }
}

class SlotsPresentationData
{
    public EventManager slotsEventManager;
    public SlotsEngine slotsEngine;
    public List<WinData> currentWinData;
    public GameSymbol[] currentGrid;
    public bool presentationCompleted;
    public bool readyToPresent;
    private int pendingCallbacks;

    public int PendingCallbacks { get { return pendingCallbacks; } }

    public void ResetPendingCallbacks() { pendingCallbacks = 0; }
    public void IncrementPendingCallbacks() { pendingCallbacks++; }
    public void DecrementPendingCallbacks() { pendingCallbacks = Mathf.Max(0, pendingCallbacks - 1); }

    public void SetCurrentWinData(List<WinData> newWinData) { currentWinData = newWinData; }
    public void ClearCurrentWinData() { currentWinData = null; }
    public void SetPresentationCompleted(bool complete) { presentationCompleted = complete; }

    public void SetCurrentGrid(GameSymbol[] grid) { currentGrid = grid; }
    public void ClearCurrentGrid() { currentGrid = null; }
    public void SetReadyToPresent(bool ready) { readyToPresent = ready; }
}