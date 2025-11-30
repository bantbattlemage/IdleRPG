using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralized manager for executing slot events. Provides a global access point
/// for triggering events that are not tied to specific win triggers (e.g., timed
/// bonuses, external triggers, or manual event invocation).
/// </summary>
public class SlotEventManager : Singleton<SlotEventManager>
{
    protected override void Awake()
    {
        base.Awake();
        // No additional initialization required currently
    }

    /// <summary>
    /// Execute a single SlotEvent with the provided context.
    /// </summary>
    public void ExecuteEvent(SlotEvent slotEvent, SlotEventContext context, Action onComplete)
    {
        if (slotEvent == null)
        {
            Debug.LogWarning("SlotEventManager.ExecuteEvent: slotEvent is null. Completing immediately.");
            SafeComplete(onComplete);
            return;
        }

        try
        {
            slotEvent.Execute(context, onComplete);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SafeComplete(onComplete);
        }
    }

    /// <summary>
    /// Execute a sequence of SlotEvents in order. Each event must complete before the next begins.
    /// </summary>
    public void ExecuteEventSequence(List<SlotEvent> events, SlotEventContext context, Action onComplete)
    {
        if (events == null || events.Count == 0)
        {
            SafeComplete(onComplete);
            return;
        }

        ExecuteEventsRecursive(events, context, 0, onComplete);
    }

    private void ExecuteEventsRecursive(List<SlotEvent> events, SlotEventContext context, int index, Action onComplete)
    {
        if (index >= events.Count)
        {
            SafeComplete(onComplete);
            return;
        }

        SlotEvent currentEvent = events[index];
        if (currentEvent == null)
        {
            // Skip null entries
            ExecuteEventsRecursive(events, context, index + 1, onComplete);
            return;
        }

        try
        {
            currentEvent.Execute(context, () =>
            {
                ExecuteEventsRecursive(events, context, index + 1, onComplete);
            });
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            ExecuteEventsRecursive(events, context, index + 1, onComplete);
        }
    }

    /// <summary>
    /// Build a default execution context for events not triggered by a specific win.
    /// </summary>
    public SlotEventContext BuildDefaultContext(SlotsEngine slotsEngine = null)
    {
        return new SlotEventContext
        {
            Player = GamePlayer.Instance,
            CurrentBet = GamePlayer.Instance?.CurrentBet,
            SlotsEngine = slotsEngine,
            AccumulatedWinValue = 0
        };
    }

    private void SafeComplete(Action onComplete)
    {
        try
        {
            onComplete?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }
}
