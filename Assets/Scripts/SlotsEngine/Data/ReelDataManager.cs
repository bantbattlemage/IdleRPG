using System.Linq;

public class ReelDataManager : DataManager<ReelDataManager, ReelData>
{
	public override void LoadData(GameData persistantData)
	{
		LocalData = persistantData.CurrentReelData;
	}

	public override void SaveData(GameData persistantData)
	{
		persistantData.CurrentReelData = LocalData;
	}

	public ReelData GetPlayerData()
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