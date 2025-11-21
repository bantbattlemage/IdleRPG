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

	public void RemoveDataIfExists(SymbolData data)
	{
		if (data == null) return;
		if (LocalData != null && LocalData.ContainsKey(data.AccessorId))
		{
			LocalData.Remove(data.AccessorId);
			DataPersistenceManager.Instance.SaveGame();
		}
	}
}