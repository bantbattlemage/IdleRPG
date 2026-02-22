using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages the attachment and execution of PlayerSkills on SlotsEngine instances.
/// Skills can be equipped to specific engines or globally to all engines.
/// Handles event subscription, cooldown management, and skill triggering.
/// </summary>
public class PlayerSkillManager : Singleton<PlayerSkillManager>
{
    // Maps SlotsEngine instances to their equipped skills
    private Dictionary<SlotsEngine, List<PlayerSkillData>> engineSkills = new Dictionary<SlotsEngine, List<PlayerSkillData>>();
    
    // Global skills that apply to ALL engines
    private List<PlayerSkillData> globalSkills = new List<PlayerSkillData>();
    
    // Track which events we're listening to globally
    private HashSet<SlotsEvent> subscribedGlobalEvents = new HashSet<SlotsEvent>();
    
    protected override void Awake()
    {
        base.Awake();
    }
    
    /// <summary>
    /// Equip a skill to a specific SlotsEngine. The skill will only trigger on that engine.
    /// Uses GlobalEventManager since engine's EventManager is private. Skills filter by engine instance.
    /// </summary>
    public void EquipSkill(PlayerSkillData skill, SlotsEngine engine)
    {
        if (skill == null || engine == null) return;
        
        if (!engineSkills.TryGetValue(engine, out var skills))
        {
            skills = new List<PlayerSkillData>();
            engineSkills[engine] = skills;
        }
        
        if (!skills.Contains(skill))
        {
            skills.Add(skill);
            // Subscribe to global event manager and filter by engine in handler
            SubscribeToGlobalTriggerEvent(skill.TriggerEvent);
            Debug.Log($"Equipped skill '{skill.SkillName}' to engine {engine.CurrentSlotsData?.Index ?? -1}");
        }
    }
    
    /// <summary>
    /// Equip a skill globally. It will trigger on ALL SlotsEngine instances.
    /// </summary>
    public void EquipSkillGlobally(PlayerSkillData skill)
    {
        if (skill == null) return;
        
        if (!globalSkills.Contains(skill))
        {
            globalSkills.Add(skill);
            SubscribeToGlobalTriggerEvent(skill.TriggerEvent);
            Debug.Log($"Equipped skill '{skill.SkillName}' globally");
        }
    }
    
    /// <summary>
    /// Unequip a skill from a specific engine.
    /// </summary>
    public void UnequipSkill(PlayerSkillData skill, SlotsEngine engine)
    {
        if (skill == null || engine == null) return;
        
        if (engineSkills.TryGetValue(engine, out var skills))
        {
            skills.Remove(skill);
            if (skills.Count == 0)
            {
                engineSkills.Remove(engine);
            }
        }
    }
    
    /// <summary>
    /// Unequip a global skill.
    /// </summary>
    public void UnequipGlobalSkill(PlayerSkillData skill)
    {
        if (skill == null) return;
        globalSkills.Remove(skill);
    }
    
    /// <summary>
    /// Remove all skills from an engine (call when engine is destroyed).
    /// </summary>
    public void ClearEngineSkills(SlotsEngine engine)
    {
        if (engine == null) return;
        engineSkills.Remove(engine);
    }
    
    /// <summary>
    /// Get all skills equipped to a specific engine.
    /// </summary>
    public List<PlayerSkillData> GetEngineSkills(SlotsEngine engine)
    {
        if (engine == null) return new List<PlayerSkillData>();
        
        if (engineSkills.TryGetValue(engine, out var skills))
        {
            return new List<PlayerSkillData>(skills);
        }
        
        return new List<PlayerSkillData>();
    }
    
    /// <summary>
    /// Get all globally equipped skills.
    /// </summary>
    public List<PlayerSkillData> GetGlobalSkills()
    {
        return new List<PlayerSkillData>(globalSkills);
    }
    
    private void SubscribeToGlobalTriggerEvent(SlotsEvent trigger)
    {
        if (subscribedGlobalEvents.Contains(trigger)) return;
        
        // Subscribe to global event manager
        GlobalEventManager.Instance.RegisterEvent(trigger, OnGlobalSkillTriggerEvent);
        subscribedGlobalEvents.Add(trigger);
    }
    
    private void OnGlobalSkillTriggerEvent(object eventData)
    {
        // Extract SlotsEvent from eventData if it's an enum
        SlotsEvent trigger = SlotsEvent.SpinCompleted; // default
        
        // Try to get actual trigger event - GlobalEventManager passes enum as event key
        // but we need to handle it differently. For now, iterate all triggers.
        // This is called for each subscribed event type separately
        
        foreach (var subscribedEvent in subscribedGlobalEvents)
        {
            HandleSkillTrigger(subscribedEvent, eventData);
        }
    }
    
    private void HandleSkillTrigger(SlotsEvent trigger, object eventData)
    {
        // Determine which engine triggered this event
        SlotsEngine triggeringEngine = null;
        
        // Try to extract engine from event data
        if (eventData is SlotsEngine engine)
        {
            triggeringEngine = engine;
        }
        else if (eventData is GameReel reel)
        {
            triggeringEngine = reel.OwnerEngine;
        }
        
        // Handle engine-specific skills
        if (triggeringEngine != null && engineSkills.TryGetValue(triggeringEngine, out var skills))
        {
            var matchingSkills = skills.Where(s => s != null && s.TriggerEvent == trigger && s.CanTrigger()).ToList();
            
            if (matchingSkills.Count > 0)
            {
                SlotEventContext context = new SlotEventContext
                {
                    SlotsEngine = triggeringEngine,
                    Player = GamePlayer.Instance,
                    CurrentBet = GamePlayer.Instance?.CurrentBet,
                    CustomData = eventData
                };
                
                ExecuteSkillsSequentially(matchingSkills, context, 0);
            }
        }
        
        // Handle global skills
        var globalMatchingSkills = globalSkills.Where(s => s != null && s.TriggerEvent == trigger && s.CanTrigger()).ToList();
        
        if (globalMatchingSkills.Count > 0)
        {
            SlotEventContext context = new SlotEventContext
            {
                SlotsEngine = triggeringEngine, // may be null for truly global events
                Player = GamePlayer.Instance,
                CurrentBet = GamePlayer.Instance?.CurrentBet,
                CustomData = eventData
            };
            
            ExecuteSkillsSequentially(globalMatchingSkills, context, 0);
        }
    }
    
    private void ExecuteSkillsSequentially(List<PlayerSkillData> skills, SlotEventContext context, int index)
    {
        if (index >= skills.Count) return;
        
        var skill = skills[index];
        if (skill == null)
        {
            ExecuteSkillsSequentially(skills, context, index + 1);
            return;
        }
        
        try
        {
            skill.Execute(context, () =>
            {
                // Execute next skill when current completes
                ExecuteSkillsSequentially(skills, context, index + 1);
            });
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            ExecuteSkillsSequentially(skills, context, index + 1);
        }
    }
    
    /// <summary>
    /// Tick cooldowns for all skills equipped to a specific engine.
    /// Call this when a spin completes on that engine.
    /// </summary>
    public void TickEngineCooldowns(SlotsEngine engine)
    {
        if (engine == null) return;
        
        if (engineSkills.TryGetValue(engine, out var skills))
        {
            foreach (var skill in skills)
            {
                if (skill != null)
                {
                    skill.TickCooldown();
                }
            }
        }
        
        // Also tick global skills
        foreach (var skill in globalSkills)
        {
            if (skill != null)
            {
                skill.TickCooldown();
            }
        }
    }
}
