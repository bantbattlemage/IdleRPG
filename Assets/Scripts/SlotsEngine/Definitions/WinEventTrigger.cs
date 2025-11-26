using System;
using UnityEngine;

/// <summary>
/// ScriptableObject assigned on a `SymbolDefinition` to run custom behavior
/// when that symbol is awarded. Presentation waits until `Execute` calls the
/// provided completion callback before proceeding.
/// </summary>
[CreateAssetMenu(menuName = "Slots/Win Event Trigger", fileName = "WinEventTrigger")]
public class WinEventTrigger : ScriptableObject, IEventTriggerScript
{
    [Tooltip("When true the standard presentation/highlight animation is played on the triggering symbols. Set false to suppress the default highlight.")]
    [SerializeField]
    private bool playStandardPresentation = true;

    public bool PlayStandardPresentation => playStandardPresentation;

    // Developers can subclass this asset and override Execute to implement fully custom behavior.
    // The default implementation now warns and completes immediately so presenters are not blocked.
    public virtual void Execute(WinData winData, Action onComplete)
    {
		Debug.LogWarning($"WinEventTrigger '{name}' has no custom Execute implementation. Completing immediately.");
		SafeComplete(onComplete);
    }

    protected void SafeComplete(Action onComplete)
    {
        try { onComplete?.Invoke(); } catch { }
    }
}
