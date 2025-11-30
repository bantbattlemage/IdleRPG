using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject assigned on a `SymbolDefinition` to run custom behavior
/// when that symbol is awarded. Presentation waits until `Execute` calls the
/// provided completion callback before proceeding.
/// 
/// Now supports delivering a configurable sequence of SlotEvent instances,
/// allowing composition of multiple event behaviors (e.g., instant win + free spins).
/// </summary>
[CreateAssetMenu(menuName = "Slots/Win Event Trigger", fileName = "WinEventTrigger")]
public class WinEventTrigger : ScriptableObject, IEventTriggerScript
{
    [Tooltip("When true the standard presentation/highlight animation is played on the triggering symbols. Set false to suppress the default highlight.")]
    [SerializeField]
    private bool playStandardPresentation = true;

    [Tooltip("Ordered list of SlotEvent instances to execute when this trigger fires. Events execute sequentially.")]
    [SerializeField]
    private List<SlotEvent> slotEvents = new List<SlotEvent>();

    public bool PlayStandardPresentation => playStandardPresentation;
    public List<SlotEvent> SlotEvents => slotEvents;

    // Backwards-compatible Execute without SlotsEngine parameter
    public virtual void Execute(WinData winData, Action onComplete)
    {
        Execute(winData, onComplete, null);
    }

    // New Execute overload that accepts SlotsEngine for proper context building
    public virtual void Execute(WinData winData, Action onComplete, SlotsEngine slotsEngine)
    {
        // If no events are configured, warn and complete immediately
        if (slotEvents == null || slotEvents.Count == 0)
        {
            Debug.LogWarning($"WinEventTrigger '{name}' has no SlotEvents configured. Completing immediately.");
            SafeComplete(onComplete);
            return;
        }

        // Build execution context with the provided SlotsEngine
        SlotEventContext context = BuildContext(winData, slotsEngine);

        // Execute events sequentially
        ExecuteEventsSequentially(context, 0, onComplete);
    }

    /// <summary>
    /// Recursively execute the configured SlotEvent list in order, invoking onComplete
    /// when all events have finished.
    /// </summary>
    private void ExecuteEventsSequentially(SlotEventContext context, int index, Action onComplete)
    {
        if (index >= slotEvents.Count)
        {
            // All events completed
            SafeComplete(onComplete);
            return;
        }

        SlotEvent currentEvent = slotEvents[index];
        if (currentEvent == null)
        {
            // Skip null entries
            ExecuteEventsSequentially(context, index + 1, onComplete);
            return;
        }

        try
        {
            currentEvent.Execute(context, () =>
            {
                // When this event completes, execute the next one
                ExecuteEventsSequentially(context, index + 1, onComplete);
            });
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            // Continue to next event even if this one threw
            ExecuteEventsSequentially(context, index + 1, onComplete);
        }
    }

    /// <summary>
    /// Build the execution context from the triggering WinData and SlotsEngine.
    /// Subclasses can override to provide additional context data.
    /// </summary>
    protected virtual SlotEventContext BuildContext(WinData winData, SlotsEngine slotsEngine)
    {
        SlotEventContext context = new SlotEventContext
        {
            TriggeringWin = winData,
            Player = GamePlayer.Instance,
            CurrentBet = GamePlayer.Instance?.CurrentBet,
            AccumulatedWinValue = winData?.WinValue ?? 0,
            SlotsEngine = slotsEngine
        };

        return context;
    }

    protected void SafeComplete(Action onComplete)
    {
        try { onComplete?.Invoke(); } catch { }
    }
}
