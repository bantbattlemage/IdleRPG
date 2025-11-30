using UnityEngine;
using System.Collections.Generic;
using System.Text;

public class ItemInventoryInterface : InventoryInterfaceBase
{
    // Reference to symbol management menu for reel strips
    public AddRemoveSymbolsMenu AddRemoveSymbolsMenuInstance;

    // Optional: override Start to call base and keep any future extension point
    private new void Start()
    {
        base.Start();

        // Refresh inventory UI when reel strips are updated elsewhere (symbols added/removed, transfers)
        GlobalEventManager.Instance?.RegisterEvent(SlotsEvent.ReelStripUpdated, OnReelStripUpdated);
    }

    private void OnDestroy()
    {
        GlobalEventManager.Instance?.UnregisterEvent(SlotsEvent.ReelStripUpdated, OnReelStripUpdated);
    }

    public override void Refresh()
    {
        if (ItemPanelPrefab == null || ItemPrefabContentRoot == null) return;

        var pd = GamePlayer.Instance?.PlayerData;
        var items = pd?.Inventory?.Items as IList<InventoryItemData>;

        // Clear existing children
        for (int i = ItemPrefabContentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(ItemPrefabContentRoot.GetChild(i).gameObject);
        }

        if (items == null) return;

        // Precompute unassociated reel/reelstrip accessor ids so we can hide their symbol items
        var unassociatedStripAccessors = new HashSet<int>();
        foreach (var candidate in items)
        {
            if (candidate == null) continue;
            if (candidate.ItemType == InventoryItemType.Reel || candidate.ItemType == InventoryItemType.ReelStrip)
            {
                // apply inspector filter: if user filtered to symbols only, don't consider reels
                if (ItemFilter != InventoryItemFilter.All)
                {
                    var expected = (InventoryItemType)ItemFilter;
                    if (candidate.ItemType != expected) continue;
                }

                // Only consider reel/reelstrip items that are currently NOT associated with any slot/reel
                if (!IsAssociatedWithAnySlot(pd, candidate) && candidate.DefinitionAccessorId != 0)
                {
                    unassociatedStripAccessors.Add(candidate.DefinitionAccessorId);
                }
            }
        }

        foreach (var it in items)
        {
            if (it == null) continue;

            // Apply inspector filter if set
            if (ItemFilter != InventoryItemFilter.All)
            {
                var expected = (InventoryItemType)ItemFilter;
                if (it.ItemType != expected) continue;
            }

            // Only show items that are NOT associated with any current slot or reel
            if (IsAssociatedWithAnySlot(pd, it))
                continue;

            // If this is a Symbol item that belongs to a reel/reelstrip we already plan to show, skip it.
            // This prevents listing per-symbol entries when the parent Reel/ReelStrip inventory item is present.
            if (it.ItemType == InventoryItemType.Symbol && it.DefinitionAccessorId != 0)
            {
                if (unassociatedStripAccessors.Contains(it.DefinitionAccessorId))
                {
                    // skip symbol since its parent reel/strip is present and unassociated
                    continue;
                }
            }

            var itemData = it; // capture for lambda
            var itemUI = Instantiate(ItemPanelPrefab, ItemPrefabContentRoot);
            itemUI.Setup(itemData, () =>
            {
                if (pd != null)
                {
                    var found = pd.Inventory?.FindById(itemData.Id);
                    if (found != null) pd.RemoveInventoryItem(found);
                    Refresh();
                }
            }, () =>
            {
                OnItemSelected(itemData);
            });
        }
    }

    private bool IsAssociatedWithAnySlot(PlayerData pd, InventoryItemData item)
    {
        if (pd == null || pd.CurrentSlots == null || pd.CurrentSlots.Count == 0) return false;

        int defId = item != null ? item.DefinitionAccessorId : 0;
        if (defId == 0) return false; // no explicit association

        // Check each slot and its reels for matching accessor ids
        foreach (var slot in pd.CurrentSlots)
        {
            if (slot == null) continue;

            // Slot association (SlotEngine items)
            if (slot.AccessorId == defId) return true;

            // Check reels in this slot
            var reels = slot.CurrentReelData;
            if (reels == null) continue;
            foreach (var rd in reels)
            {
                if (rd == null) continue;

                // ReelData accessor match
                if (rd.AccessorId == defId) return true;

                // Reel's current strip accessor match
                var strip = rd.CurrentReelStrip;
                if (strip != null && strip.AccessorId == defId) return true;
            }
        }

        return false;
    }

    // Respond to global reel strip updates by refreshing inventory list if visible
    private void OnReelStripUpdated(object obj)
    {
        // Only refresh if our inventory UI is currently visible
        bool visible = InventoryRoot != null ? InventoryRoot.gameObject.activeSelf : gameObject.activeSelf;
        if (!visible) return;
        Refresh();
    }

    protected override void OnItemSelected(InventoryItemData itemData)
    {
        if (itemData == null)
        {
            base.OnItemSelected(itemData);
            return;
        }

        // New behavior: when selecting a ReelStrip inventory item, open the AddRemoveSymbolsMenu with that strip in context (no slot)
        if (itemData.ItemType == InventoryItemType.ReelStrip)
        {
            if (AddRemoveSymbolsMenuInstance == null)
            {
                Debug.LogWarning("ItemInventoryInterface: AddRemoveSymbolsMenuInstance is not assigned.");
                return;
            }

            ReelStripData strip = null;
            int accessor = itemData.DefinitionAccessorId;
            if (accessor != 0 && ReelStripDataManager.Instance != null)
            {
                ReelStripDataManager.Instance.TryGetData(accessor, out strip);
            }

            if (strip == null)
            {
                Debug.LogWarning($"ItemInventoryInterface: Could not resolve ReelStripData for inventory item id={itemData.Id}, accessor={accessor}.");
                return;
            }

            AddRemoveSymbolsMenuInstance.Show(strip, null);
            return;
        }

        // Symbol-specific details
        if (itemData.ItemType == InventoryItemType.Symbol)
        {
            if (ItemDetailsNameText != null) ItemDetailsNameText.text = itemData.DisplayName ?? "(unnamed)";

            var sb = new StringBuilder();
            sb.AppendLine($"Type: {itemData.ItemType}");
            if (!string.IsNullOrEmpty(itemData.SpriteKey)) sb.AppendLine($"SpriteKey: {itemData.SpriteKey}");
            if (itemData.SymbolAccessorId != 0) sb.AppendLine($"SymbolAccessorId: {itemData.SymbolAccessorId}");
            if (itemData.DefinitionAccessorId != 0) sb.AppendLine($"DefinitionAccessorId: {itemData.DefinitionAccessorId}");
            sb.AppendLine($"ID: {itemData.Id}");

            // Try to include persisted symbol details when available
            if (itemData.SymbolAccessorId != 0 && SymbolDataManager.Instance != null)
            {
                if (SymbolDataManager.Instance.TryGetData(itemData.SymbolAccessorId, out var sym))
                {
                    if (sym != null)
                    {
                        sb.AppendLine($"Symbol.Name: {sym.Name}");
                        sb.AppendLine($"Symbol.SpriteKey: {sym.SpriteKey}");
                        sb.AppendLine($"BaseValue: {sym.BaseValue}, MinWinDepth: {sym.MinWinDepth}");
                    }
                }
            }

            if (ItemDetailsDescriptionText != null) ItemDetailsDescriptionText.text = sb.ToString();
            SetDetailsVisible(true);
            return;
        }

        // Fallback to generic behavior
        base.OnItemSelected(itemData);
    }
}
