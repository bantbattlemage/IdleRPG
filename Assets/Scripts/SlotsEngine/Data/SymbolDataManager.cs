using System.Linq;

public class SymbolDataManager : DataManager<SymbolDataManager, SymbolData>
{
	public override void LoadData(GameData persistantData)
	{
		LocalData = persistantData.CurrentSymbolData;
		
		// After loading, ensure RuntimeEventTrigger is initialized for all symbols
		if (LocalData != null)
		{
			foreach (var symbolData in LocalData.Values)
			{
				if (symbolData != null)
				{
					symbolData.EnsureRuntimeEventTriggerInitialized();
				}
			}
		}
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
			// Debounced save request
			DataPersistenceManager.Instance?.RequestSave();
		}
	}
}