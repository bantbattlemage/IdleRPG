using System.Linq;

public class SlotsDataManager : DataManager<SlotsDataManager, SlotsData>
{
	public override void LoadData(GameData persistantData)
	{
		LocalData = persistantData.CurrentSlotsData;
	}

	public override void SaveData(GameData persistantData)
	{
		persistantData.CurrentSlotsData = LocalData;
	}

	public SlotsData GetPlayerData()
	{
		DataPersistenceManager.Instance.LoadGame();

		if (LocalData.Count == 0)
		{
			return null;
		}
		else
		{
			return LocalData.Values.First();
		}
	}
}
