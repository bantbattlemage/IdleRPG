using System.Collections.Generic;
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

	public override void AddNewData(SlotsData newData)
	{
		base.AddNewData(newData);

		for (int i = 0; i < newData.CurrentReelData.Count; i++)
		{
			ReelDataManager.Instance.AddNewData(newData.CurrentReelData[i]);
		}
	}

	public void ClearSlotsData()
	{
		ClearData();
		ReelDataManager.Instance.ClearData();
		SymbolDataManager.Instance.ClearData();
		DataPersistenceManager.Instance.SaveGame();
	}
}
