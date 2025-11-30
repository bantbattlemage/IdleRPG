using System;
using UnityEngine;

public class ReelStripDataManager : DataManager<ReelStripDataManager, ReelStripData>
{
	public override void LoadData(GameData persistantData)
	{
		LocalData = persistantData.CurrentReelStripData;
	}

	public override void SaveData(GameData persistantData)
	{
		persistantData.CurrentReelStripData = LocalData;
	}

	public override void AddNewData(ReelStripData newData)
	{
		if (newData == null) return;
		base.AddNewData(newData);
		var list = newData.RuntimeSymbols;
		if (list != null && SymbolDataManager.Instance != null)
		{
			for (int i = 0; i < list.Count; i++)
			{
				var sym = list[i];
				// Only register symbols that are newly created (AccessorId == 0). Persisted symbols will
				// be loaded by SymbolDataManager.LoadData and should not be duplicated here.
				if (sym != null && sym.AccessorId == 0)
				{
					SymbolDataManager.Instance.AddNewData(sym);
				}
			}
		}
	}

	public void UpdateRuntimeStrip(ReelStripData strip)
	{
		if (strip == null) return;
		if (LocalData == null) LocalData = new SerializableDictionary<int, ReelStripData>();
		if (strip.AccessorId == 0 || !LocalData.ContainsKey(strip.AccessorId))
		{
			AddNewData(strip);
		}
		else
		{
			LocalData[strip.AccessorId] = strip;
		}

		// Validate runtime symbols: ensure SpriteKey resolves and maps to a definition or persisted SymbolData
		if (strip.RuntimeSymbols != null)
		{
			for (int i = 0; i < strip.RuntimeSymbols.Count; i++)
			{
				var s = strip.RuntimeSymbols[i];
				if (s == null) continue;
				// SpriteKey must be present
				if (string.IsNullOrEmpty(s.SpriteKey))
				{
					throw new InvalidOperationException($"ReelStripDataManager.UpdateRuntimeStrip: runtime SymbolData (AccessorId={s.AccessorId}, Name='{s.Name ?? "<unnamed>"}') has no SpriteKey. Persisted runtime symbols must include a valid sprite key.");
				}
				// Sprite must resolve
				var sprite = AssetResolver.ResolveSprite(s.SpriteKey);
				if (sprite == null)
				{
					throw new InvalidOperationException($"ReelStripDataManager.UpdateRuntimeStrip: failed to resolve sprite for SpriteKey='{s.SpriteKey}' on runtime SymbolData (AccessorId={s.AccessorId}, Name='{s.Name ?? "<unnamed>"}').");
				}
				// Must match either an authoring SymbolDefinition (by sprite key) or an existing persisted SymbolData
				bool matchesDefinition = false;
				if (SymbolDefinitionManager.Instance != null)
				{
					var def = SymbolDefinitionManager.Instance.GetDefinitionOrNull(s.SpriteKey);
					if (def != null) matchesDefinition = true;
				}
				bool matchesPersisted = false;
				if (SymbolDataManager.Instance != null)
				{
					var all = SymbolDataManager.Instance.GetAllData();
					for (int j = 0; j < all.Count; j++) { var sd = all[j]; if (sd == null) continue; if (!string.IsNullOrEmpty(sd.SpriteKey) && string.Equals(sd.SpriteKey, s.SpriteKey, System.StringComparison.OrdinalIgnoreCase)) { matchesPersisted = true; break; } }
				}
				if (!matchesDefinition && !matchesPersisted)
				{
					throw new InvalidOperationException($"ReelStripDataManager.UpdateRuntimeStrip: runtime SymbolData (AccessorId={s.AccessorId}, Name='{s.Name ?? "<unnamed>"}', SpriteKey='{s.SpriteKey}') does not match any SymbolDefinition or persisted SymbolData. Persisted runtime symbols must reference valid definitions or persisted symbol accessors.");
				}
			}
		}

		DataPersistenceManager.Instance?.RequestSave();
		// Notify UI and other systems that a reel strip has changed so they can refresh
		GlobalEventManager.Instance?.BroadcastEvent(SlotsEvent.ReelStripUpdated, strip);
	}

	public void RemoveDataIfExists(ReelStripData strip)
	{
		if (strip == null || LocalData == null) return;
		if (LocalData.ContainsKey(strip.AccessorId))
		{
			LocalData.Remove(strip.AccessorId);
			// Disassociate any inventory items pointing to this strip's accessor id
			var pd = GamePlayer.Instance?.PlayerData;
			if (pd != null)
			{
				var syms = pd.Inventory?.GetItemsOfType(InventoryItemType.Symbol);
				if (syms != null)
				{
					foreach (var inv in syms)
					{
						if (inv != null && inv.DefinitionAccessorId == strip.AccessorId) inv.SetDefinitionAccessorId(0);
					}
				}
			}
			DataPersistenceManager.Instance?.RequestSave();
			// Notify UI that a strip was removed/changed
			GlobalEventManager.Instance?.BroadcastEvent(SlotsEvent.ReelStripUpdated, strip);
		}
	}

	public SerializableDictionary<int, ReelStripData> ReadOnlyLocalData => LocalData;

	/// <summary>
	/// Debug helper: log all managed reel strips and their runtime symbol entries.
	/// Call from runtime (e.g., via console or button) to inspect persisted runtime symbol data.
	/// </summary>
	public void DebugDumpAllRuntimeSymbols()
	{
		if (LocalData == null || LocalData.Count == 0)
		{
			return;
		}

		foreach (var kv in LocalData)
		{
			var strip = kv.Value;
			if (strip == null)
			{
				continue;
			}
			var sb = new System.Text.StringBuilder();
			if (strip.RuntimeSymbols != null)
			{
				for (int i = 0; i < strip.RuntimeSymbols.Count; i++)
				{
					var s = strip.RuntimeSymbols[i];
					if (s == null) { sb.Append("(null)"); }
					else { sb.AppendFormat("{0}(id={1},key={2},hasSprite={3})", s.Name ?? "<unnamed>", s.AccessorId, string.IsNullOrEmpty(s.SpriteKey) ? "<none>" : s.SpriteKey, s.Sprite != null); }
					if (i + 1 < strip.RuntimeSymbols.Count) sb.Append(", ");
				}
			}
			// Intentionally do not write to Debug.Log here to avoid noisy logs; callers can inspect returned data or opt-in to logging
		}
	}
}