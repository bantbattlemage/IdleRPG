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

	public override void AddNewData(ReelData newData)
	{
		base.AddNewData(newData);

		for (int i = 0; i < newData.CurrentSymbolData.Count; i++)
		{
			SymbolDataManager.Instance.AddNewData(newData.CurrentSymbolData[i]);
		}

		DataPersistenceManager.Instance.SaveGame();
	}

	public void RemoveDataIfExists(ReelData data)
	{
		if (data == null) return;
		if (LocalData != null && LocalData.ContainsKey(data.AccessorId))
		{
			LocalData.Remove(data.AccessorId);
			DataPersistenceManager.Instance.SaveGame();
		}
	}
}