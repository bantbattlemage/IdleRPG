using System;
using UnityEngine;

/// <summary>
/// Awards one or more free spins to the player. Free spins are tracked by the GamePlayer
/// and automatically consumed on the next spin(s) without charging credits.
/// Note: Full free spin functionality would require additional state management in GamePlayer.
/// This implementation provides the scaffolding; developers can extend as needed.
/// </summary>
[CreateAssetMenu(menuName = "Slots/Events/Free Spin Event", fileName = "FreeSpinEvent")]
public class FreeSpinEvent : SlotEvent
{
    [Tooltip("Number of free spins to award.")]
    [SerializeField]
    private int freeSpinCount = 1;

    [Tooltip("Optional message to display when free spins are awarded.")]
    [SerializeField]
    private string awardMessage = "Free Spins Awarded!";

    public int FreeSpinCount => freeSpinCount;

    public override void Execute(SlotEventContext context, Action onComplete)
    {
        try
        {
            // TODO: Implement free spin state tracking in GamePlayer/PlayerData
            // For now, log the award and display a message

            if (!string.IsNullOrEmpty(awardMessage) && SlotConsoleController.Instance != null && context?.SlotsEngine != null)
            {
                string message = $"{awardMessage} x{freeSpinCount}";
                SlotConsoleController.Instance.AppendPresentationMessage(context.SlotsEngine, message);
            }

            Debug.Log($"FreeSpinEvent '{eventName}' awarded {freeSpinCount} free spin(s). (Free spin state management not yet implemented)");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
        finally
        {
            SafeComplete(onComplete);
        }
    }
}
