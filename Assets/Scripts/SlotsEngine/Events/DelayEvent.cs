using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Simple delay event that waits for a specified duration before completing.
/// Useful for pacing event sequences or adding dramatic pauses in presentations.
/// </summary>
[CreateAssetMenu(menuName = "Slots/Events/Delay Event", fileName = "DelayEvent")]
public class DelayEvent : SlotEvent
{
    [Tooltip("Duration in seconds to wait before completing.")]
    [SerializeField]
    private float delaySeconds = 1f;

    [Tooltip("Optional message to display during the delay.")]
    [SerializeField]
    private string delayMessage = "";

    public float DelaySeconds => delaySeconds;

    public override void Execute(SlotEventContext context, Action onComplete)
    {
        try
        {
            if (!string.IsNullOrEmpty(delayMessage) && SlotConsoleController.Instance != null && context?.SlotsEngine != null)
            {
                SlotConsoleController.Instance.AppendPresentationMessage(context.SlotsEngine, delayMessage);
            }

            // Start a coroutine to handle the delay (requires a MonoBehaviour)
            if (SlotEventManager.Instance != null)
            {
                SlotEventManager.Instance.StartCoroutine(DelayCoroutine(delaySeconds, onComplete));
            }
            else
            {
                // Fallback: complete immediately if no manager available
                Debug.LogWarning($"DelayEvent '{eventName}' cannot delay without SlotEventManager. Completing immediately.");
                SafeComplete(onComplete);
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SafeComplete(onComplete);
        }
    }

    private IEnumerator DelayCoroutine(float duration, Action onComplete)
    {
        yield return new WaitForSeconds(duration);
        SafeComplete(onComplete);
    }
}
