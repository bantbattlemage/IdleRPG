using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EvaluatorCore;

public class AddRemoveSymbolsMenu : MonoBehaviour
{
	public RectTransform MenuRoot;
	public RectTransform SymbolListRoot;
	public SymbolDetailsItem SymbolDetailsItemPrefab;
	public Button CloseButton;

	private ReelStripData currentStrip;
	private SlotsData currentSlot;

	private void Start()
	{
		if (CloseButton != null) CloseButton.onClick.AddListener(OnCloseClicked);

		if (SymbolListRoot != null)
		{
			for (int i = SymbolListRoot.childCount - 1; i >= 0; i--)
			{
				var child = SymbolListRoot.GetChild(i);
				Destroy(child.gameObject);
			}
		}

		// Listen for external updates to reel strips so the menu can refresh if the current strip was changed elsewhere
		GlobalEventManager.Instance?.RegisterEvent(SlotsEvent.ReelStripUpdated, OnGlobalReelStripUpdated);
	}

	private void OnDestroy()
	{
		GlobalEventManager.Instance?.UnregisterEvent(SlotsEvent.ReelStripUpdated, OnGlobalReelStripUpdated);
	}

	public void Show(ReelStripData strip, SlotsData slot)
	{
		// Prefer canonical manager instance if available so UI reflects runtime-managed data.
		if (strip != null && ReelStripDataManager.Instance != null)
		{
			if (strip.AccessorId > 0 && ReelStripDataManager.Instance.TryGetData(strip.AccessorId, out var foundById))
			{
				strip = foundById;
			}
			else if (!string.IsNullOrEmpty(strip.InstanceKey))
			{
				var all = ReelStripDataManager.Instance.ReadOnlyLocalData;
				if (all != null)
				{
					foreach (var kv in all)
					{
						var s = kv.Value; if (s == null) continue;
						if (!string.IsNullOrEmpty(s.InstanceKey) && s.InstanceKey == strip.InstanceKey) { strip = s; break; }
					}
				}
			}
		}

		currentStrip = strip;
		currentSlot = slot;
		MigrateLegacyInventoryKeys();
		if (MenuRoot != null) MenuRoot.gameObject.SetActive(true); else gameObject.SetActive(true);
		Refresh();
		Canvas.ForceUpdateCanvases();
		if (SymbolListRoot != null) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(SymbolListRoot);
	}

	private void OnGlobalReelStripUpdated(object obj)
	{
		var updated = obj as ReelStripData;
		if (updated == null) return;
		if (currentStrip == null) return;
		// If this update pertains to the strip we're currently viewing, refresh to reflect external changes
		if (updated.AccessorId == currentStrip.AccessorId || (!string.IsNullOrEmpty(updated.InstanceKey) && updated.InstanceKey == currentStrip.InstanceKey))
		{
			currentStrip = updated; // adopt the updated reference
			Refresh();
		}
	}

	private bool IsGuid(string s)
	{
		if (string.IsNullOrEmpty(s)) return false;
		return Guid.TryParse(s, out _);
	}

	// Convert any legacy non-GUID definition keys to null (they were old definition asset names)
	private void MigrateLegacyInventoryKeys()
	{
		var pd = GamePlayer.Instance?.PlayerData; if (pd == null) return;
		var syms = pd.GetItemsOfType(InventoryItemType.Symbol); if (syms == null) return;
		for (int i = 0; i < syms.Count; i++)
		{
			var itm = syms[i]; if (itm == null) continue;
			var key = itm.DefinitionKey;
			if (!string.IsNullOrEmpty(key) && !IsGuid(key))
			{
				itm.SetDefinitionKey(null); // legacy clear
			}
		}
	}

