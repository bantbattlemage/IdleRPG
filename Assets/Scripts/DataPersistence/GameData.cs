[System.Serializable]
public class GameData
{
    public long lastUpdated;

	public SerializableDictionary<int, PlayerData> CurrentPlayerData;
}