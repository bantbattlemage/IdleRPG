using System.Linq;

public class PlayerDataManager : DataManager<PlayerDataManager, PlayerData>
{
	public override void LoadData(GameData persistantData)
	{
		LocalData = persistantData.CurrentPlayerData ?? new SerializableDictionary<int, PlayerData>();
	}

	public override void SaveData(GameData persistantData)
	{
		persistantData.CurrentPlayerData = LocalData ?? new SerializableDictionary<int, PlayerData>();
	}

	public PlayerData GetPlayerData()
	{
		// Do not trigger global load here. Callers should ensure data is loaded at app start.
		if (LocalData == null || LocalData.Count == 0)
		{
			return CreateNewPlayerData();
		}
		else
		{
			return LocalData.Values.First();
		}
	}

	private PlayerData CreateNewPlayerData()
	{
		PlayerData playerData = new PlayerData();

		AddNewData(playerData);

		// Do not automatically save here; let the caller decide when to persist
		return playerData;
	}
}