	public void Refresh()
	{
		if (SymbolListRoot == null || SymbolDetailsItemPrefab == null) return;
		for (int i = SymbolListRoot.childCount - 1; i >= 0; i--)
		{
			var child = SymbolListRoot.GetChild(i);
			Destroy(child.gameObject);
		}

		var player = GamePlayer.Instance?.PlayerData;
		if (player == null) return;
		var inventorySyms = player.GetItemsOfType(InventoryItemType.Symbol) ?? new List<InventoryItemData>();

		string stripInstanceKey = currentStrip != null ? currentStrip.InstanceKey : null;

		var associatedThis = new List<InventoryItemData>();
		var unassociated = new List<InventoryItemData>();
		var associatedOther = new List<InventoryItemData>();

		for (int i = 0; i < inventorySyms.Count; i++)
		{
			var inv = inventorySyms[i]; if (inv == null) continue;
			var key = inv.DefinitionKey;
			if (string.IsNullOrEmpty(key))
			{
				unassociated.Add(inv);
				continue;
			}
			if (key == stripInstanceKey)
			{
				associatedThis.Add(inv);
			}
			else if (IsGuid(key))
			{
				associatedOther.Add(inv); // GUID but different strip
			}
			else
			{
				// legacy key encountered mid-session: treat as unassociated
				inv.SetDefinitionKey(null);
				unassociated.Add(inv);
			}
		}

		for (int i = 0; i < associatedThis.Count; i++)
		{
			var itm = Instantiate(SymbolDetailsItemPrefab, SymbolListRoot);
			itm.Setup(associatedThis[i], OnAddInventoryItem, OnRemoveInventoryItem, allowAdd: false, allowRemove: true, onTransfer: null, allowTransfer: false, targetStrip: currentStrip);
		}
		for (int i = 0; i < unassociated.Count; i++)
		{
			var itm = Instantiate(SymbolDetailsItemPrefab, SymbolListRoot);
			itm.Setup(unassociated[i], OnAddInventoryItem, OnRemoveInventoryItem, allowAdd: true, allowRemove: false, onTransfer: null, allowTransfer: false, targetStrip: currentStrip);
		}
		for (int i = 0; i < associatedOther.Count; i++)
		{
			var itm = Instantiate(SymbolDetailsItemPrefab, SymbolListRoot);
			// Allow transfer: enable Add when symbol belongs to another strip
			// Previous behavior wired transfer handler into the "add" slot; now provide it as explicit onTransfer and enable transfer button.
			itm.Setup(associatedOther[i], onAdd: null, onRemove: OnRemoveInventoryItem, allowAdd: false, allowRemove: false, onTransfer: OnTransferInventoryItem, allowTransfer: true, targetStrip: currentStrip);
		}
	}

	private void OnCloseClicked()
	{
		if (MenuRoot != null) MenuRoot.gameObject.SetActive(false); else gameObject.SetActive(false);
	}

	private void LogRuntimeSymbols(string prefix)
	{
		if (currentStrip == null) { Debug.Log($"{prefix}: currentStrip is null"); return; }
		var list = currentStrip.RuntimeSymbols;
		if (list == null) { Debug.Log($"{prefix}: runtimeSymbols is null"); return; }

		// Build a detailed view: index:Name(id,key,hasSprite)
		var names = new System.Text.StringBuilder();
		for (int i = 0; i < list.Count; i++)
		{
			var s = list[i];
			if (s == null)
			{
				names.Append("(null)");
			}
			else
			{
				bool hasSprite = s.Sprite != null;
				names.AppendFormat("{0}(id={1},key={2},hasSprite={3})", s.Name ?? "<unnamed>", s.AccessorId, string.IsNullOrEmpty(s.SpriteKey) ? "<none>" : s.SpriteKey, hasSprite);
			}
			if (i + 1 < list.Count) names.Append(", ");
		}
		Debug.Log($"{prefix}: stripAccessorId={currentStrip.AccessorId} runtimeSymbols=[{names}]");
	}

	// Helper: find a global SymbolDefinition by flexible key (uses manager's matching logic)
	private SymbolDefinition FindGlobalSymbolDefinition(string displayName)
	{
		if (string.IsNullOrEmpty(displayName)) return null;
		if (SymbolDefinitionManager.Instance == null) return null;
		return SymbolDefinitionManager.Instance.TryGetDefinition(displayName, out var def) ? def : null;
	}

