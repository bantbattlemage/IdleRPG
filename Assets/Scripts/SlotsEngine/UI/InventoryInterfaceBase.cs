using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public abstract class InventoryInterfaceBase : MonoBehaviour
{
    public enum InventoryItemFilter
    {
        All = -1,
        Symbol = 0,
        Reel = 1,
        ReelStrip = 2,
        SlotEngine = 3
    }

    public RectTransform InventoryRoot;

    public InventoryInterfaceItem ItemPanelPrefab;
    public Button CloseButton;
    public Button ToggleButton;
    public RectTransform ItemPrefabContentRoot;

    [Header("Filter")]
    public InventoryItemFilter ItemFilter = InventoryItemFilter.All; // inspector dropdown to choose which types to show

    public RectTransform ItemDetailsGroup;
    public TMP_Text ItemDetailsNameText;
    public TMP_Text ItemDetailsDescriptionText;
    public Button ItemDetailsBackButton;

    protected virtual void Start()
    {
        if (CloseButton != null)
            CloseButton.onClick.AddListener(OnCloseButtonClicked);

        if (ToggleButton != null)
        {
            // ensure no duplicate listeners and wire toggle behavior
            ToggleButton.onClick.RemoveAllListeners();
            ToggleButton.onClick.AddListener(OnToggleButtonClicked);
        }

        if (ItemDetailsBackButton != null)
        {
            ItemDetailsBackButton.onClick.RemoveAllListeners();
            ItemDetailsBackButton.onClick.AddListener(OnItemDetailsBack);
        }

        // Ensure details group is hidden initially
        if (ItemDetailsGroup != null)
        {
            ItemDetailsGroup.gameObject.SetActive(false);
        }
    }

    // Removed OnEnable dependency: InventoryInterface may no longer be toggled directly.
    // Call OpenInventory() to show the inventory and refresh its contents.
    public void OpenInventory()
    {
        if (InventoryRoot != null)
        {
            InventoryRoot.gameObject.SetActive(true);
        }
        else
        {
            // fallback for older setups where this component lived on the UI root
            gameObject.SetActive(true);
        }

        Refresh();
    }

    public virtual void Refresh()
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
            // Apply inspector filter: skip items that don't match selected filter
            if (ItemFilter != InventoryItemFilter.All)
            {
                if (it == null) continue;
                var expected = (InventoryItemType)ItemFilter;
                if (it.ItemType != expected) continue;
            }

            // create a local copy so the lambda captures the correct item
            var itemData = it;
            var itemUI = Instantiate(ItemPanelPrefab, ItemPrefabContentRoot);
            itemUI.Setup(itemData, () =>
            {
                // Remove callback
                if (pd != null)
                {
                    var found = pd.Inventory?.FindById(itemData.Id);
                    if (found != null) pd.RemoveInventoryItem(found);
                    Refresh();
                }
            }, () =>
            {
                // Selection callback delegated to derived classes
                OnItemSelected(itemData);
            });
        }
    }

    protected virtual void OnItemSelected(InventoryItemData itemData)
    {
        // Default behavior: show generic item details
        ShowItemDetails(itemData);
    }

    void OnCloseButtonClicked()
    {
        if (InventoryRoot != null)
        {
            InventoryRoot.gameObject.SetActive(false);
        }
        else
        {
            // fallback for older setups
            gameObject.SetActive(false);
        }
    }

    private void OnToggleButtonClicked()
    {
        if (InventoryRoot != null)
        {
            bool newState = !InventoryRoot.gameObject.activeSelf;
            InventoryRoot.gameObject.SetActive(newState);
            if (newState) Refresh();
        }
        else
        {
            bool newState = !gameObject.activeSelf;
            gameObject.SetActive(newState);
            if (newState) Refresh();
        }
    }

    private void OnItemDetailsBack()
    {
        // Hide details and show list
        SetDetailsVisible(false);
    }

    protected void ShowItemDetails(InventoryItemData item)
    {
        if (item == null) return;

        if (ItemDetailsNameText != null) ItemDetailsNameText.text = item.DisplayName ?? "(unnamed)";

        // Build a simple description: include type, id, and definition accessor id if present
        string desc = "";
        desc += $"Type: {item.ItemType}\n";
        if (item.DefinitionAccessorId != 0) desc += $"DefinitionAccessorId: {item.DefinitionAccessorId}\n";
        desc += $"ID: {item.Id}\n";

        if (ItemDetailsDescriptionText != null) ItemDetailsDescriptionText.text = desc;

        SetDetailsVisible(true);
    }

    protected void SetDetailsVisible(bool visible)
    {
        if (ItemDetailsGroup != null)
        {
            ItemDetailsGroup.gameObject.SetActive(visible);
        }

        if (ItemPrefabContentRoot != null)
        {
            ItemPrefabContentRoot.gameObject.SetActive(!visible);
        }
    }
}
