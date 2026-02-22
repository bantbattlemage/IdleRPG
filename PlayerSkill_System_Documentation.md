# PlayerSkill System - Complete Documentation

## Overview

The **PlayerSkill** system provides persistent, inventory-based skills that can execute SlotEvents in response to game triggers. Skills can be equipped to specific SlotsEngine instances or globally, and include cooldown management, one-time-use flags, and state requirements.

## Architecture

### Core Components

1. **PlayerSkillData** - `Assets\Scripts\SlotsEngine\Data\PlayerSkillData.cs`
   - Persistent runtime data object (inherits from `Data`)
   - Contains skill configuration and runtime state
   - Stored in player inventory and persisted across sessions

2. **PlayerSkillDataManager** - `Assets\Scripts\SlotsEngine\Data\PlayerSkillDataManager.cs`
   - Manages persistence and access to PlayerSkillData instances
   - Follows established DataManager pattern
   - Integrates with save/load system

3. **PlayerSkillManager** - `Assets\Scripts\SlotsEngine\GameControllers\PlayerSkillManager.cs`
   - Runtime singleton that manages skill hooking to engines
   - Handles event subscription and skill triggering
   - Manages cooldowns and execution

4. **PlayerSkillDefinition** - `Assets\Scripts\SlotsEngine\Definitions\PlayerSkillDefinition.cs`
   - ScriptableObject template for creating skills in Unity editor
   - Converts to PlayerSkillData at runtime
   - Design-time authoring tool

### Integration Points

- **GameData**: Added `CurrentPlayerSkillData` dictionary for persistence
- **DataPersistenceManager**: Loads PlayerSkillDataManager during game load
- **InventoryItemType**: Added `Skill` enum value for inventory support
- **SlotEvent System**: Skills execute lists of SlotEvents

## PlayerSkillData Structure

```csharp
[Serializable]
public class PlayerSkillData : Data
{
    // Configuration (persisted)
    private string skillName;
    private string description;
    private SlotsEvent triggerEvent;        // When to trigger
    private State requiredState;            // Optional state requirement
    private int cooldownSpins;              // Cooldown in spins
    private bool oneTimeUse;                // Single-use flag
    private List<SlotEvent> skillEvents;    // Events to execute
    
    // Runtime state (not persisted - reset each session)
    private int currentCooldown;
    private bool hasTriggered;              // For one-time skills
    private bool isEnabled;
}
```

### Key Properties

- **TriggerEvent**: SlotsEvent that activates the skill (e.g., SpinCompleted, ReelCompleted)
- **RequiredState**: Optional state constraint (e.g., only trigger in Presentation state)
- **CooldownSpins**: Number of spins before skill can trigger again (0 = no cooldown)
- **OneTimeUse**: If true, skill triggers once then deactivates permanently
- **SkillEvents**: Ordered list of SlotEvents to execute when triggered

### Key Methods

```csharp
bool CanTrigger()                           // Check if skill is ready to fire
bool MeetsRequirements(SlotEventContext)    // Check if state requirements are met
void Execute(SlotEventContext, Action)      // Execute the skill's events
void TickCooldown()                         // Decrement cooldown by one spin
void SetEnabled(bool)                       // Enable/disable skill
```

## PlayerSkillManager Usage

### Equipping Skills

```csharp
// Equip to a specific engine
PlayerSkillManager.Instance.EquipSkill(skillData, slotsEngine);

// Equip globally (triggers on all engines)
PlayerSkillManager.Instance.EquipSkillGlobally(skillData);
```

### Unequipping Skills

```csharp
// Unequip from specific engine
PlayerSkillManager.Instance.UnequipSkill(skillData, slotsEngine);

// Unequip globally
PlayerSkillManager.Instance.UnequipGlobalSkill(skillData);
```

### Cleanup

```csharp
// Remove all skills from an engine (call when destroying)
PlayerSkillManager.Instance.ClearEngineSkills(slotsEngine);
```

