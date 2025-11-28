using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

		// Do not override display Index with persistence AccessorId; keep Index decoupled.
		// Ensure contained reels are registered.
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

		bool isNew = (slotsData.AccessorId == 0 || !LocalData.ContainsKey(slotsData.AccessorId));

		if (isNew)
		{
			AddNewData(slotsData);
			Debug.Log($"[SlotsDataManager] Added new SlotsData accessor={slotsData.AccessorId} index={slotsData.Index} reels={slotsData.CurrentReelData?.Count ?? 0}");
		}
		else
		{
			// Ensure any new reel data within the slots is added to ReelDataManager
			foreach (var reel in slotsData.CurrentReelData)
			{
				if (reel != null && reel.AccessorId == 0)
				{
					ReelDataManager.Instance.AddNewData(reel);
					Debug.Log($"[SlotsDataManager] Registered new ReelData for slot accessor={slotsData.AccessorId}, reelAccessor={reel.AccessorId}");
				}
			}

			// Do not force Index to match AccessorId; maintain existing Index.

			// Replace stored reference
			LocalData[slotsData.AccessorId] = slotsData;
			Debug.Log($"[SlotsDataManager] Updated SlotsData accessor={slotsData.AccessorId} reels={slotsData.CurrentReelData?.Count ?? 0}");
		}

		// Debounced save request instead of immediate disk write
		DataPersistenceManager.Instance?.RequestSave();

		// Broadcast global notification so UI can refresh
		try { GlobalEventManager.Instance?.BroadcastEvent(SlotsEvent.ReelAdded, slotsData); } catch { }

		// Propagate changes to live SlotsEngine in a safe manner
		var mgr = SlotsEngineManager.Instance;
		if (mgr == null)
		{
			Debug.LogWarning("[SlotsDataManager] SlotsEngineManager.Instance is null. UI may be out of sync.");
			return;
		}

		var engine = mgr.FindEngineForSlotsData(slotsData);
		if (engine == null)
		{
			Debug.LogWarning($"[SlotsDataManager] No engine found for SlotsData accessor={slotsData.AccessorId}. Changes persisted but not applied to UI.");
			return;
		}

		// Log before applying to engine
		Debug.Log($"[SlotsDataManager] Applying update to engine slotAccessor={slotsData.AccessorId}, state={engine.CurrentState}, engineReels={engine.CurrentReels?.Count ?? 0}, dataReels={slotsData.CurrentReelData?.Count ?? 0}");

		try
		{
			// Try a surgical update first; this will throw if engine is spinning
			engine.TryApplySlotsDataUpdate(slotsData);
			SlotsEngineManager.Instance.AdjustSlotCanvas(engine);
			Debug.Log($"[SlotsDataManager] Applied TryApplySlotsDataUpdate successfully for slotAccessor={slotsData.AccessorId}. EngineReels now={engine.CurrentReels?.Count ?? 0}");
		}
		catch (System.InvalidOperationException ex)
		{
			Debug.LogWarning($"[SlotsDataManager] Engine refused update (spinning) for slotAccessor={slotsData.AccessorId}: {ex.Message}");
			throw;
		}
		catch (System.Exception ex)
		{
			// Attempt conservative reinit if engine is idle
			if (engine.CurrentState != State.Spinning)
			{
				Debug.LogWarning($"[SlotsDataManager] TryApplySlotsDataUpdate failed for slotAccessor={slotsData.AccessorId}. Reinitializing engine. Error: {ex.Message}");
				engine.InitializeSlotsEngine(engine.ReelsRootTransform, slotsData);
				SlotsEngineManager.Instance.AdjustSlotCanvas(engine);
				Debug.Log($"[SlotsDataManager] Engine reinitialized for slotAccessor={slotsData.AccessorId}. EngineReels now={engine.CurrentReels?.Count ?? 0}");
			}
			else
			{
				Debug.LogError($"[SlotsDataManager] Failed to apply SlotsData update and engine is spinning for slotAccessor={slotsData.AccessorId}. Error: {ex.Message}");
				throw new System.InvalidOperationException("Failed to apply SlotsData update and engine is spinning.");
			}
		}
	}

	public void RemoveSlotsDataIfExists(SlotsData data)
	{
		if (data == null || LocalData == null) return;

		if (LocalData.ContainsKey(data.AccessorId))
		{
			// Do NOT remove contained reel & symbol data here. Keeping ReelData and SymbolData
			// allows previously created runtime reels/strips to remain available in the AddReel UI
			// so players can re-use them across slots.
			//
			// Previous behavior removed each contained ReelData via ReelDataManager.Instance.RemoveDataIfExists(reel);
			// That caused runtime reels to disappear from the ReelDataManager and made them unavailable
			// for reattachment to new slots. To preserve player-owned runtime data, we only remove
			// the SlotsData entry itself.

			LocalData.Remove(data.AccessorId);
			// Debounced save
			DataPersistenceManager.Instance?.RequestSave();
			Debug.Log($"[SlotsDataManager] Removed SlotsData accessor={data.AccessorId}");
		}
	}

	public void ClearSlotsData()
	{
		ClearData();
		ReelDataManager.Instance.ClearData();
		SymbolDataManager.Instance.ClearData();
		DataPersistenceManager.Instance?.RequestSave();
		Debug.Log("[SlotsDataManager] Cleared all slots, reels, symbols data.");
	}
}
