using System;
using UnityEngine;

/// <summary>
/// Base class for reusable, composable slot events that can be triggered during gameplay.
/// Events execute asynchronously and must invoke the completion callback when finished
/// so the slot presentation can proceed. Developers can create custom events by subclassing
/// this ScriptableObject and overriding Execute.
/// </summary>
public abstract class SlotEvent : ScriptableObject
{
    [Tooltip("Human-readable name for this event, used in UI and logs.")]
    [SerializeField]
    protected string eventName = "Slot Event";

    public string EventName => eventName;

    /// <summary>
    /// Execute the event behavior. Must invoke onComplete when finished to allow the slots
    /// engine/presentation to continue. Context provides access to the current game state
    /// (credits, bet, slot engine, win data, etc.).
    /// </summary>
    /// <param name="context">Execution context containing game state and win information</param>
    /// <param name="onComplete">Callback to invoke when this event finishes executing</param>
    public abstract void Execute(SlotEventContext context, Action onComplete);

    /// <summary>
    /// Safe wrapper to invoke completion callback with exception handling to avoid blocking presentation.
    /// </summary>
    protected void SafeComplete(Action onComplete)
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
