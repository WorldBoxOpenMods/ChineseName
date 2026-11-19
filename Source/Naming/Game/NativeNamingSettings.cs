using System;
using System.Linq;
using NeoModLoader.General;
using UnityEngine;
using UnityEngine.UI;
using NativeInput = UnityEngine.UI.InputField;

namespace Chinese_Name.Naming.Game;

internal static class NativeNamingSettings
{
    internal const string PackTab = "chinesename_packs";
    internal const string LexiconTab = "chinesename_lexicons";
    private const string SettingsTooltip = "chinesename_settings";
    private const string ActionSurface = "ActionButton";
    private static Font _font;

    internal static Font EditorFont() => _font ??= Font.CreateDynamicFontFromOSFont(
        new[] { "Noto Sans CJK SC", "Microsoft YaHei", "PingFang SC", "WenQuanYi Zen Hei" }, 14);

    internal static void Register()
    {
        AssetManager.tooltips.add(new TooltipAsset
        {
            id = SettingsTooltip,
            callback = (tooltip, _, data) =>
            {
                tooltip.name.text = data.tip_name;
                tooltip.setDescription(data.tip_description);
            }
        });
        ScrollWindow.addCallbackShow(Attach);
    }

    private static void Attach(string id)
    {
        if (id is not ("world_info" or "language")) return;
        var window = ScrollWindow.getCurrentWindow();
        bool packages = id == "world_info";
        string tabId = packages ? PackTab : LexiconTab;
        if (window.tabs._tabs.Any(tab => tab.name == tabId)) return;
        var content = AddTab(window, tabId, packages ? "命名包" : "词库", packages ? "iconBooks" : "iconLanguageList");
        if (packages) content.gameObject.AddComponent<NativePackagePage>();
        else content.gameObject.AddComponent<NativeLexiconPage>();
    }

    private static RectTransform AddTab(ScrollWindow window, string id, string label, string icon)
    {
        var tabs = window.tabs;
        var source = tabs.tab_default;
        LM.AddToCurrentLocale(id, label);
        var tab = UnityEngine.Object.Instantiate(source, source.transform.parent);
        UnityEngine.Object.DestroyImmediate(tab.GetComponent<DragOrderElement>());
        var spacer = tabs.transform.Cast<Transform>().FirstOrDefault(item => item.gameObject.activeSelf &&
            item.name.StartsWith("space", StringComparison.Ordinal));
        if (spacer != null)
        {
            tab.transform.SetSiblingIndex(spacer.GetSiblingIndex());
            spacer.gameObject.SetActive(false);
        }
        tab.gameObject.name = id;
        tab.container = tabs;
        tab.tab_elements.Clear();
        tab.tab_action = new WindowMetaTabEvent();
        tab.tab_action.AddListener(selected => tabs.showTab(selected));
        var button = tab.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(tab.doAction);
        var tip = tab.GetComponent<TipButton>();
        tip.textOnClick = id;
        tip.textOnClickDescription = "";
        tab._worldtip_text = label;
        var image = tab.GetComponentsInChildren<Image>(true).FirstOrDefault(child => child.transform != tab.transform);
        var tabIcon = SpriteTextureLoader.getSprite("ui/Icons/" + icon);
        var previousIcon = image?.sprite;
        if (image != null) image.sprite = tabIcon;
        tabs._tabs.Add(tab);
        var page = Stack(window.transform_content, id + ".Page");
        page.gameObject.SetActive(false);
        var titleTemplate = source.tab_elements.FirstOrDefault(item => item != null &&
            item.name.StartsWith("tab_title_container_", StringComparison.Ordinal));
        if (titleTemplate != null)
        {
            var title = UnityEngine.Object.Instantiate(titleTemplate, page);
            title.name = id + ".Title";
            title.gameObject.SetActive(true);
            foreach (var text in title.GetComponentsInChildren<LocalizedText>(true)) text.setKeyAndUpdate(id);
            foreach (var picture in title.GetComponentsInChildren<Image>(true))
                if (previousIcon != null && picture.sprite == previousIcon) picture.sprite = tabIcon;
        }
        var content = Stack(page, id + ".Content", inset: true);
        tabs.addTabContent(tab, page);
        tabs.refillTabsWithContent();
        tabs.disableTabs();
        tabs.enableTab(tabs.getActiveTab());
        return content;
    }

