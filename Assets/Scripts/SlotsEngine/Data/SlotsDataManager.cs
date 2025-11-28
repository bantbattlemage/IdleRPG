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

		// Ensure the SlotsData.Index reflects the assigned AccessorId for display and ordering
		try
		{
			newData.Index = newData.AccessorId;
		}
		catch { }

		for (int i = 0; i < newData.CurrentReelData.Count; i++)
		{
			ReelDataManager.Instance.AddNewData(newData.CurrentReelData[i]);
		}
	}

	public void UpdateSlotsData(SlotsData slotsData)
	{
		if (slotsData == null) return;

		if (LocalData == null)
		{
			LocalData = new SerializableDictionary<int, SlotsData>();
		}

		if (slotsData.AccessorId == 0 || !LocalData.ContainsKey(slotsData.AccessorId))
		{
			// Treat as new slots data
			AddNewData(slotsData);
		}
		else
		{
			// Ensure any new reel data within the slots is added to ReelDataManager
			foreach (var reel in slotsData.CurrentReelData)
			{
				if (reel != null && reel.AccessorId == 0)
				{
					ReelDataManager.Instance.AddNewData(reel);
				}
			}

			// Ensure index matches accessor id for consistent display
			try { slotsData.Index = slotsData.AccessorId; } catch { }

			// Replace stored reference
			LocalData[slotsData.AccessorId] = slotsData;
		}

		// Debounced save request instead of immediate disk write
		DataPersistenceManager.Instance?.RequestSave();

		// Propagate changes to live SlotsEngine in a safe, surgical manner when possible
		try
		{
			var mgr = SlotsEngineManager.Instance;
			if (mgr != null)
			{
				var engine = mgr.FindEngineForSlotsData(slotsData);
				if (engine != null)
				{
					// Try a surgical update first; this will throw if engine is spinning
					try
					{
						engine.TryApplySlotsDataUpdate(slotsData);
						SlotsEngineManager.Instance.AdjustSlotCanvas(engine);
					}
					catch (System.InvalidOperationException)
					{
						// Engine is spinning; surface error to caller
						throw;
					}
					catch
					{
						// Other errors: attempt conservative reinit if engine is idle, otherwise surface
						if (engine.CurrentState != State.Spinning)
						{
							engine.InitializeSlotsEngine(engine.ReelsRootTransform, slotsData);
							SlotsEngineManager.Instance.AdjustSlotCanvas(engine);
						}
						else
						{
							throw new System.InvalidOperationException("Failed to apply SlotsData update and engine is spinning.");
						}
					}
				}
			}
		}
		catch
		{
			// Rethrow to surface to caller
			throw;
		}
	}

	public void RemoveSlotsDataIfExists(SlotsData data)
	{
		if (data == null || LocalData == null) return;

		if (LocalData.ContainsKey(data.AccessorId))
		{
			// Remove contained reel & symbol data to prevent orphaned entries
			foreach (var reel in data.CurrentReelData.ToList())
			{
				if (reel != null) ReelDataManager.Instance.RemoveDataIfExists(reel);
			}

			LocalData.Remove(data.AccessorId);
			// Debounced save
			DataPersistenceManager.Instance?.RequestSave();
		}
	}

	public void ClearSlotsData()
	{
		ClearData();
		ReelDataManager.Instance.ClearData();
		SymbolDataManager.Instance.ClearData();
		DataPersistenceManager.Instance?.RequestSave();
	}
}
