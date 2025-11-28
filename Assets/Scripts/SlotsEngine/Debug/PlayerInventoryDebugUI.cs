using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Runtime debug UI for visualizing and managing the player's inventory.
/// Attach this to any GameObject in a scene (or let it auto-create a Canvas).
/// Toggle with the 'I' key.
/// </summary>
public class PlayerInventoryDebugUI : MonoBehaviour
{
    private GameObject rootCanvas;
    private RectTransform panel;
    private RectTransform listContent;
    private TMP_Dropdown filterDropdown;

    private bool visible = false;

    private void Awake()
    {
        TryCreateEventSystem();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            visible = !visible;
            if (visible) EnsureUI();
            if (panel != null) panel.gameObject.SetActive(visible);
        }
    }

    private void EnsureUI()
    {
        if (panel != null) return;
        CreateCanvasIfNeeded();
        BuildUI();
        RefreshList();
    }

    private void CreateCanvasIfNeeded()
    {
        if (rootCanvas != null) return;

        // Prefer an explicit overlay canvas if present in the scene (authoring-friendly)
        var overlay = GameObject.Find("OverlayCanvas");
        if (overlay != null)
        {
            // Use the found object if it has a Canvas, otherwise use it as parent container anyway
            rootCanvas = overlay;
            if (rootCanvas.GetComponent<Canvas>() == null)
            {
                // Add a Canvas component so UI will render correctly when we parent under this object
                var c = rootCanvas.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                rootCanvas.AddComponent<CanvasScaler>();
                rootCanvas.AddComponent<GraphicRaycaster>();
            }
            return;
        }

        // Try to find an existing Canvas using the newer API to avoid deprecation warnings
        Canvas existing = null;
#if UNITY_2023_1_OR_NEWER
        existing = UnityEngine.Object.FindFirstObjectByType<Canvas>();
#else
        existing = UnityEngine.Object.FindObjectOfType<Canvas>();
#endif
        if (existing != null)
        {
            rootCanvas = existing.gameObject;
            return;
        }

        rootCanvas = new GameObject("RuntimeDebugCanvas");
        var newCanvas = rootCanvas.AddComponent<Canvas>();
        newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.AddComponent<CanvasScaler>();
        rootCanvas.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(rootCanvas);
    }

    private void BuildUI()
    {
        // Panel
        var panelGo = new GameObject("InventoryDebugPanel");
        panelGo.transform.SetParent(rootCanvas.transform, false);
        panel = panelGo.AddComponent<RectTransform>();
        panel.sizeDelta = new Vector2(420, 480);
        panel.anchorMin = new Vector2(0.02f, 0.02f);
        panel.anchorMax = new Vector2(0.02f, 0.02f);
        panel.pivot = new Vector2(0, 0);
        var image = panelGo.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.75f);

        // Title
        var title = CreateText("Inventory Debug", 18, TextAlignmentOptions.Left);
        title.transform.SetParent(panel, false);
        var tr = title.GetComponent<RectTransform>(); tr.anchoredPosition = new Vector2(10, -10);

        // Buttons row
        var btnRow = new GameObject("ButtonsRow");
        btnRow.transform.SetParent(panel, false);
        var brt = btnRow.AddComponent<RectTransform>(); brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(0, 1); brt.anchoredPosition = new Vector2(10, -40); brt.sizeDelta = new Vector2(-20, 28);

        // Add button
        var addBtn = CreateButton("AddRandom", "Add Random");
        addBtn.transform.SetParent(btnRow.transform, false);
        var addRt = addBtn.GetComponent<RectTransform>(); addRt.anchorMin = new Vector2(0, 0); addRt.anchorMax = new Vector2(0, 1); addRt.pivot = new Vector2(0, 0.5f); addRt.anchoredPosition = new Vector2(0, 0); addRt.sizeDelta = new Vector2(110, 28);
        addBtn.onClick.AddListener(OnAddRandom);

        // Refresh button
        var refreshBtn = CreateButton("Refresh", "Refresh");
        refreshBtn.transform.SetParent(btnRow.transform, false);
        var refRt = refreshBtn.GetComponent<RectTransform>(); refRt.anchorMin = new Vector2(0, 0); refRt.anchorMax = new Vector2(0, 1); refRt.pivot = new Vector2(0, 0.5f); refRt.anchoredPosition = new Vector2(120, 0); refRt.sizeDelta = new Vector2(80, 28);
        refreshBtn.onClick.AddListener(RefreshList);

        // Filter dropdown
        var ddGo = new GameObject("FilterDropdown");
        ddGo.transform.SetParent(btnRow.transform, false);
        filterDropdown = ddGo.AddComponent<TMP_Dropdown>();
        var ddRt = ddGo.GetComponent<RectTransform>(); ddRt.anchorMin = new Vector2(0, 0); ddRt.anchorMax = new Vector2(0, 1); ddRt.pivot = new Vector2(0, 0.5f); ddRt.anchoredPosition = new Vector2(210, 0); ddRt.sizeDelta = new Vector2(110, 28);
        // add placeholder components for visual
        var ddImg = ddGo.AddComponent<Image>(); ddImg.color = Color.white * 0.9f;
        var caption = CreateText("All", 14, TextAlignmentOptions.Center);
        caption.transform.SetParent(ddGo.transform, false);
        caption.color = Color.black; // ensure caption is readable on the light dropdown background
        filterDropdown.captionText = caption;

        // Create a minimal template required by TMP_Dropdown: a root 'Template' (inactive) with a child 'Item' that has a Toggle
        var templateGo = new GameObject("Template");
        templateGo.transform.SetParent(ddGo.transform, false);
        var templateRt = templateGo.AddComponent<RectTransform>();
        templateRt.anchorMin = new Vector2(0, 0); templateRt.anchorMax = new Vector2(1, 1); templateRt.pivot = new Vector2(0.5f, 0.5f);
        // add background image for template (optional)
        var templateBg = templateGo.AddComponent<Image>(); templateBg.color = new Color(1f, 1f, 1f, 0.95f);

        // Item (child) - required to have a Toggle component
        var itemGo = new GameObject("Item");
        itemGo.transform.SetParent(templateGo.transform, false);
        var itemRt = itemGo.AddComponent<RectTransform>(); itemRt.anchorMin = new Vector2(0, 0.5f); itemRt.anchorMax = new Vector2(1, 0.5f); itemRt.pivot = new Vector2(0.5f, 0.5f); itemRt.sizeDelta = new Vector2(0, 20);
        var itemToggle = itemGo.AddComponent<Toggle>();
        // add a background graphic for toggle
        var itemBg = itemGo.AddComponent<Image>(); itemBg.color = Color.white * 0.9f; itemToggle.targetGraphic = itemBg;
        // label for item
        var itemLabel = CreateText("Option", 14, TextAlignmentOptions.Left);
        itemLabel.color = Color.black; // item label should be black against the template's white background
        itemLabel.transform.SetParent(itemGo.transform, false);
        var itemLabelRt = itemLabel.GetComponent<RectTransform>(); itemLabelRt.anchorMin = new Vector2(0, 0); itemLabelRt.anchorMax = new Vector2(1, 1); itemLabelRt.offsetMin = new Vector2(8, 0); itemLabelRt.offsetMax = new Vector2(-8, 0);

        // Make template inactive per TMP expectations
        templateGo.SetActive(false);

        // assign template and item text to dropdown so SetupTemplate can find required elements
        filterDropdown.template = templateRt;
        filterDropdown.itemText = itemLabel;

        filterDropdown.options = new List<TMP_Dropdown.OptionData>() { new TMP_Dropdown.OptionData("All") };
        filterDropdown.options.AddRange(Enum.GetNames(typeof(InventoryItemType)).Select(n => new TMP_Dropdown.OptionData(n)));
        filterDropdown.onValueChanged.AddListener(idx => RefreshList());

        // Scroll area for items
        var scrollGo = new GameObject("ItemScroll"); scrollGo.transform.SetParent(panel, false);
        var srt = scrollGo.AddComponent<RectTransform>(); srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 1); srt.pivot = new Vector2(0, 0); srt.anchoredPosition = new Vector2(10, 10); srt.sizeDelta = new Vector2(-20, -80);
        var sr = scrollGo.AddComponent<ScrollRect>();
        var svImage = scrollGo.AddComponent<Image>(); svImage.color = new Color(1,1,1,0.02f);

        var viewport = new GameObject("Viewport"); viewport.transform.SetParent(scrollGo.transform, false);
        var vRt = viewport.AddComponent<RectTransform>(); vRt.anchorMin = new Vector2(0, 0); vRt.anchorMax = new Vector2(1, 1); vRt.pivot = new Vector2(0.5f, 0.5f); vRt.anchoredPosition = Vector2.zero; vRt.sizeDelta = Vector2.zero;
        var mask = viewport.AddComponent<Mask>(); mask.showMaskGraphic = false; var vImg = viewport.AddComponent<Image>(); vImg.color = Color.white * 0.02f;

        var content = new GameObject("Content"); content.transform.SetParent(viewport.transform, false);
        listContent = content.AddComponent<RectTransform>(); listContent.anchorMin = new Vector2(0, 1); listContent.anchorMax = new Vector2(1, 1); listContent.pivot = new Vector2(0.5f, 1); listContent.anchoredPosition = Vector2.zero; listContent.sizeDelta = new Vector2(0, 0);
        var vlg = content.AddComponent<VerticalLayoutGroup>(); vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = true; vlg.spacing = 6; vlg.padding = new RectOffset(6,6,6,6);
        var ct = content.AddComponent<ContentSizeFitter>(); ct.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.content = listContent; sr.viewport = vRt; sr.horizontal = false; sr.vertical = true;

        panelGo.SetActive(visible);
    }

    private TextMeshProUGUI CreateText(string text, float size, TextAlignmentOptions alignment)
    {
        var go = new GameObject("Text");
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = alignment;
        t.color = Color.white;
        // Let TMP use its default font asset; if none present the user will see a warning in editor
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(100, 20);
        return t;
    }

    private Button CreateButton(string name, string label)
    {
        var go = new GameObject(name);
        var img = go.AddComponent<Image>(); img.color = new Color(1f,1f,1f,0.9f);
        var btn = go.AddComponent<Button>();
        var txt = CreateText(label, 14, TextAlignmentOptions.Center);
        txt.color = Color.black; // ensure button labels are readable against the light button background
        txt.transform.SetParent(go.transform, false);
        return btn;
    }

    private void RefreshList()
    {
        if (listContent == null) return;
        // clear existing children
        for (int i = listContent.childCount - 1; i >= 0; i--) Destroy(listContent.GetChild(i).gameObject);

        var pd = GamePlayer.Instance?.PlayerData;
        if (pd == null) return;

        var items = pd.Inventory?.Items ?? new List<InventoryItemData>();
        // filter
        int fidx = filterDropdown != null ? filterDropdown.value : 0;
        string selected = fidx >= 0 && filterDropdown.options.Count > fidx ? filterDropdown.options[fidx].text : "All";
        List<InventoryItemData> toShow;
        if (selected == "All") toShow = items.ToList(); else
        {
            if (Enum.TryParse<InventoryItemType>(selected, out var t)) toShow = items.Where(x => x.ItemType == t).ToList(); else toShow = items.ToList();
        }

        foreach (var it in toShow)
        {
            var row = new GameObject("ItemRow"); row.transform.SetParent(listContent, false);
            var rt = row.AddComponent<RectTransform>(); rt.sizeDelta = new Vector2(0, 28);
            var hor = row.AddComponent<HorizontalLayoutGroup>(); hor.childForceExpandHeight = true; hor.childForceExpandWidth = false; hor.padding = new RectOffset(4,4,4,4); hor.spacing = 6;

            var nameTxt = CreateText(it.DisplayName ?? "(unnamed)", 14, TextAlignmentOptions.Left);
            nameTxt.transform.SetParent(row.transform, false);
            var nmRt = nameTxt.GetComponent<RectTransform>(); nmRt.sizeDelta = new Vector2(180, 24);

            var typeTxt = CreateText(it.ItemType.ToString(), 12, TextAlignmentOptions.Left);
            typeTxt.transform.SetParent(row.transform, false);
            var tyRt = typeTxt.GetComponent<RectTransform>(); tyRt.sizeDelta = new Vector2(90, 24);

            var idTxt = CreateText(it.Id, 11, TextAlignmentOptions.Left);
            idTxt.transform.SetParent(row.transform, false);
            var idRt = idTxt.GetComponent<RectTransform>(); idRt.sizeDelta = new Vector2(90, 24);

            var remBtn = CreateButton("Remove", "Remove");
            remBtn.transform.SetParent(row.transform, false);
            var rb = remBtn; rb.onClick.AddListener(() => OnRemoveItem(it.Id));
            var rbRt = remBtn.GetComponent<RectTransform>(); rbRt.sizeDelta = new Vector2(70, 24);
        }
    }

    private void OnAddRandom()
    {
        var pd = GamePlayer.Instance?.PlayerData;
        if (pd == null) return;
        var types = Enum.GetValues(typeof(InventoryItemType));
        var t = (InventoryItemType)types.GetValue(UnityEngine.Random.Range(0, types.Length));
        var name = "Item " + UnityEngine.Random.Range(1000, 9999);

        // Try to find a real sprite key from available reel strips or runtime symbols so the new inventory item stores an explicit spriteKey
        string spriteKey = FindAnyAvailableSymbolSpriteKey();

        InventoryItemData item;
        if (!string.IsNullOrEmpty(spriteKey)) item = new InventoryItemData(name, t, null, spriteKey);
        else item = new InventoryItemData(name, t, null);

        pd.AddInventoryItem(item);
        RefreshList();
    }

    // Search managed reel strips for any available authoring sprite or runtime sprite key to use as a sample spriteKey
    private string FindAnyAvailableSymbolSpriteKey()
    {
        // Prefer explicit runtime sprite keys on runtime symbols, then authoring symbol definition sprites
        var mgr = ReelStripDataManager.Instance;
        if (mgr != null && mgr.ReadOnlyLocalData != null)
        {
            foreach (var kv in mgr.ReadOnlyLocalData)
            {
                var strip = kv.Value; if (strip == null) continue;

                // Check runtime symbols first
                var runtimes = strip.RuntimeSymbols;
                if (runtimes != null)
                {
                    for (int i = 0; i < runtimes.Count; i++)
                    {
                        var s = runtimes[i];
                        if (s == null) continue;
                        if (!string.IsNullOrEmpty(s.SpriteKey)) return s.SpriteKey;
                        if (s.Sprite != null) return s.Sprite.name;
                    }
                }

                // Fall back to authoring symbol definitions
                var defs = strip.SymbolDefinitions;
                if (defs != null)
                {
                    for (int i = 0; i < defs.Length; i++)
                    {
                        var d = defs[i]; if (d == null) continue;
                        if (d.SymbolSprite != null) return d.SymbolSprite.name;
                    }
                }
            }
        }

        return null;
    }

    private void OnRemoveItem(string id)
    {
        var pd = GamePlayer.Instance?.PlayerData;
        if (pd == null) return;
        var it = pd.Inventory?.FindById(id);
        if (it != null) pd.RemoveInventoryItem(it);
        RefreshList();
    }

    private void TryCreateEventSystem()
    {
#if UNITY_2023_1_OR_NEWER
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
#else
        if (FindObjectOfType<EventSystem>() != null) return;
#endif
        var go = new GameObject("EventSystem"); go.AddComponent<EventSystem>(); go.AddComponent<StandaloneInputModule>(); DontDestroyOnLoad(go);
    }
}
