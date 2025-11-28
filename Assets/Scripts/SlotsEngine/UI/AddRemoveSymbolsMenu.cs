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
	public Button AddSymbolButton;

	private ReelStripData currentStrip;
	private SlotsData currentSlot;

	private void Start()
	{
		if (CloseButton != null) CloseButton.onClick.AddListener(OnCloseClicked);
		if (AddSymbolButton != null) AddSymbolButton.onClick.AddListener(OnQuickAddTestSymbol);

		if (SymbolListRoot != null)
		{
			for (int i = SymbolListRoot.childCount - 1; i >= 0; i--)
			{
				var child = SymbolListRoot.GetChild(i);
				if (AddSymbolButton != null && (child == AddSymbolButton.transform || AddSymbolButton.transform.IsChildOf(child))) continue;
				Destroy(child.gameObject);
			}
			if (AddSymbolButton != null && AddSymbolButton.transform.parent == SymbolListRoot)
				AddSymbolButton.transform.SetAsLastSibling();
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
			if (AddSymbolButton != null && (child == AddSymbolButton.transform || AddSymbolButton.transform.IsChildOf(child))) continue;
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
			itm.Setup(associatedThis[i], OnAddInventoryItem, OnRemoveInventoryItem, allowAdd: false, allowRemove: true);
		}
		for (int i = 0; i < unassociated.Count; i++)
		{
			var itm = Instantiate(SymbolDetailsItemPrefab, SymbolListRoot);
			itm.Setup(unassociated[i], OnAddInventoryItem, OnRemoveInventoryItem, allowAdd: true, allowRemove: false);
		}
		for (int i = 0; i < associatedOther.Count; i++)
		{
			var itm = Instantiate(SymbolDetailsItemPrefab, SymbolListRoot);
			// Allow transfer: enable Add when symbol belongs to another strip
			itm.Setup(associatedOther[i], OnTransferInventoryItem, OnRemoveInventoryItem, allowAdd: true, allowRemove: false);
		}

		if (AddSymbolButton != null && AddSymbolButton.transform.parent == SymbolListRoot)
			AddSymbolButton.transform.SetAsLastSibling();
	}

	private void OnCloseClicked()
	{
		if (MenuRoot != null) MenuRoot.gameObject.SetActive(false); else gameObject.SetActive(false);
	}

	private void OnQuickAddTestSymbol()
	{
		var pd = GamePlayer.Instance?.PlayerData; if (pd == null) return;
		string newName = "Symbol" + (pd.Inventory?.Items.Count + 1);
		// Prefer to pick a sprite from the centralized SymbolDefinitionManager when available
		Sprite chosen = null;
		if (SymbolDefinitionManager.Instance != null)
		{
			var defs = SymbolDefinitionManager.Instance.GetAllDefinitions();
			if (defs != null && defs.Count > 0)
			{
				for (int i = 0; i < defs.Count; i++)
				{
					var d = defs[i]; if (d == null) continue;
					if (d.SymbolSprite != null) { chosen = d.SymbolSprite; break; }
				}
			}
		}

		if (chosen != null)
		{
			pd.AddInventoryItem(new InventoryItemData(newName, InventoryItemType.Symbol, null, chosen.name));
		}
		else
		{
			pd.AddInventoryItem(new InventoryItemData(newName, InventoryItemType.Symbol, null));
		}
		Refresh();
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
		if (!string.IsNullOrEmpty(previousDefinitionKey) && previousDefinitionKey != currentStrip.InstanceKey && ReelStripDataManager.Instance != null)
		{
			var all = ReelStripDataManager.Instance.ReadOnlyLocalData;
			if (all != null)
			{
				// Copy values to avoid collection-modified exceptions while we remove symbols which may trigger updates
				var strips = new List<ReelStripData>(all.Values);
				foreach (var other in strips)
				{
					if (other == null) continue;
					if (other.InstanceKey != previousDefinitionKey) continue; // target previous strip only
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
							Debug.Log($"OnAddInventoryItem: removing matching symbol from previous strip (instanceKey={other.InstanceKey}) as part of transfer.");
							other.RemoveRuntimeSymbolAt(ri);
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
				// match against authoring display name, definition asset name, or sprite asset name (case-insensitive)
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

		// Do NOT attempt to resolve by display name or use fuzzy dynamic loading here. Names are not valid fallbacks.
		// Enforce: if there is no definition match and the inventory item does not carry an explicit sprite key
		// or a SymbolAccessorId referencing an existing SymbolData, fail immediately so data can be corrected.
		if (matchedDef == null && string.IsNullOrEmpty(item.SpriteKey) && (item.SymbolAccessorId <= 0))
		{
			throw new InvalidOperationException($"AddRemoveSymbolsMenu: inventory item '{item.DisplayName}' cannot be added because it has no matching SymbolDefinition, no spriteKey, and no SymbolAccessorId. Fix the inventory item or associate it with a definition.");
		}

		// At this point either matchedDef != null, or an explicit spriteKey (or SymbolAccessorId) exists.

		// Ensure runtime SymbolData always carries a spriteKey when a definition match exists
		bool alreadyPresent = false;
		foreach (var rs in currentStrip.RuntimeSymbols)
		{
			// Treat equality by name OR sprite key OR accessor id
			if (rs == null) continue;
			if (!string.IsNullOrEmpty(spriteKey) && !string.IsNullOrEmpty(rs.SpriteKey) && string.Equals(rs.SpriteKey, spriteKey, StringComparison.OrdinalIgnoreCase)) { alreadyPresent = true; break; }
			if (rs.AccessorId > 0 && item.SymbolAccessorId > 0 && rs.AccessorId == item.SymbolAccessorId) { alreadyPresent = true; break; }
			if (!string.IsNullOrEmpty(rs.Name) && !string.IsNullOrEmpty(item.DisplayName) && string.Equals(rs.Name, item.DisplayName, StringComparison.OrdinalIgnoreCase)) { alreadyPresent = true; break; }
		}
		if (!alreadyPresent)
		{
			// Attempt to reuse an existing SymbolData instance when possible rather than creating a new one.
			SymbolData existingSymbol = null;
			// First preference: inventory item may carry a direct reference (accessor id) to a SymbolData registered with the manager
			if (item != null && item.SymbolAccessorId > 0 && SymbolDataManager.Instance != null)
			{
				if (SymbolDataManager.Instance.TryGetData(item.SymbolAccessorId, out var invSym))
				{
					existingSymbol = invSym;
					Debug.Log($"OnAddInventoryItem: found existingSymbol from inventory accessor: Name={invSym.Name} SpriteKey={invSym.SpriteKey} AccessorId={invSym.AccessorId}");
				}
			}

			// Prefer symbols already on this strip (same key or name)
			if (existingSymbol == null && currentStrip != null && currentStrip.RuntimeSymbols != null)
			{
				for (int i = 0; i < currentStrip.RuntimeSymbols.Count; i++)
				{
					var s = currentStrip.RuntimeSymbols[i];
					if (s == null) continue;
					if (!string.IsNullOrEmpty(spriteKey) && !string.IsNullOrEmpty(s.SpriteKey) && string.Equals(s.SpriteKey, spriteKey, StringComparison.OrdinalIgnoreCase)) { existingSymbol = s; Debug.Log($"OnAddInventoryItem: matched existingSymbol on strip by SpriteKey: Name={s.Name} SpriteKey={s.SpriteKey} AccessorId={s.AccessorId}"); break; }
					if (string.Equals(s.Name, item.DisplayName, StringComparison.OrdinalIgnoreCase)) { existingSymbol = s; Debug.Log($"OnAddInventoryItem: matched existingSymbol on strip by Name: Name={s.Name} SpriteKey={s.SpriteKey} AccessorId={s.AccessorId}"); break; }
				}
			}

			// Next, search the global SymbolDataManager for a matching persisted symbol by spriteKey or name
			if (existingSymbol == null && SymbolDataManager.Instance != null)
			{
				var all = SymbolDataManager.Instance.GetAllData();
				for (int i = 0; i < all.Count; i++)
				{
					var s = all[i]; if (s == null) continue;
					if (!string.IsNullOrEmpty(spriteKey) && !string.IsNullOrEmpty(s.SpriteKey) && string.Equals(s.SpriteKey, spriteKey, StringComparison.OrdinalIgnoreCase)) { existingSymbol = s; Debug.Log($"OnAddInventoryItem: matched existingSymbol in global manager by SpriteKey: Name={s.Name} SpriteKey={s.SpriteKey} AccessorId={s.AccessorId}"); break; }
					if (string.Equals(s.Name, item.DisplayName, StringComparison.OrdinalIgnoreCase)) { existingSymbol = s; Debug.Log($"OnAddInventoryItem: matched existingSymbol in global manager by Name: Name={s.Name} SpriteKey={s.SpriteKey} AccessorId={s.AccessorId}"); break; }
				}
			}

			// If we matched a definition (authoring asset) try to find its runtime instance by comparing definition sprite/name
			if (existingSymbol == null && matchedDef != null)
			{
				if (!string.IsNullOrEmpty(matchedDef.SymbolName))
				{
					// look on strip first
					if (currentStrip != null && currentStrip.RuntimeSymbols != null)
					{
						for (int i = 0; i < currentStrip.RuntimeSymbols.Count; i++) { var s = currentStrip.RuntimeSymbols[i]; if (s == null) continue; if (string.Equals(s.Name, matchedDef.SymbolName, StringComparison.OrdinalIgnoreCase)) { existingSymbol = s; Debug.Log($"OnAddInventoryItem: matched existingSymbol on strip by matchedDef.SymbolName: Name={s.Name} SpriteKey={s.SpriteKey} AccessorId={s.AccessorId}"); break; } }
					}
				}
				if (existingSymbol == null && matchedDef.SymbolSprite != null)
				{
					string defSpriteKey = matchedDef.SymbolSprite.name;
					if (SymbolDataManager.Instance != null)
					{
						var all = SymbolDataManager.Instance.GetAllData();
						for (int i = 0; i < all.Count; i++) { var s = all[i]; if (s == null) continue; if (!string.IsNullOrEmpty(s.SpriteKey) && string.Equals(s.SpriteKey, defSpriteKey, StringComparison.OrdinalIgnoreCase)) { existingSymbol = s; Debug.Log($"OnAddInventoryItem: matched existingSymbol in global manager by matchedDef.SpriteKey: Name={s.Name} SpriteKey={s.SpriteKey} AccessorId={s.AccessorId}"); break; } }
					}
				}
			}

			// When a definition match exists and the inventory item doesn't explicitly reference a persisted accessor,
			// prefer creating a fresh runtime instance from the definition instead of reusing a possibly unrelated persisted instance.
			if (matchedDef != null && existingSymbol != null)
			{
				// If the persisted symbol's spriteKey doesn't match the matched definition's sprite, ignore the persisted accessor
				// and create a fresh runtime instance from the definition so name/sprite align with the definition.
				string defSpriteKey = matchedDef.SymbolSprite != null ? matchedDef.SymbolSprite.name : null;
				if (string.IsNullOrEmpty(defSpriteKey) || !string.Equals(existingSymbol.SpriteKey, defSpriteKey, StringComparison.OrdinalIgnoreCase))
				{
					Debug.Log($"OnAddInventoryItem: persisted SymbolData (AccessorId={existingSymbol.AccessorId}, SpriteKey={existingSymbol.SpriteKey}) does not match matched definition sprite '{defSpriteKey}'. Ignoring persisted accessor and creating from definition.");
					existingSymbol = null;
				}
				else
				{
					Debug.Log($"OnAddInventoryItem: persisted SymbolData matches matched definition sprite; reusing persisted accessor (AccessorId={existingSymbol.AccessorId}).");
				}
			}

			if (existingSymbol != null)
			{
				// If the symbol instance is already present on this strip, nothing to do.
				if (currentStrip != null && currentStrip.RuntimeSymbols != null)
				{
					bool present = false;
					for (int pi = 0; pi < currentStrip.RuntimeSymbols.Count; pi++)
					{
						if (object.ReferenceEquals(currentStrip.RuntimeSymbols[pi], existingSymbol)) { present = true; break; }
					}
					if (present)
					{
						Debug.Log($"[Diagnostics] Existing runtimeSymbol already present on strip: Name={existingSymbol.Name} AccessorId={existingSymbol.AccessorId}");
						return;
					}
				}

				// Before reusing the instance on this strip, ensure it's not still attached to another strip (transfer semantics)
				if (existingSymbol != null && ReelStripDataManager.Instance != null)
				{
					var all = ReelStripDataManager.Instance.ReadOnlyLocalData;
					if (all != null)
					{
						foreach (var kv in all)
						{
							var other = kv.Value; if (other == null) continue;
							if (object.ReferenceEquals(other, currentStrip)) continue;
							// If other strip contains this runtime symbol, remove it so the symbol is moved rather than duplicated in UI
							var list = other.RuntimeSymbols;
							if (list != null)
							{
								for (int ri = list.Count - 1; ri >= 0; ri--)
								{
									var rs = list[ri]; if (rs == null) continue;
									if (object.ReferenceEquals(rs, existingSymbol))
									{
										Debug.Log($"OnAddInventoryItem: removing existingSymbol from other strip (instanceKey={other.InstanceKey}) to perform transfer.");
										other.RemoveRuntimeSymbolAt(ri);
										break;
									}
								}
							}
						}
					}
				}

				// Reuse existing instance on this strip
				Debug.Log($"[Diagnostics] Reusing existing runtimeSymbol: Name={existingSymbol.Name} SpriteKey={existingSymbol.SpriteKey} HasSprite={existingSymbol.Sprite != null} AccessorId={existingSymbol.AccessorId}");
				currentStrip.AddRuntimeSymbol(existingSymbol);
				Debug.Log($"[Diagnostics] After AddRuntimeSymbol (reuse): Name={existingSymbol.Name} SpriteKey={existingSymbol.SpriteKey} HasSprite={existingSymbol.Sprite != null} AccessorId={existingSymbol.AccessorId}");
			}
			else
			{
				// No existing symbol: create new only via definition; ad-hoc creation is disallowed here.
				if (matchedDef != null)
				{
					var runtimeSymbol = matchedDef.CreateInstance();
					if (runtimeSymbol.Sprite != null)
					{
						runtimeSymbol.SetSpriteKey(runtimeSymbol.Sprite.name);
					}

					Debug.Log($"[Diagnostics] Creating runtimeSymbol from definition: Name={runtimeSymbol.Name} SpriteKey={runtimeSymbol.SpriteKey} HasSprite={runtimeSymbol.Sprite != null}");

					currentStrip.AddRuntimeSymbol(runtimeSymbol);

					Debug.Log($"[Diagnostics] After AddRuntimeSymbol (definition): Name={runtimeSymbol.Name} SpriteKey={runtimeSymbol.SpriteKey} HasSprite={runtimeSymbol.Sprite != null} AccessorId={runtimeSymbol.AccessorId}");
				}
				else
				{
					// No definition and no existing SymbolData — fail loudly so data can be corrected/migrated.
					throw new InvalidOperationException($"AddRemoveSymbolsMenu: cannot add inventory item '{item.DisplayName}' to strip — no matching SymbolDefinition and no existing SymbolData found. Inventory items must reference a definition or a SymbolData accessor.");
				}
			}

			// After successful add, associate inventory item to new strip
			item.SetDefinitionKey(currentStrip.InstanceKey);
		}

		ReelStripDataManager.Instance.UpdateRuntimeStrip(currentStrip);
		LogRuntimeSymbols("After Add");
		Refresh();
	}

	private void OnRemoveInventoryItem(InventoryItemData item)
	{
		if (item == null || currentStrip == null) return;
		var pd = GamePlayer.Instance?.PlayerData; if (pd == null) return;
		if (item.DefinitionKey == currentStrip.InstanceKey)
		{
			item.SetDefinitionKey(null); // disassociate only
		}

		LogRuntimeSymbols("Before Remove");

		var list = currentStrip.RuntimeSymbols;
		for (int i = list.Count - 1; i >= 0; i--)
		{
			var rs = list[i];
			if (rs == null) continue;

			// Match by strongest identifiers first: persisted accessor id, then spriteKey, then fallback to name equality
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
				currentStrip.RemoveRuntimeSymbolAt(i);
				break;
			}
		}
		ReelStripDataManager.Instance.UpdateRuntimeStrip(currentStrip);
		LogRuntimeSymbols("After Remove");
		Refresh();
	}

	private void OnTransferInventoryItem(InventoryItemData item)
	{
		// Treat transfer same as add but item currently belongs to another strip
		OnAddInventoryItem(item);
	}
}
