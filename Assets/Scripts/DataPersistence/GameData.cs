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
}