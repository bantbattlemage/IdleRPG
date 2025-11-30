using System;
using UnityEngine;

/// <summary>
/// Multiplies the accumulated win value (or triggering win value) by a configured factor.
/// Example use case: "wins of symbol X pay double this spin" or "triple payout" bonus.
/// </summary>
[CreateAssetMenu(menuName = "Slots/Events/Win Multiplier Event", fileName = "WinMultiplierEvent")]
public class WinMultiplierEvent : SlotEvent
{
    [Tooltip("Multiplier factor applied to the win value (e.g., 2 for double, 3 for triple).")]
    [SerializeField]
    private int multiplier = 2;

    [Tooltip("Apply multiplier to the triggering win only (if available) or to the entire accumulated win value.")]
    [SerializeField]
    private bool applyToTriggeringWinOnly = false;

    [Tooltip("Optional message to display when the multiplier is applied.")]
    [SerializeField]
    private string multiplierMessage = "Win Multiplied!";

    public int Multiplier => multiplier;

    public override void Execute(SlotEventContext context, Action onComplete)
    {
        try
        {
            if (context == null || context.Player == null)
            {
                Debug.LogWarning($"WinMultiplierEvent '{eventName}' executed with null context or player.");
                SafeComplete(onComplete);
                return;
            }

            int baseValue = 0;
            if (applyToTriggeringWinOnly && context.TriggeringWin != null)
            {
                baseValue = context.TriggeringWin.WinValue;
            }
            else
            {
                baseValue = context.AccumulatedWinValue;
            }

            // Calculate additional credits (original already awarded, so award multiplier-1 extra)
            int additionalCredits = baseValue * (multiplier - 1);

            if (additionalCredits > 0)
            {
                context.Player.AddCredits(additionalCredits);

                if (!string.IsNullOrEmpty(multiplierMessage) && SlotConsoleController.Instance != null && context.SlotsEngine != null)
                {
                    string message = $"{multiplierMessage} x{multiplier} (+{additionalCredits})";
                    SlotConsoleController.Instance.AppendPresentationMessage(context.SlotsEngine, message);
                }

                Debug.Log($"WinMultiplierEvent '{eventName}' applied x{multiplier} multiplier, awarded {additionalCredits} additional credits.");
            }
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
