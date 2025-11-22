using System.Linq;
using System.Collections.Generic;

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

		// Remove contained symbol data entries first
		if (data.CurrentSymbolData != null)
		{
			foreach (var sym in data.CurrentSymbolData.ToList())
			{
				if (sym != null)
				{
					SymbolDataManager.Instance.RemoveDataIfExists(sym);
				}
			}
		}

		if (LocalData != null && LocalData.ContainsKey(data.AccessorId))
		{
			LocalData.Remove(data.AccessorId);
			DataPersistenceManager.Instance.SaveGame();
		}
	}

	public void SoftUpdateSymbolCount(ReelData reelData, int desiredCount)
	{
		if (reelData == null) return;
		if (desiredCount < 1) desiredCount = 1;

		// Adjust underlying symbol data list length gracefully maintaining existing entries
		List<SymbolData> list = reelData.CurrentSymbolData != null
			? new List<SymbolData>(reelData.CurrentSymbolData)
			: new List<SymbolData>();

		if (list.Count > desiredCount)
		{
			list.RemoveRange(desiredCount, list.Count - desiredCount);
		}
		else if (list.Count < desiredCount)
		{
			for (int i = list.Count; i < desiredCount; i++)
			{
				list.Add(reelData.CurrentReelStrip?.GetWeightedSymbol());
			}
		}

		reelData.SetCurrentSymbolData(list);
		DataPersistenceManager.Instance.SaveGame();
	}
}