using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryInterfaceItem : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text typeText;
    [SerializeField] private TMP_Text idText;
    [SerializeField] private Button removeButton;
    [SerializeField] private Button selectButton; // optional: click to view details

    private System.Action onRemove;
    private System.Action onSelect;

    public void Setup(InventoryItemData data, System.Action onRemoveCallback, System.Action onSelectCallback = null)
    {
        onRemove = onRemoveCallback;
        onSelect = onSelectCallback;
        if (nameText != null) nameText.text = data?.DisplayName ?? "(unnamed)";
        if (typeText != null) typeText.text = data != null ? data.ItemType.ToString() : string.Empty;
        if (idText != null) idText.text = data?.Id ?? string.Empty;

        if (removeButton != null)
        {
            removeButton.onClick.RemoveAllListeners();
            removeButton.onClick.AddListener(() => onRemove?.Invoke());
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelect?.Invoke());
        }
    }
}
