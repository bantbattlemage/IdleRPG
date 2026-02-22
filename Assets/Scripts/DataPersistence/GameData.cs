using System;

[System.Serializable]
/// <summary>
/// Root save data container serialized to disk per profile.
/// Contains dictionaries for all runtime data types keyed by their manager-assigned AccessorIds.
/// </summary>
public class GameData
{
	public long lastUpdated;

	public SerializableDictionary<int, PlayerData> CurrentPlayerData;
	public SerializableDictionary<int, SlotsData> CurrentSlotsData;
	public SerializableDictionary<int, ReelData> CurrentReelData;
	public SerializableDictionary<int, SymbolData> CurrentSymbolData;
	public SerializableDictionary<int, ReelStripData> CurrentReelStripData;
	public SerializableDictionary<int, BetLevelData> BetLevelData;
	public SerializableDictionary<int, PlayerSkillData> CurrentPlayerSkillData;

	// Persist the last globally-assigned accessor id so a centralized provider can resume monotonic ids across sessions
	public int LastAssignedAccessorId;

	public GameData()
	{
		lastUpdated = DateTime.Now.ToBinary();

		CurrentPlayerData = new SerializableDictionary<int, PlayerData>();
		CurrentSlotsData = new SerializableDictionary<int, SlotsData>();
		CurrentReelData = new SerializableDictionary<int, ReelData>();
		CurrentSymbolData = new SerializableDictionary<int, SymbolData>();
		CurrentReelStripData = new SerializableDictionary<int, ReelStripData>();
		BetLevelData = new SerializableDictionary<int, BetLevelData>();
		CurrentPlayerSkillData = new SerializableDictionary<int, PlayerSkillData>();

		LastAssignedAccessorId = 0;
	}
}