	private void OnAddInventoryItem(InventoryItemData item)
	{
		if (item == null || currentStrip == null) return;
		var pd = GamePlayer.Instance?.PlayerData; if (pd == null) return;

		// Diagnostic: log incoming inventory item details
		Debug.Log($"OnAddInventoryItem: incoming item DisplayName='{item.DisplayName}' SpriteKey='{item.SpriteKey}' SymbolAccessorId={item.SymbolAccessorId} DefinitionKey='{item.DefinitionKey}'");

		// Capture previous association (if any) so transfer semantics can remove the symbol from its old strip.
		string previousDefinitionKey = item.DefinitionKey;

		LogRuntimeSymbols("Before Add");

		// If this inventory item was previously associated with another strip, pre-clear matching runtime symbols
		// on that strip so transfer will appear as a move rather than duplicate across strips.
		// Only run transfer removal when the item was actually associated (GUID instance key); do not remove
		// matching symbols across strips for unassociated inventory items (same sprite may legitimately exist on multiple strips).
		if (!string.IsNullOrEmpty(previousDefinitionKey) && IsGuid(previousDefinitionKey) && ReelStripDataManager.Instance != null)
		{
			var all = ReelStripDataManager.Instance.ReadOnlyLocalData;
			if (all != null)
			{
				// Copy values to avoid collection-modified exceptions while we remove symbols which may trigger updates
				var strips = new List<ReelStripData>(all.Values);
				foreach (var other in strips)
				{
					if (other == null) continue;
					// Only target the strip referenced by the previousDefinitionKey (avoid aggressive cross-strip removals)
					if (!string.Equals(other.InstanceKey, previousDefinitionKey, StringComparison.OrdinalIgnoreCase)) continue;
					var list = other.RuntimeSymbols;
					if (list == null) continue;
					for (int ri = list.Count - 1; ri >= 0; ri--)
					{
						var rs = list[ri]; if (rs == null) continue;
						bool match = false;
						if (item.SymbolAccessorId > 0 && rs.AccessorId == item.SymbolAccessorId) match = true;
						if (!match && !string.IsNullOrEmpty(item.SpriteKey) && !string.IsNullOrEmpty(rs.SpriteKey) && string.Equals(rs.SpriteKey, item.SpriteKey, StringComparison.OrdinalIgnoreCase)) match = true;
						if (!match && !string.IsNullOrEmpty(item.DisplayName) && string.Equals(rs.Name, item.DisplayName, StringComparison.OrdinalIgnoreCase)) match = true;
						if (match)
						{
							Debug.Log($"OnAddInventoryItem: removing matching symbol from other strip (instanceKey={other.InstanceKey}) as part of transfer.");
							other.RemoveRuntimeSymbolAt(ri);
							ReelStripDataManager.Instance.UpdateRuntimeStrip(other);
						}
					}
				}
			}
		}

		// Prefer an explicit spriteKey stored on the inventory item. If absent, try to match a definition on the current strip.
		Sprite sprite = null;
		string spriteKey = null;
		if (!string.IsNullOrEmpty(item.SpriteKey))
		{
			spriteKey = item.SpriteKey;
			sprite = AssetResolver.ResolveSprite(item.SpriteKey);
		}

		// If we still don't have a sprite, try to match definitions on the current strip (authoring assets)
		SymbolDefinition matchedDef = null;
		if (string.IsNullOrEmpty(spriteKey) && currentStrip != null && currentStrip.SymbolDefinitions != null)
		{
			var defs = currentStrip.SymbolDefinitions;
			for (int i = 0; i < defs.Length; i++)
			{
				var def = defs[i]; if (def == null) continue;
				bool matchesDisplay = false;
				if (!string.IsNullOrEmpty(def.SymbolName) && string.Equals(def.SymbolName, item.DisplayName, StringComparison.OrdinalIgnoreCase)) matchesDisplay = true;
				if (!matchesDisplay && string.Equals(def.name, item.DisplayName, StringComparison.OrdinalIgnoreCase)) matchesDisplay = true;
				if (!matchesDisplay && def.SymbolSprite != null && string.Equals(def.SymbolSprite.name, item.DisplayName, StringComparison.OrdinalIgnoreCase)) matchesDisplay = true;

				if (matchesDisplay)
				{
					matchedDef = def;
					if (def.SymbolSprite != null)
					{
						spriteKey = def.SymbolSprite.name;
						sprite = def.SymbolSprite;
						item.SetSpriteKey(spriteKey);
					}
					break;
				}
			}
		}

		// If still not matched, try global definition search (covers runtime-only flows)
		if (matchedDef == null)
		{
			var global = FindGlobalSymbolDefinition(item.DisplayName);
			if (global != null)
			{
				matchedDef = global;
				if (global.SymbolSprite != null)
				{
					spriteKey = global.SymbolSprite.name;
					sprite = global.SymbolSprite;
					item.SetSpriteKey(spriteKey);
				}
			}
		}

		// Diagnostic: report matched definition and resolved sprite key
		Debug.Log($"OnAddInventoryItem: matchedDef={(matchedDef!=null?matchedDef.name:"<null>")} matchedDef.SymbolName={(matchedDef!=null?matchedDef.SymbolName:"<null>")} resolvedSpriteKey={spriteKey}");

		if (matchedDef == null && string.IsNullOrEmpty(item.SpriteKey) && (item.SymbolAccessorId <= 0))
		{
			throw new InvalidOperationException($"AddRemoveSymbolsMenu: inventory item '{item.DisplayName}' cannot be added because it has no matching SymbolDefinition, no spriteKey, and no SymbolAccessorId. Fix the inventory item or associate it with a definition.");
		}

		// Create a fresh runtime symbol instance for the target strip
		SymbolData newRuntime = null;
		if (matchedDef != null)
		{
			newRuntime = matchedDef.CreateInstance();
			if (newRuntime.Sprite != null) newRuntime.SetSpriteKey(newRuntime.Sprite.name);
		}
		else if (item.SymbolAccessorId > 0 && SymbolDataManager.Instance != null && SymbolDataManager.Instance.TryGetData(item.SymbolAccessorId, out var persisted))
		{
			// Clone persisted into a fresh runtime instance to avoid shared references across strips
			newRuntime = new SymbolData(persisted.Name, persisted.Sprite, persisted.BaseValue, persisted.MinWinDepth, persisted.Weight, persisted.PayScaling, persisted.IsWild, persisted.AllowWildMatch, persisted.WinMode, persisted.TotalCountTrigger, persisted.MaxPerReel, persisted.MatchGroupId);
			newRuntime.EventTriggerScript = persisted.EventTriggerScript;
			if (newRuntime.Sprite != null) newRuntime.SetSpriteKey(newRuntime.Sprite.name);
		}
		else if (!string.IsNullOrEmpty(spriteKey) && sprite != null)
		{
			// Create minimal symbol data from sprite
			newRuntime = new SymbolData(item.DisplayName ?? spriteKey, sprite, 0, -1, 1f, PayScaling.DepthSquared, false, true);
			newRuntime.SetSpriteKey(spriteKey);
		}
		else
		{
			throw new InvalidOperationException($"AddRemoveSymbolsMenu: unable to construct runtime symbol for inventory item '{item.DisplayName}'.");
		}

		// Add runtime and associate inventory
		currentStrip.AddRuntimeSymbol(newRuntime);
		item.SetDefinitionKey(currentStrip.InstanceKey);
		item.SetSymbolAccessorId(newRuntime.AccessorId);

		// Persist and refresh
		ReelStripDataManager.Instance.UpdateRuntimeStrip(currentStrip);
		LogRuntimeSymbols("After Add");
		Refresh();
	}

