using UnityEngine;
using System.Collections.Generic;
using System.Text;

public class ItemInventoryInterface : InventoryInterfaceBase
{
    // Optional: override Start to call base and keep any future extension point
    private new void Start()
    {
        base.Start();
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

        foreach (var it in items)
        {
            if (it == null) continue;

            // Always skip SlotEngine and Reel items for this interface (we only show non-slot, non-reel items)
            if (it.ItemType == InventoryItemType.SlotEngine || it.ItemType == InventoryItemType.Reel)
                continue;

            // Apply inspector filter if set
            if (ItemFilter != InventoryItemFilter.All)
            {
                var expected = (InventoryItemType)ItemFilter;
                if (it.ItemType != expected) continue;
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

    protected override void OnItemSelected(InventoryItemData itemData)
    {
        if (itemData == null)
        {
            base.OnItemSelected(itemData);
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
