using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Composite SlotEvent that executes a sequence of child events in order.
/// Useful for building complex multi-stage event behaviors from simpler building blocks.
/// Example: "Award instant win, then apply 2x multiplier to next spin, then award free spin"
/// </summary>
[CreateAssetMenu(menuName = "Slots/Events/Composite Event", fileName = "CompositeEvent")]
public class CompositeSlotEvent : SlotEvent
{
    [Tooltip("Ordered list of child events to execute sequentially.")]
    [SerializeField]
    private List<SlotEvent> childEvents = new List<SlotEvent>();

    [Tooltip("If true, continue executing remaining events even if one fails. If false, stop on first failure.")]
    [SerializeField]
    private bool continueOnFailure = true;

    public List<SlotEvent> ChildEvents => childEvents;

    public override void Execute(SlotEventContext context, Action onComplete)
    {
        if (childEvents == null || childEvents.Count == 0)
        {
            Debug.LogWarning($"CompositeSlotEvent '{eventName}' has no child events. Completing immediately.");
            SafeComplete(onComplete);
            return;
        }

        ExecuteChildEventsRecursive(context, 0, onComplete);
    }

    private void ExecuteChildEventsRecursive(SlotEventContext context, int index, Action onComplete)
    {
        if (index >= childEvents.Count)
        {
            SafeComplete(onComplete);
            return;
        }

        SlotEvent currentEvent = childEvents[index];
        if (currentEvent == null)
        {
            // Skip null entries
            ExecuteChildEventsRecursive(context, index + 1, onComplete);
            return;
        }

        try
        {
            currentEvent.Execute(context, () =>
            {
                // Event completed successfully, proceed to next
                ExecuteChildEventsRecursive(context, index + 1, onComplete);
            });
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);

            if (continueOnFailure)
            {
                // Continue to next event despite failure
                ExecuteChildEventsRecursive(context, index + 1, onComplete);
            }
            else
            {
                // Stop sequence on failure
                SafeComplete(onComplete);
            }
        }
    }
}
