using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages persistence and runtime access to PlayerSkillData instances.
/// Follows the established DataManager pattern used by other game systems.
/// </summary>
public class PlayerSkillDataManager : DataManager<PlayerSkillDataManager, PlayerSkillData>
{
    public override void LoadData(GameData persistantData)
    {
        LocalData = persistantData.CurrentPlayerSkillData;
        
        // Note: PlayerSkillData doesn't have runtime references that need restoration like SymbolData does.
        // SlotEvent references are ScriptableObjects that serialize/deserialize automatically.
    }

    public override void SaveData(GameData persistantData)
    {
        persistantData.CurrentPlayerSkillData = LocalData;
    }

    /// <summary>
    /// Remove a PlayerSkillData if it exists in the manager.
    /// </summary>
    public void RemoveDataIfExists(PlayerSkillData data)
    {
        if (data == null) return;
        if (LocalData != null && LocalData.ContainsKey(data.AccessorId))
        {
            LocalData.Remove(data.AccessorId);
            DataPersistenceManager.Instance?.RequestSave();
        }
    }
    
    /// <summary>
    /// Find all skills that are configured to trigger on a specific SlotsEvent.
    /// </summary>
    public List<PlayerSkillData> GetSkillsByTrigger(SlotsEvent trigger)
    {
        if (LocalData == null) return new List<PlayerSkillData>();
        
        return LocalData.Values.Where(skill => skill != null && skill.TriggerEvent == trigger).ToList();
    }
}
