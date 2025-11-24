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

		// Ensure contained SymbolData entries are registered with SymbolDataManager and that
		// the ReelData has up-to-date AccessorId references persisted.
		if (newData?.CurrentSymbolData != null)
		{
			for (int i = 0; i < newData.CurrentSymbolData.Count; i++)
			{
				var sym = newData.CurrentSymbolData[i];
				if (sym != null) SymbolDataManager.Instance.AddNewData(sym);
			}

			// After SymbolDataManager assigned AccessorIds, refresh the stored accessor id array
			newData.SetCurrentSymbolData(newData.CurrentSymbolData);
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