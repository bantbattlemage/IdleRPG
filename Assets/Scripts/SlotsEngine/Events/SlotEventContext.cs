using System;
using UnityEngine;

/// <summary>
/// Context passed to SlotEvent.Execute providing access to relevant game state
/// and win information. This allows events to read/modify credits, access the
/// triggering slot engine, inspect win data, etc.
/// </summary>
[Serializable]
public class SlotEventContext
{
    /// <summary>
    /// The WinData that triggered this event (if applicable). May be null for events
    /// that are not triggered by wins.
    /// </summary>
    public WinData TriggeringWin { get; set; }

    /// <summary>
    /// Reference to the SlotsEngine instance where this event was triggered.
    /// </summary>
    public SlotsEngine SlotsEngine { get; set; }

    /// <summary>
    /// Reference to the GamePlayer for credit/bet modifications.
    /// </summary>
    public GamePlayer Player { get; set; }

    /// <summary>
    /// Current bet level definition at the time of event execution.
    /// </summary>
    public BetLevelDefinition CurrentBet { get; set; }

    /// <summary>
    /// Total win value accumulated before this event executes (useful for modifier events).
    /// </summary>
    public int AccumulatedWinValue { get; set; }

    /// <summary>
    /// Additional custom data that can be attached by event authors for specialized behaviors.
    /// </summary>
    public object CustomData { get; set; }

    public SlotEventContext()
    {
    }

    public SlotEventContext(WinData triggeringWin, SlotsEngine slotsEngine, GamePlayer player, BetLevelDefinition currentBet, int accumulatedWinValue = 0)
    {
        TriggeringWin = triggeringWin;
        SlotsEngine = slotsEngine;
        Player = player;
        CurrentBet = currentBet;
        AccumulatedWinValue = accumulatedWinValue;
    }
}