### Cooldown Management

```csharp
// Tick cooldowns when a spin completes
PlayerSkillManager.Instance.TickEngineCooldowns(slotsEngine);
```

## Creating Skills

### Method 1: Unity Editor (Recommended)

1. **Create PlayerSkillDefinition asset**:
   - Right-click in Project: `Create > Slots > Player Skill Definition`
   - Configure trigger event, cooldown, and effects
   - Add SlotEvents to the `Skill Events` list

2. **Convert to runtime instance**:
```csharp
PlayerSkillDefinition definition = // load asset
PlayerSkillData skillData = definition.CreateInstance();

// Add to player inventory
var inventoryItem = new InventoryItemData(
    skillData.SkillName, 
    InventoryItemType.Skill,
    skillData.AccessorId
);
playerData.AddInventoryItem(inventoryItem);

// Register with manager
PlayerSkillDataManager.Instance.AddNewData(skillData);

// Equip to engine
PlayerSkillManager.Instance.EquipSkill(skillData, engine);
```

### Method 2: Code (Runtime Creation)

```csharp
// Create skill programmatically
var skillEvents = new List<SlotEvent> { instantWinEvent, multiplierEvent };

var skillData = new PlayerSkillData(
    "Lucky Spin",                      // name
    "Doubles win value",               // description
    SlotsEvent.SpinCompleted,          // trigger
    State.Presentation,                // required state
    5,                                 // cooldown (spins)
    false,                             // one-time use
    skillEvents                        // events to execute
);

// Register and equip
PlayerSkillDataManager.Instance.AddNewData(skillData);
PlayerSkillManager.Instance.EquipSkillGlobally(skillData);
```

## Example Skills

### Example 1: Double Win on Lucky Spin

```csharp
// Create via definition
var winMultiplier = ScriptableObject.CreateInstance<WinMultiplierEvent>();
winMultiplier.multiplier = 2;

var skillDef = ScriptableObject.CreateInstance<PlayerSkillDefinition>();
skillDef.skillName = "Lucky Strike";
skillDef.triggerEvent = SlotsEvent.SpinCompleted;
skillDef.cooldownSpins = 10;
skillDef.skillEvents = new List<SlotEvent> { winMultiplier };

var skillData = skillDef.CreateInstance();
PlayerSkillManager.Instance.EquipSkillGlobally(skillData);
```

### Example 2: Instant Bonus on Reel Stop

```csharp
var instantWin = ScriptableObject.CreateInstance<InstantWinEvent>();
instantWin.creditAmount = 50;

var skillData = new PlayerSkillData(
    "Reel Bonus",
    "Awards 50 credits when reel stops",
    SlotsEvent.ReelCompleted,
    State.None,
    0,  // No cooldown
    false,
    new List<SlotEvent> { instantWin }
);

PlayerSkillDataManager.Instance.AddNewData(skillData);
// Equip to first engine only
PlayerSkillManager.Instance.EquipSkill(skillData, firstEngine);
```

### Example 3: One-Time Jackpot

```csharp
var jackpotWin = ScriptableObject.CreateInstance<InstantWinEvent>();
jackpotWin.creditAmount = 1000;

var skillData = new PlayerSkillData(
    "Mega Jackpot",
    "One-time 1000 credit bonus",
    SlotsEvent.SpinCompleted,
    State.Presentation,
    0,
    true,  // One-time use
    new List<SlotEvent> { jackpotWin }
);

PlayerSkillDataManager.Instance.AddNewData(skillData);
PlayerSkillManager.Instance.EquipSkillGlobally(skillData);
// After first trigger, hasTriggered = true, won't fire again
```

## Event Hooking Flow

