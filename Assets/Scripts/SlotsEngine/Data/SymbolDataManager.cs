using System.Linq;

public class SymbolDataManager : DataManager<SymbolDataManager, SymbolData>
{
	public override void LoadData(GameData persistantData)
	{
		LocalData = persistantData.CurrentSymbolData;
	}

	public override void SaveData(GameData persistantData)
	{
		persistantData.CurrentSymbolData = LocalData;
	}

	public SymbolData GetPlayerData()
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