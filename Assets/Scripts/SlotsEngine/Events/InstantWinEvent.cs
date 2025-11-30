using System;
using UnityEngine;

/// <summary>
/// Slot event that instantly awards a fixed credit amount to the player.
/// Useful for bonus awards, jackpot triggers, or fixed-value scatter wins.
/// </summary>
[CreateAssetMenu(menuName = "Slots/Events/Instant Win Event", fileName = "InstantWinEvent")]
public class InstantWinEvent : SlotEvent
{
    [Tooltip("Fixed credit amount to award when this event executes.")]
    [SerializeField]
    private int creditAmount = 100;

    [Tooltip("Optional message to display when this instant win is awarded.")]
    [SerializeField]
    private string winMessage = "Instant Win!";

    public int CreditAmount => creditAmount;
    public string WinMessage => winMessage;

    public override void Execute(SlotEventContext context, Action onComplete)
    {
        try
        {
            if (context?.Player != null)
            {
                context.Player.AddCredits(creditAmount);
            }

            if (!string.IsNullOrEmpty(winMessage) && SlotConsoleController.Instance != null && context?.SlotsEngine != null)
            {
                string message = $"{winMessage} +{creditAmount}";
                SlotConsoleController.Instance.AppendPresentationMessage(context.SlotsEngine, message);
            }

            Debug.Log($"InstantWinEvent '{eventName}' awarded {creditAmount} credits.");
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
