using System;

[System.Serializable]
public class GameData
{
	public long lastUpdated;

	public SerializableDictionary<int, PlayerData> CurrentPlayerData;
	public SerializableDictionary<int, SlotsData> CurrentSlotsData;
	public SerializableDictionary<int, ReelData> CurrentReelData;
	public SerializableDictionary<int, SymbolData> CurrentSymbolData;
	public SerializableDictionary<int, ReelStripData> CurrentReelStripData;
	public SerializableDictionary<int, BetLevelData> BetLevelData;

	public GameData()
	{
		lastUpdated = DateTime.Now.ToBinary();

		CurrentPlayerData = new SerializableDictionary<int, PlayerData>();
		CurrentSlotsData = new SerializableDictionary<int, SlotsData>();
		CurrentReelData = new SerializableDictionary<int, ReelData>();
		CurrentSymbolData = new SerializableDictionary<int, SymbolData>();
		CurrentReelStripData = new SerializableDictionary<int, ReelStripData>();
		BetLevelData = new SerializableDictionary<int, BetLevelData>();
	}
}