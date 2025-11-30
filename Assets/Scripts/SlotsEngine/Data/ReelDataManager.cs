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
		int beforeCount = LocalData != null ? LocalData.Count : 0;
		int incomingAccessor = newData != null ? newData.AccessorId : -1;

		// If this ReelData already has a persisted AccessorId and is present in LocalData, avoid re-adding
		if (newData != null && newData.AccessorId > 0 && LocalData != null && LocalData.ContainsKey(newData.AccessorId))
		{
			return;
		}
		base.AddNewData(newData);
		int afterCount = LocalData != null ? LocalData.Count : 0;

		if (newData?.CurrentSymbolData != null && SymbolDataManager.Instance != null)
		{
			for (int i = 0; i < newData.CurrentSymbolData.Count; i++)
			{
				var sym = newData.CurrentSymbolData[i];
				if (sym != null && sym.AccessorId == 0)
				{
					SymbolDataManager.Instance.AddNewData(sym);
				}
			}

			newData.SetCurrentSymbolData(newData.CurrentSymbolData);
		}

		DataPersistenceManager.Instance?.RequestSave();
	}

	public void RemoveDataIfExists(ReelData data)
	{
		if (data == null) return;

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
			DataPersistenceManager.Instance?.RequestSave();
		}
	}

	public void SoftUpdateSymbolCount(ReelData reelData, int desiredCount)
	{
		if (reelData == null) return;
		if (desiredCount < 1) desiredCount = 1;

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
		DataPersistenceManager.Instance?.RequestSave();
	}
}