	private void OnRemoveInventoryItem(InventoryItemData item)
	{
		if (item == null) return;
		// Determine target strip: prefer the strip referenced by the inventory item (GUID) if present,
		// otherwise fall back to the currently-open strip shown in the menu.
		var pd = GamePlayer.Instance?.PlayerData; if (pd == null) return;

		// By default operate on the strip currently shown in the menu. Only when there is no
		// currently-open strip do we attempt to locate a target strip by the inventory item's
		// DefinitionKey. This prevents accidentally removing symbols from other strips when
		// the user is managing a different strip.
		ReelStripData targetStrip = currentStrip;
		if (targetStrip == null && !string.IsNullOrEmpty(item.DefinitionKey) && IsGuid(item.DefinitionKey) && ReelStripDataManager.Instance != null)
		{
			// try to find the strip that matches the inventory item's DefinitionKey
			var all = ReelStripDataManager.Instance.ReadOnlyLocalData;
			if (all != null)
			{
				foreach (var kv in all)
				{
					var s = kv.Value; if (s == null) continue;
					if (!string.IsNullOrEmpty(s.InstanceKey) && string.Equals(s.InstanceKey, item.DefinitionKey, StringComparison.OrdinalIgnoreCase)) { targetStrip = s; break; }
				}
			}
		}

		if (targetStrip == null || ReelStripDataManager.Instance == null) return;

		// If removing from the strip the item was associated with, disassociate it.
		if (!string.IsNullOrEmpty(item.DefinitionKey) && targetStrip.InstanceKey == item.DefinitionKey)
		{
			item.SetDefinitionKey(null);
		}

		LogRuntimeSymbols("Before Remove");

		var list = targetStrip.RuntimeSymbols;
		for (int i = list.Count - 1; i >= 0; i--)
		{
			var rs = list[i];
			if (rs == null) continue;

			bool matched = false;
			if (item.SymbolAccessorId > 0 && rs.AccessorId == item.SymbolAccessorId)
			{
				matched = true;
				Debug.Log($"OnRemoveInventoryItem: matched by SymbolAccessorId (item={item.SymbolAccessorId}) to runtimeSymbol AccessorId={rs.AccessorId}");
			}
			else if (!string.IsNullOrEmpty(item.SpriteKey) && !string.IsNullOrEmpty(rs.SpriteKey) && string.Equals(rs.SpriteKey, item.SpriteKey, StringComparison.OrdinalIgnoreCase))
			{
				matched = true;
				Debug.Log($"OnRemoveInventoryItem: matched by SpriteKey (item={item.SpriteKey}) to runtimeSymbol SpriteKey={rs.SpriteKey}");
			}
			else if (!string.IsNullOrEmpty(item.DisplayName) && string.Equals(rs.Name, item.DisplayName, StringComparison.OrdinalIgnoreCase))
			{
				matched = true;
				Debug.Log($"OnRemoveInventoryItem: matched by Name (item={item.DisplayName}) to runtimeSymbol Name={rs.Name}");
			}

			if (matched)
			{
				targetStrip.RemoveRuntimeSymbolAt(i);
				break;
			}
		}

		ReelStripDataManager.Instance.UpdateRuntimeStrip(targetStrip);

		// If the menu is currently showing the same strip we removed from, refresh the menu; otherwise
		// if we removed from a different strip we should still refresh to reflect inventory changes.
		if (targetStrip == currentStrip)
		{
			LogRuntimeSymbols("After Remove");
			Refresh();
		}
		else
		{
			// If targetStrip differs from currentStrip, still log for diagnostics and refresh the menu
			Debug.Log($"OnRemoveInventoryItem: removed symbol from other strip InstanceKey={targetStrip.InstanceKey}");
			Refresh();
		}
	}

	private void OnTransferInventoryItem(InventoryItemData item)
	{
		// Treat transfer same as add but item currently belongs to another strip
		OnAddInventoryItem(item);
	}
}
