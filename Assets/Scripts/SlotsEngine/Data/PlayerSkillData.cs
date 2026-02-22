using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents a player-owned skill that can execute SlotEvents in response to game triggers.
/// PlayerSkills are persistent objects stored in the player's inventory and can be
/// equipped/assigned to specific SlotsEngine instances to modify gameplay.
/// 
/// Skills can trigger on various game events (spin complete, win, symbol land, etc.)
/// and execute a configured sequence of SlotEvents when their trigger conditions are met.
/// </summary>
[Serializable]
public class PlayerSkillData : Data
{
    [SerializeField] private string skillName;
    [SerializeField] private string description;
    
    [Tooltip("The game event that triggers this skill (e.g., SpinCompleted, ReelCompleted, etc.)")]
    [SerializeField] private SlotsEvent triggerEvent;
    
    [Tooltip("Optional: specific state that must be active for skill to trigger (e.g., only trigger in Presentation state)")]
    [SerializeField] private State requiredState = State.None;
    
    [Tooltip("Cooldown in spins before this skill can trigger again. 0 = no cooldown")]
    [SerializeField] private int cooldownSpins = 0;
    
    [Tooltip("If true, skill triggers only once per game session then deactivates")]
    [SerializeField] private bool oneTimeUse = false;
    
    [Tooltip("Ordered list of SlotEvents to execute when this skill triggers")]
    [SerializeField] private List<SlotEvent> skillEvents = new List<SlotEvent>();
    
    // Runtime state (not serialized - reset each session)
    [NonSerialized] private int currentCooldown = 0;
    [NonSerialized] private bool hasTriggered = false; // for one-time use skills
    [NonSerialized] private bool isEnabled = true;
    
    public string SkillName => skillName;
    public string Description => description;
    public SlotsEvent TriggerEvent => triggerEvent;
    public State RequiredState => requiredState;
    public int CooldownSpins => cooldownSpins;
    public bool OneTimeUse => oneTimeUse;
    public List<SlotEvent> SkillEvents => skillEvents;
    
    public bool IsEnabled => isEnabled;
    public bool IsOnCooldown => currentCooldown > 0;
    public int CurrentCooldown => currentCooldown;
    
    public PlayerSkillData(string name, SlotsEvent trigger, List<SlotEvent> events = null)
    {
        skillName = name;
        triggerEvent = trigger;
        skillEvents = events ?? new List<SlotEvent>();
        description = string.Empty;
        requiredState = State.None;
        cooldownSpins = 0;
        oneTimeUse = false;
    }
    
    /// <summary>
    /// Full constructor for creating PlayerSkillData with all parameters.
    /// </summary>
    public PlayerSkillData(string name, string desc, SlotsEvent trigger, State required, int cooldown, bool oneTime, List<SlotEvent> events)
    {
        skillName = name;
        description = desc;
        triggerEvent = trigger;
        requiredState = required;
        cooldownSpins = cooldown;
        oneTimeUse = oneTime;
        skillEvents = events ?? new List<SlotEvent>();
    }
    
    /// <summary>
    /// Check if this skill can currently trigger based on cooldown and one-time-use status.
    /// </summary>
    public bool CanTrigger()
    {
        if (!isEnabled) return false;
        if (oneTimeUse && hasTriggered) return false;
        if (currentCooldown > 0) return false;
        return true;
    }
    
    /// <summary>
    /// Check if the provided context meets this skill's triggering requirements.
    /// </summary>
    public bool MeetsRequirements(SlotEventContext context)
    {
        if (requiredState != State.None)
        {
            // If a required state is specified, check that the SlotsEngine is in that state
            if (context?.SlotsEngine != null && context.SlotsEngine.CurrentState != requiredState)
            {
                return false;
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Execute this skill's events with the provided context.
    /// Automatically manages cooldown and one-time-use flags.
    /// </summary>
    public void Execute(SlotEventContext context, Action onComplete)
    {
        if (!CanTrigger())
        {
            SafeComplete(onComplete);
            return;
        }
        
        if (!MeetsRequirements(context))
        {
            SafeComplete(onComplete);
            return;
        }
        
        // Mark as triggered for one-time skills
        if (oneTimeUse)
        {
            hasTriggered = true;
        }
        
        // Start cooldown
        if (cooldownSpins > 0)
        {
            currentCooldown = cooldownSpins;
        }
        
        // Execute skill events
        if (skillEvents == null || skillEvents.Count == 0)
        {
            Debug.LogWarning($"PlayerSkill '{skillName}' has no events configured.");
            SafeComplete(onComplete);
            return;
        }
        
        SlotEventManager.Instance.ExecuteEventSequence(skillEvents, context, onComplete);
    }
    
    /// <summary>
    /// Decrement cooldown by one spin. Call this when a spin completes on an engine
    /// where this skill is equipped.
    /// </summary>
    public void TickCooldown()
    {
        if (currentCooldown > 0)
        {
            currentCooldown--;
        }
    }
    
    /// <summary>
    /// Reset cooldown to zero (useful for debugging or skill interactions).
    /// </summary>
    public void ResetCooldown()
    {
        currentCooldown = 0;
    }
    
    /// <summary>
    /// Reset one-time-use flag (useful for debugging or skill reactivation items).
    /// </summary>
    public void ResetOneTimeUse()
    {
        hasTriggered = false;
    }
    
    /// <summary>
    /// Enable or disable this skill. Disabled skills will not trigger.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        isEnabled = enabled;
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
