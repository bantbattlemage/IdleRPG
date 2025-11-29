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
		// Keep the strip reference passed in so we operate directly on the slot-owned instance.
		currentStrip = strip;
		currentSlot = slot;
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
		if (updated.AccessorId == currentStrip.AccessorId)
		{
			Refresh();
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

		int stripAccessor = currentStrip != null ? currentStrip.AccessorId : 0;

		var associatedThis = new List<InventoryItemData>();
		var unassociated = new List<InventoryItemData>();
		var associatedOther = new List<InventoryItemData>();

		for (int i = 0; i < inventorySyms.Count; i++)
		{
			var inv = inventorySyms[i]; if (inv == null) continue;
			var defId = inv.DefinitionAccessorId;
			if (defId == 0)
			{
				unassociated.Add(inv);
				continue;
			}
			if (defId == stripAccessor)
			{
				associatedThis.Add(inv);
			}
			else
			{
				associatedOther.Add(inv);
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
		Debug.Log($"OnAddInventoryItem: incoming item DisplayName='{item.DisplayName}' SpriteKey='{item.SpriteKey}' SymbolAccessorId={item.SymbolAccessorId} DefinitionAccessorId='{item.DefinitionAccessorId}'");

		int previousDefinitionAccessorId = item.DefinitionAccessorId;

		LogRuntimeSymbols("Before Add");

		// If this inventory item was previously associated with another strip, pre-clear matching runtime symbols
		if (previousDefinitionAccessorId != 0 && ReelStripDataManager.Instance != null)
		{
			var all = ReelStripDataManager.Instance.ReadOnlyLocalData;
			if (all != null)
			{
				var strips = new List<ReelStripData>(all.Values);
				foreach (var other in strips)
				{
					if (other == null) continue;
					if (other.AccessorId != previousDefinitionAccessorId) continue;
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
							Debug.Log($"OnAddInventoryItem: removing matching symbol from other strip (accessorId={other.AccessorId}) as part of transfer.");
							other.RemoveRuntimeSymbolAt(ri);
							ReelStripDataManager.Instance.UpdateRuntimeStrip(other);
						}
					}
				}
			}
		}

		Sprite sprite = null;
		string spriteKey = null;
		if (!string.IsNullOrEmpty(item.SpriteKey))
		{
			spriteKey = item.SpriteKey;
			sprite = AssetResolver.ResolveSprite(item.SpriteKey);
		}

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

		Debug.Log($"OnAddInventoryItem: matchedDef={(matchedDef!=null?matchedDef.name:"<null>")} matchedDef.SymbolName={(matchedDef!=null?matchedDef.SymbolName:"<null>")} resolvedSpriteKey={spriteKey}");

		if (matchedDef == null && string.IsNullOrEmpty(item.SpriteKey) && (item.SymbolAccessorId <= 0))
		{
			throw new InvalidOperationException($"AddRemoveSymbolsMenu: inventory item '{item.DisplayName}' cannot be added because it has no matching SymbolDefinition, no spriteKey, and no SymbolAccessorId. Fix the inventory item or associate it with a definition.");
		}

		SymbolData newRuntime = null;
		if (matchedDef != null)
		{
			newRuntime = matchedDef.CreateInstance();
			if (newRuntime.Sprite != null) newRuntime.SetSpriteKey(newRuntime.Sprite.name);
		}
		else if (item.SymbolAccessorId > 0 && SymbolDataManager.Instance != null && SymbolDataManager.Instance.TryGetData(item.SymbolAccessorId, out var persisted))
		{
			newRuntime = new SymbolData(persisted.Name, persisted.Sprite, persisted.BaseValue, persisted.MinWinDepth, persisted.Weight, persisted.PayScaling, persisted.IsWild, persisted.AllowWildMatch, persisted.WinMode, persisted.TotalCountTrigger, persisted.MaxPerReel, persisted.MatchGroupId);
			newRuntime.EventTriggerScript = persisted.EventTriggerScript;
			if (newRuntime.Sprite != null) newRuntime.SetSpriteKey(newRuntime.Sprite.name);
		}
		else if (!string.IsNullOrEmpty(spriteKey) && sprite != null)
		{
			newRuntime = new SymbolData(item.DisplayName ?? spriteKey, sprite, 0, -1, 1f, PayScaling.DepthSquared, false, true);
			newRuntime.SetSpriteKey(spriteKey);
		}
		else
		{
			throw new InvalidOperationException($"AddRemoveSymbolsMenu: unable to construct runtime symbol for inventory item '{item.DisplayName}'.")
;
		}

		currentStrip.AddRuntimeSymbol(newRuntime);
		item.SetDefinitionAccessorId(currentStrip.AccessorId);
		item.SetSymbolAccessorId(newRuntime.AccessorId);

		ReelStripDataManager.Instance.UpdateRuntimeStrip(currentStrip);
		LogRuntimeSymbols("After Add");
		Refresh();
	}

	private void OnRemoveInventoryItem(InventoryItemData item)
	{
		if (item == null) return;
		var pd = GamePlayer.Instance?.PlayerData; if (pd == null) return;

		ReelStripData targetStrip = currentStrip;
		if (targetStrip == null && item.DefinitionAccessorId != 0 && ReelStripDataManager.Instance != null)
		{
			if (ReelStripDataManager.Instance.TryGetData(item.DefinitionAccessorId, out var found)) targetStrip = found;
		}

		if (targetStrip == null || ReelStripDataManager.Instance == null) return;

		if (item.DefinitionAccessorId != 0 && targetStrip.AccessorId == item.DefinitionAccessorId)
		{
			item.SetDefinitionAccessorId(0);
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

		if (targetStrip == currentStrip)
		{
			LogRuntimeSymbols("After Remove");
			Refresh();
		}
		else
		{
			Debug.Log($"OnRemoveInventoryItem: removed symbol from other strip AccessorId={targetStrip.AccessorId}");
			Refresh();
		}
	}

	private void OnTransferInventoryItem(InventoryItemData item)
	{
		OnAddInventoryItem(item);
	}
}