```
1. PlayerSkillManager.EquipSkill(skill, engine)
   ?
2. Subscribe to engine.SlotsEventManager.RegisterEvent(skill.TriggerEvent)
   ?
3. When trigger fires:
   a. Check skill.CanTrigger() (cooldown, one-time-use)
   b. Check skill.MeetsRequirements(context) (state)
   c. Execute skill.Execute(context, onComplete)
   ?
4. skill.Execute():
   a. Mark as triggered (if one-time)
   b. Start cooldown
   c. SlotEventManager.Instance.ExecuteEventSequence(skillEvents)
   ?
5. After spin completes:
   PlayerSkillManager.TickEngineCooldowns(engine)
```

## Persistence Flow

### Save

```
GamePlayer.SaveData()
  ?
DataPersistenceManager.SaveGame()
  ?
PlayerSkillDataManager.SaveData(gameData)
  ?
gameData.CurrentPlayerSkillData = LocalData
  ?
Serialize to JSON file
```

### Load

```
DataPersistenceManager.LoadGame()
  ?
PlayerSkillDataManager.LoadData(gameData)
  ?
LocalData = gameData.CurrentPlayerSkillData
  ?
Skills available for equipping
```

**Note**: Runtime state (cooldown, hasTriggered, isEnabled) is NOT persisted. Skills reset to default state each session. This is intentional to prevent exploits and ensure consistent gameplay.

## Integration with Inventory

### Adding Skill to Inventory

```csharp
PlayerSkillData skill = // create or load
var inventoryItem = new InventoryItemData(
    skill.SkillName,
    InventoryItemType.Skill,
    skill.AccessorId
);
playerData.AddInventoryItem(inventoryItem);
```

### Retrieving Skill from Inventory

```csharp
// Get all skill inventory items
var skillItems = playerData.GetItemsOfType(InventoryItemType.Skill);

// Load skill data by accessor ID
foreach (var item in skillItems)
{
    if (PlayerSkillDataManager.Instance.TryGetData(item.DefinitionAccessorId, out var skillData))
    {
        // Use skillData
        PlayerSkillManager.Instance.EquipSkill(skillData, engine);
    }
}
```

## Advanced Features

### Conditional Triggering

Skills support conditional execution via `RequiredState`:

```csharp
var skillData = new PlayerSkillData(
    "Presentation Bonus",
    "Only triggers during presentation",
    SlotsEvent.SpinCompleted,
    State.Presentation,  // ? Only fires in Presentation state
    0, false, events
);
```

### Multi-Event Combos

Chain multiple SlotEvents for complex behaviors:

```csharp
var events = new List<SlotEvent>
{
    instantWinEvent,      // Award 100 credits
    delayEvent,           // Wait 1 second
    multiplierEvent,      // Double next win
    freeSpinEvent         // Award free spin
};

var comboSkill = new PlayerSkillData(
    "Grand Combo",
    "Multi-stage bonus",
    SlotsEvent.SpinCompleted,
    State.None, 20, false, events
);
```

### Dynamic Enable/Disable

```csharp
// Temporarily disable skill
skillData.SetEnabled(false);

// Re-enable later
skillData.SetEnabled(true);

// Reset cooldown manually
skillData.ResetCooldown();

// Reset one-time-use flag (for testing/cheat codes)
skillData.ResetOneTimeUse();
```

## Debugging

### Check if Skill Can Fire

```csharp
if (skillData.CanTrigger())
{
    Debug.Log($"Skill {skillData.SkillName} is ready");
}
else
{
    Debug.Log($"Cooldown: {skillData.CurrentCooldown} spins remaining");
}
```

### Monitor Equipped Skills

```csharp
// Check engine skills
var engineSkills = PlayerSkillManager.Instance.GetEngineSkills(engine);
Debug.Log($"Engine has {engineSkills.Count} skills equipped");

// Check global skills
var globalSkills = PlayerSkillManager.Instance.GetGlobalSkills();
Debug.Log($"{globalSkills.Count} global skills active");
```

### Skill Execution Logging

Add logging to your SlotEvent implementations:

