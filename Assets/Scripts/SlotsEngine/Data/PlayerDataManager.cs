using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SocialPlatforms;

public class PlayerDataManager : DataManager<PlayerDataManager, PlayerData>
{
	public override void LoadData(GameData persistantData)
	{
		LocalData = persistantData.CurrentPlayerData;
	}

	public override void SaveData(GameData persistantData)
	{
		persistantData.CurrentPlayerData = LocalData;
	}

	public PlayerData GetPlayerData()
	{
		DataPersistenceManager.Instance.LoadGame();

		if (LocalData.Count == 0)
		{
			return CreateNewPlayerData();
		}
		else
		{
			return LocalData.Values.First();
		}
	}

	public PlayerData CreateNewPlayerData()
	{
		DataPersistenceManager.Instance.LoadGame();

		PlayerData playerData = new PlayerData();

		AddNewData(playerData);

		DataPersistenceManager.Instance.SaveGame();

		return playerData;
	}
}