    internal static RectTransform Stack(Transform parent, string name, float spacing = 2, bool inset = false)
    {
        var node = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        var rect = (RectTransform)node.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(210, 0);
        var layout = node.GetComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        if (inset) layout.padding = new RectOffset(4, 4, 2, 2);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        node.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rect;
    }

    internal static void Clear(Transform parent)
    {
        foreach (Transform item in parent)
        {
            item.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(item.gameObject);
        }
    }

    internal static KeyValueField Row(Transform parent, string id, string label, string value, Action action = null,
        string icon = null, string tooltip = null)
    {
        var row = UnityEngine.Object.Instantiate(Resources.Load<KeyValueField>("ui/KeyValueFieldStats"), parent);
        row.name = id;
        row.icon.gameObject.SetActive(icon != null);
        if (icon != null) row.icon.sprite = SpriteTextureLoader.getSprite("ui/Icons/" + icon);
        row.name_text.font = row.value.font = EditorFont() ?? LocalizedTextManager.current_font;
        row.name_text.text = label;
        row.value.text = value;
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 0, 0);
        layout.spacing = 3;
        layout.childAlignment = TextAnchor.MiddleLeft;
        row.GetComponent<LayoutElement>().preferredHeight = 15;
        Bind(row, action, tooltip);
        row.checkOddEvenColor(row.transform.GetSiblingIndex());
        return row;
    }

    internal static void Bind(KeyValueField row, Action action, string tooltip = null)
    {
        if (action != null) AddValueButton(row);
        row.on_click_value = action == null ? null : () =>
        {
            MusicBox.playSoundUI("event:/SFX/UI/Click");
            action();
        };
        row.on_hover_value_out = Tooltip.hideTooltip;
        if (tooltip != null)
            row.on_hover_value = () => Tooltip.show(row, SettingsTooltip, new TooltipData
            {
                tip_name = row.name_text.text,
                tip_description = tooltip
            });
    }

    private static void AddValueButton(KeyValueField row)
    {
        var value = row.value.rectTransform;
        if (value.parent.name == ActionSurface) return;

        var textColumn = (RectTransform)value.parent;
        var layout = textColumn.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = 4;
        textColumn.sizeDelta = new Vector2(textColumn.sizeDelta.x, 16);
        row.GetComponent<LayoutElement>().preferredHeight = 18;
        var label = row.name_text.GetComponent<LayoutElement>() ?? row.name_text.gameObject.AddComponent<LayoutElement>();
        label.minWidth = 0;
        label.preferredWidth = 0;
        label.flexibleWidth = 1;

        var face = ButtonFace(textColumn, ActionSurface);
        face.transform.SetSiblingIndex(value.GetSiblingIndex());
        face.raycastTarget = false;
        value.SetParent(face.transform, false);
        value.anchorMin = Vector2.zero;
        value.anchorMax = Vector2.one;
        value.offsetMin = Vector2.zero;
        value.offsetMax = new Vector2(0, 3);
        row.value.alignment = TextAnchor.MiddleCenter;
        row.value.raycastTarget = true;
        StyleButton(row.value.GetComponent<Button>(), face);
        SetActionText(row, row.value.text);
    }

    internal static void SetActionText(KeyValueField row, string text)
    {
        row.value.text = text;
        if (row.value.transform.parent.name != ActionSurface) return;
        float width = Mathf.Clamp(Mathf.Ceil(row.value.preferredWidth) + 14, 32, 100);
        row.value.transform.parent.GetComponent<LayoutElement>().preferredWidth = width;
    }

    internal static void AddIconButton(KeyValueField row, string id, string icon, string title, bool selected, Action action)
    {
        var primary = row.value.transform.parent;
        var button = IconButton(primary.parent, id, icon, row.name_text.text, title, action);
        button.transform.SetSiblingIndex(primary.GetSiblingIndex());
        if (selected) ((Image)button.targetGraphic).sprite = button.spriteState.selectedSprite;
    }

    internal static Button IconButton(Transform parent, string id, string icon, string title, string description, Action action)
    {
        var face = ButtonFace(parent, id);
        face.GetComponent<LayoutElement>().preferredWidth = 18;
        face.rectTransform.sizeDelta = new Vector2(18, 14);
        var button = face.gameObject.AddComponent<Button>();
        StyleButton(button, face);
        button.onClick.AddListener(() =>
        {
            MusicBox.playSoundUI("event:/SFX/UI/Click");
            action();
        });
        button.OnHover(() => Tooltip.show(button, SettingsTooltip, new TooltipData
        {
            tip_name = title,
            tip_description = description
        }));
        button.OnHoverOut(Tooltip.hideTooltip);

        var node = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)node.transform;
        rect.SetParent(face.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(10, 10);
        var picture = node.GetComponent<Image>();
        picture.sprite = SpriteTextureLoader.getSprite("ui/Icons/" + icon);
        picture.preserveAspect = true;
        picture.raycastTarget = false;
        return button;
    }

    private static Image ButtonFace(Transform parent, string name)
    {
        var node = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        var rect = (RectTransform)node.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(30, 14);
        var layout = node.GetComponent<LayoutElement>();
        layout.preferredHeight = 14;
        var face = node.GetComponent<Image>();
        face.sprite = SpriteTextureLoader.getSprite("ui/tab_button_sort");
        face.type = Image.Type.Sliced;
        return face;
    }

    private static void StyleButton(Button button, Image face)
    {
        button.targetGraphic = face;
        button.transition = Selectable.Transition.SpriteSwap;
        var highlight = SpriteTextureLoader.getSprite("ui/tab_button_sort_selected");
        button.spriteState = new SpriteState { highlightedSprite = highlight, pressedSprite = highlight, selectedSprite = highlight };
    }

    internal static void Export(Transform parent, string initialId, Func<string, string> export)
    {
        var box = Stack(parent, "ExportNamingPack");
        var id = Field(box, SettingsInput(), "ExportPackageId", "命名包 ID", initialId);
        Row(box, "ExportPackage", "命名包", "导出", () =>
        {
            var status = box.Find("ExportStatus").GetComponent<KeyValueField>();
            try
            {
                string path = export(id.inputField.text.Trim());
                status.name_text.text = "已导出 " + System.IO.Path.GetFileName(path);
            }
            catch (Exception error) { status.name_text.text = error.Message; }
            status.gameObject.SetActive(true);
        });
        Row(box, "ExportStatus", "", "").gameObject.SetActive(false);
    }

    internal static NameInput Field(Transform parent, NameInput source, string id, string placeholder,
        string value, Action<string> changed = null)
    {
        var field = UnityEngine.Object.Instantiate(source, parent);
        field.name = id;
        field.can_be_empty = true;
        field.is_onomastics = false;
        field.inputField.onValueChanged = new NativeInput.OnChangeEvent();
        field.inputField.onEndEdit = new NativeInput.EndEditEvent();
        field.inputField.onEndEdit.AddListener(field.checkInput);
        field.inputField.characterLimit = 0;
        var font = EditorFont() ?? LocalizedTextManager.current_font;
        field.textField.font = font;
        if (!string.IsNullOrEmpty(placeholder))
            SetPlaceholder(field.inputField, font, placeholder);
        field.setText(value);
        if (changed != null) field.addListener(changed.Invoke);
        return field;
    }

    private static void SetPlaceholder(NativeInput field, Font font, string value)
    {
        var original = field.textComponent;
        if (field.placeholder is Graphic previous && previous != original) previous.enabled = false;
        var node = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
        var rect = (RectTransform)node.transform;
        rect.SetParent(original.transform.parent, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = original.rectTransform.anchorMin;
        rect.anchorMax = original.rectTransform.anchorMax;
        rect.offsetMin = original.rectTransform.offsetMin;
        rect.offsetMax = original.rectTransform.offsetMax;
        var text = node.GetComponent<Text>();
        text.font = font;
        text.fontSize = original.fontSize;
        text.alignment = original.alignment;
        text.color = new Color(.77f, .74f, .64f, .8f);
        text.raycastTarget = false;
        text.text = value;
        field.placeholder = text;
    }

    private static NameInput SettingsInput() => Resources.Load<ScrollWindow>("windows/world_info")
        .GetComponentInChildren<WorldWindow>(true).nameInput;

    internal static NameInput Search(Transform parent, string id, string placeholder, Action<string> changed) =>
        Field(parent, SettingsInput(), id, placeholder, "", changed);

    internal static bool Matches(string query, string label, string id) =>
        string.IsNullOrWhiteSpace(query) || label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
        id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
}
