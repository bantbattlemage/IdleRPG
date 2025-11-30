using System;
using UnityEngine;

/// <summary>
/// Modifies future spin behavior for a specific symbol. Example use cases:
/// - "Wins of symbol X pay double for the next N spins"
/// - "Symbol Y becomes wild for the next spin"
/// Note: Full implementation requires additional state tracking. This provides the scaffolding.
/// </summary>
[CreateAssetMenu(menuName = "Slots/Events/Symbol Modifier Event", fileName = "SymbolModifierEvent")]
public class SymbolModifierEvent : SlotEvent
{
    [Tooltip("Symbol to modify (by name or definition). If empty, applies to the triggering symbol.")]
    [SerializeField]
    private string targetSymbolName;

    [Tooltip("Type of modification to apply.")]
    [SerializeField]
    private ModifierType modifierType = ModifierType.WinMultiplier;

    [Tooltip("Duration in spins (0 = permanent until manually cleared).")]
    [SerializeField]
    private int durationSpins = 1;

    [Tooltip("Multiplier value (for WinMultiplier type).")]
    [SerializeField]
    private int multiplierValue = 2;

    [Tooltip("Optional message to display when the modifier is applied.")]
    [SerializeField]
    private string modifierMessage = "Symbol Modified!";

    public enum ModifierType
    {
        WinMultiplier,
        BecomeWild,
        IncreaseBaseValue
    }

    public override void Execute(SlotEventContext context, Action onComplete)
    {
        try
        {
            // TODO: Implement symbol modifier state tracking (could use a dedicated manager or attach to SlotsEngine)
            // For now, log the intended modification and display a message

            string targetSymbol = !string.IsNullOrEmpty(targetSymbolName) ? targetSymbolName : "triggering symbol";

            if (!string.IsNullOrEmpty(modifierMessage) && SlotConsoleController.Instance != null && context?.SlotsEngine != null)
            {
                string message = $"{modifierMessage} [{targetSymbol}] {modifierType} for {durationSpins} spin(s)";
                SlotConsoleController.Instance.AppendPresentationMessage(context.SlotsEngine, message);
            }

            Debug.Log($"SymbolModifierEvent '{eventName}' applied {modifierType} to '{targetSymbol}' for {durationSpins} spin(s). (State tracking not yet implemented)");
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