```csharp
public override void Execute(SlotEventContext context, Action onComplete)
{
    Debug.Log($"[{eventName}] Triggered by skill on engine {context.SlotsEngine?.CurrentSlotsData?.Index}");
    // ... event logic ...
}
```

## Best Practices

### 1. Use Definitions for Design-Time Skills
Create PlayerSkillDefinition assets for skills that are part of the game design. This allows designers to configure skills without touching code.

### 2. Register Skills Before Equipping
Always register skills with PlayerSkillDataManager before equipping:
```csharp
PlayerSkillDataManager.Instance.AddNewData(skillData);  // Register first
PlayerSkillManager.Instance.EquipSkill(skillData, engine);  // Then equip
```

### 3. Clean Up on Engine Destroy
When destroying a SlotsEngine, remove its skills:
```csharp
PlayerSkillManager.Instance.ClearEngineSkills(engine);
```

### 4. Tick Cooldowns Consistently
Call `TickEngineCooldowns()` at the end of each spin to ensure cooldowns decrement properly.

### 5. Test State Requirements
When using `RequiredState`, verify the state machine transitions are working as expected.

## Troubleshooting

### Skills Not Triggering

**Check**:
1. Is skill equipped? `GetEngineSkills()` / `GetGlobalSkills()`
2. Is skill enabled? `skillData.IsEnabled`
3. Is skill on cooldown? `skillData.CurrentCooldown > 0`
4. Has one-time skill already triggered? `skillData.OneTimeUse && hasTriggered`
5. Does state match requirement? `context.SlotsEngine.CurrentState == skillData.RequiredState`
6. Is event actually firing? Add debug log to event handler

### Cooldowns Not Decrementing

Ensure `PlayerSkillManager.TickEngineCooldowns(engine)` is being called after each spin completes.

### Skills Lost After Reload

Verify:
1. PlayerSkillDataManager.SaveData() is being called
2. Skill has valid AccessorId > 0
3. GameData.CurrentPlayerSkillData is being serialized

## Files Created/Modified

### Created
```
Assets\Scripts\SlotsEngine\Data\
??? PlayerSkillData.cs
??? PlayerSkillDataManager.cs

Assets\Scripts\SlotsEngine\GameControllers\
??? PlayerSkillManager.cs

Assets\Scripts\SlotsEngine\Definitions\
??? PlayerSkillDefinition.cs
```

### Modified
```
Assets\Scripts\DataPersistence\GameData.cs
??? Added CurrentPlayerSkillData dictionary

Assets\Scripts\DataPersistence\DataPersistenceManager.cs
??? Added PlayerSkillDataManager load/save calls

Assets\Scripts\SlotsEngine\Data\PlayerInventory.cs
??? Added Skill to InventoryItemType enum
```

## Future Enhancements

### Potential Features
1. **Skill Leveling**: Add experience/level system to skills
2. **Skill Synergies**: Skills that boost each other when equipped together
3. **Conditional Triggers**: More complex trigger conditions (e.g., "on 3rd spin", "when credits < 100")
4. **Skill UI**: Visual indicators for active skills and cooldowns
5. **Skill Crafting**: Combine multiple skills to create new ones
6. **Persistent Cooldowns**: Option to persist cooldowns across sessions
7. **Skill Rarities**: Common/Rare/Epic skills with different power levels

## Summary

The PlayerSkill system provides a flexible, persistent framework for adding skill-based gameplay to the slots engine. Skills integrate seamlessly with the existing SlotEvent system, inventory, and save/load infrastructure, enabling rich gameplay customization without modifying core engine logic.

**Key Benefits**:
- ? Persistent across sessions
- ? Inventory-based (tradeable/collectible)
- ? Engine-specific or global scope
- ? Cooldown management built-in
- ? State-aware triggering
- ? Composes with existing SlotEvents
- ? Designer-friendly (ScriptableObject definitions)
