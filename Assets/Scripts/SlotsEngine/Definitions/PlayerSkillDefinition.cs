using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject definition for creating PlayerSkill templates in the Unity editor.
/// Can be converted to runtime PlayerSkillData instances that are stored in player inventory.
/// </summary>
[CreateAssetMenu(menuName = "Slots/Player Skill Definition", fileName = "PlayerSkillDefinition")]
public class PlayerSkillDefinition : ScriptableObject
{
    [SerializeField] private string skillName = "New Skill";
    [TextArea(3, 6)]
    [SerializeField] private string description = "Skill description";
    
    [Header("Trigger Configuration")]
    [Tooltip("The game event that triggers this skill")]
    [SerializeField] private SlotsEvent triggerEvent = SlotsEvent.SpinCompleted;
    
    [Tooltip("Optional: specific state required for skill to trigger")]
    [SerializeField] private State requiredState = State.None;
    
    [Header("Cooldown & Usage")]
    [Tooltip("Cooldown in spins before skill can trigger again (0 = no cooldown)")]
    [SerializeField] private int cooldownSpins = 0;
    
    [Tooltip("If true, skill triggers only once per game session")]
    [SerializeField] private bool oneTimeUse = false;
    
    [Header("Effects")]
    [Tooltip("Ordered list of SlotEvents to execute when triggered")]
    [SerializeField] private List<SlotEvent> skillEvents = new List<SlotEvent>();
    
    public string SkillName => skillName;
    public string Description => description;
    public SlotsEvent TriggerEvent => triggerEvent;
    public State RequiredState => requiredState;
    public int CooldownSpins => cooldownSpins;
    public bool OneTimeUse => oneTimeUse;
    public List<SlotEvent> SkillEvents => skillEvents;
    
    /// <summary>
    /// Create a runtime PlayerSkillData instance from this definition.
    /// </summary>
    public PlayerSkillData CreateInstance()
    {
        var instance = new PlayerSkillData(
            skillName,
            description,
            triggerEvent,
            requiredState,
            cooldownSpins,
            oneTimeUse,
            new List<SlotEvent>(skillEvents)
        );
        
        // Assign accessor ID if not already set
        if (instance.AccessorId == 0)
        {
            instance.AccessorId = GlobalAccessorIdProvider.GetNextId();
        }
        
        return instance;
    }
}
