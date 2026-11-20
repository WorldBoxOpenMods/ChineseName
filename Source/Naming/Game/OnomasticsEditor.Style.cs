using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static Chinese_Name.Naming.Game.NativeNamingSettings;

namespace Chinese_Name.Naming.Game;

public sealed partial class OnomasticsEditor
{
    private const int PresetColumns = 2;
    private const int VisiblePresetRows = 3;
    private const float PresetHeight = 17;
    private const float PresetSpacing = 3;
    private const float PresetFramePadding = 3;
    private const float PresetScrollbarWidth = 8;
    private const float PresetArrowWidth = 13;
    private KeyValueField _cultureStyle;
    private Text _styleArrow;
    private RectTransform _stylePicker;
    private ScrollRect _styleResults;
    private NameInput _styleSearch;
    private RectTransform _presetSaveForm;
    private KeyValueField _savePreset;
    private KeyValueField _cultureExport;
    private RectTransform _exportBox;

    private void AttachCultureStyle()
    {
        var categories = _tab._tab_groups.transform;
        var section = NativeNamingSettings.Stack(categories.parent, "CultureStyle", inset: true);
        section.SetSiblingIndex(categories.GetSiblingIndex());
        _cultureStyle = NativeNamingSettings.Row(section, "CultureNamingStyle", "命名预设", "随物种",
            ToggleStylePicker, "iconCulture");
        var value = _cultureStyle.value;
        var label = value.rectTransform;
        label.offsetMax -= new Vector2(PresetArrowWidth, 0);
        _styleArrow = TextAt(label, "PresetDisclosure", "▾", 10, TextAnchor.MiddleCenter);
        _styleArrow.rectTransform.anchorMin = Vector2.right;
        _styleArrow.rectTransform.anchorMax = Vector2.one;
        _styleArrow.rectTransform.offsetMin = Vector2.zero;
        _styleArrow.rectTransform.offsetMax = new Vector2(PresetArrowWidth - 1, 0);
        _styleArrow.color = value.color;
        _styleArrow.raycastTarget = true;
    }

    private void AttachCultureExport()
    {
        _cultureExport = NativeNamingSettings.Row(transform, "CulturePackExport", "命名包", "导出", ToggleCultureExport, "iconBooks");
        var anchor = transform.Find("Import / Export");
        if (anchor != null) _cultureExport.transform.SetSiblingIndex(anchor.GetSiblingIndex() + 1);
    }

    private void ToggleCultureExport()
    {
        if (_exportBox != null) { CloseCultureExport(); return; }
        if (_culture == null) return;
        _exportBox = NativeNamingSettings.Stack(transform, "CulturePackExportDetails");
        _exportBox.SetSiblingIndex(_cultureExport.transform.GetSiblingIndex() + 1);
        NativeNamingSettings.Export(_exportBox, "local:culture-" + _culture.id,
            id => WorldNamingStore.ExportCombination(id, _culture.id, _page.ReferenceLanguage));
        RevealInScroll(_exportBox);
    }

    private void CloseCultureExport()
    {
        if (_exportBox == null) return;
        _exportBox.gameObject.SetActive(false);
        Destroy(_exportBox.gameObject);
        _exportBox = null;
    }

    private string SpeciesStyle()
    {
        string origin = _culture.data.original_actor_asset;
        if (origin is "cat" or "civ_cat") return "随物种 · 灵猫";
        var species = origin == null ? null : AssetManager.actor_library.get(origin);
        return species == null ? "随物种" : "随物种 · " + species.getTranslatedName();
    }

    private void RefreshStyleLabel()
    {
        Bind(_cultureStyle, ToggleStylePicker);
        Bind(_cultureExport, ToggleCultureExport);
        var service = NamingApi.Current;
        var world = service.ActiveWorld;
        world.Cultures.TryGetValue(_culture.id, out var settings);
        var active = service.ActivePacks(world);
        string id = new[] { settings?.Pack, world.CulturePack }.FirstOrDefault(key => key != null &&
            active.TryGetValue(key, out var pack) && !pack.Supplemental);
        string label = settings?.Pack != null && world.AppliedPresets.TryGetValue(settings.Pack, out var snapshot)
            ? snapshot.Label : id == null || id == service.DefaultPack ? SpeciesStyle() : active[id].Label ?? id;
        SetActionText(_cultureStyle, label, PresetArrowWidth);
    }

    private void ToggleStylePicker()
    {
        if (_stylePicker != null)
        {
            CloseStylePicker();
            return;
        }
        if (!CurrentContext || !ScrollWindow.isCurrentWindow("culture")) return;
        _stylePicker = NativeNamingSettings.Stack(_cultureStyle.transform.parent, "CultureStylePicker");
        _styleSearch = Search(_stylePicker, "CultureStyleSearch", "搜索预设", query => ShowStyles(query.Trim()));
        _styleResults = CreateStyleResults();
        _savePreset = NativeNamingSettings.Row(_stylePicker, "SaveCulturePreset", "", "保存预设", BeginPresetSave);
        _styleArrow.text = "▴";
        ShowStyles("");
    }

    private void BeginPresetSave()
    {
        if (_presetSaveForm != null) return;
        _presetSaveForm = Row(_stylePicker, "CulturePresetSaveForm", 16, 3);
        _presetSaveForm.SetSiblingIndex(_styleSearch.transform.GetSiblingIndex());
        var form = _presetSaveForm.GetComponent<HorizontalLayoutGroup>();
        form.childForceExpandWidth = false;
        var input = Field(_presetSaveForm, SettingsInput(), "CulturePresetName", "预设名称", "");
        input.inputField.characterLimit = PlayerPresetLibrary.NameLimit;
        var layout = input.GetComponent<LayoutElement>() ?? input.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = layout.preferredWidth = 0;
        layout.flexibleWidth = 1;
        IconButton(_presetSaveForm, "ConfirmCulturePreset", "IconOn", "保存预设", "", () =>
        {
            try
            {
                var preset = PlayerPresetLibrary.Save(input.inputField.text, _culture, _page.ReferenceLanguage);
                ClosePresetSave();
                _styleSearch.setText("");
                ShowStyles("");
                WorldTip.showNowTop("已保存 · " + preset.Snapshot.Label, pTranslate: false);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException or InvalidOperationException)
            {
                WorldTip.showNow(error.Message, pTranslate: false, pPosition: "top", pColor: "#FF637D");
            }
        });
        IconButton(_presetSaveForm, "CancelCulturePreset", "IconOff", "取消", "", ClosePresetSave);
        _styleSearch.gameObject.SetActive(false);
        _savePreset.gameObject.SetActive(false);
        RebuildStyleLayout();
        input.inputField.ActivateInputField();
    }

    private void ClosePresetSave()
    {
        _presetSaveForm.gameObject.SetActive(false);
        Destroy(_presetSaveForm.gameObject);
        _presetSaveForm = null;
        _styleSearch.gameObject.SetActive(true);
        _savePreset.gameObject.SetActive(true);
        Bind(_savePreset, BeginPresetSave);
        RebuildStyleLayout();
    }

    private ScrollRect CreateStyleResults()
    {
        var results = new GameObject("CultureStyleResults", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(ScrollRect));
        results.transform.SetParent(_stylePicker, false);
        var frame = results.GetComponent<Image>();
        var glyphPanel = _tab.parent_asset_specials.parent.GetComponent<Image>();
        frame.sprite = glyphPanel.sprite;
        frame.type = glyphPanel.type;
        frame.color = glyphPanel.color;
        frame.pixelsPerUnitMultiplier = glyphPanel.pixelsPerUnitMultiplier;
        frame.raycastTarget = false;

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(results.transform, false);
        var viewRect = (RectTransform)viewport.transform;
        Inset(viewRect, PresetFramePadding, PresetFramePadding);
        viewport.GetComponent<Image>().color = Color.clear;

        var choices = new GameObject("Choices", typeof(RectTransform), typeof(GridLayoutGroup));
        choices.transform.SetParent(viewRect, false);
        var content = (RectTransform)choices.transform;
        content.anchorMin = Vector2.up;
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1);
        content.anchoredPosition = Vector2.zero;
        var grid = choices.GetComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = PresetColumns;
        grid.spacing = Vector2.one * PresetSpacing;

        var scroll = results.GetComponent<ScrollRect>();
        scroll.viewport = viewRect;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = false;
        scroll.scrollSensitivity = PresetHeight + PresetSpacing;
        scroll.verticalScrollbar = CreatePresetScrollbar(results.transform);
        return scroll;
    }

    private static Scrollbar CreatePresetScrollbar(Transform parent)
    {
        var track = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar)).GetComponent<Image>();
        track.transform.SetParent(parent, false);
        track.rectTransform.anchorMin = Vector2.right;
        track.rectTransform.anchorMax = Vector2.one;
        track.rectTransform.offsetMin = new Vector2(-PresetFramePadding - PresetScrollbarWidth, PresetFramePadding);
        track.rectTransform.offsetMax = new Vector2(-PresetFramePadding, -PresetFramePadding);
        track.color = Toolbox.makeColor("#45483A");

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        handle.transform.SetParent(track.transform, false);
        Inset(handle.rectTransform, 0, 0);
        Surface(handle, "ui/tab_button_sort_selected");
        handle.pixelsPerUnitMultiplier = 2;

        var bar = track.GetComponent<Scrollbar>();
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.targetGraphic = handle;
        bar.handleRect = handle.rectTransform;
        return bar;
    }

    private void ShowStyles(string query)
    {
        var content = _styleResults.content;
        NativeNamingSettings.Clear(content);
        var service = NamingApi.Current;
        var world = service.ActiveWorld;
        var active = service.ActivePacks(world);
        string selected = world.Cultures.TryGetValue(_culture.id, out var settings) ? settings.Pack : null;
        NamingPack currentSnapshot = null;
        if (selected != null && !world.AppliedPresets.TryGetValue(selected, out currentSnapshot) && !active.ContainsKey(selected))
            selected = null;
        int count = 0;
        void AddOption(string id, string label, string pack, NamingPack snapshot = null)
        {
            var button = LabelButton(content, "CultureStyle." + id, label, selected == pack, () => ChooseStyle(pack, snapshot));
            button.gameObject.AddComponent<ScrollableButton>();
            count++;
        }
        if (string.IsNullOrWhiteSpace(query))
        {
            string automatic = world.CulturePack == null ? null : service.DefaultPack;
            AddOption("Auto", SpeciesStyle(), automatic);
            if (world.CulturePack != null) AddOption("Inherit", "随世界", null);
        }
        try
        {
            var presets = PlayerPresetLibrary.Entries.ToArray();
            if (currentSnapshot != null && presets.All(item => item.Snapshot.Id != currentSnapshot.Id) &&
                Matches(query, currentSnapshot.Label, currentSnapshot.Id))
                AddOption(currentSnapshot.Id, currentSnapshot.Label, currentSnapshot.Id, currentSnapshot);
            foreach (var preset in presets)
            {
                var snapshot = preset.Snapshot;
                if (Matches(query, snapshot.Label, snapshot.Id))
                    AddOption(snapshot.Id, snapshot.Label, snapshot.Id, snapshot);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            WorldTip.showNowTop(error.Message, pTranslate: false);
        }
        foreach (var pack in active.Values.Where(pack => !pack.Supplemental && pack.Id != service.DefaultPack)
                     .OrderBy(pack => pack.Label ?? pack.Id, StringComparer.Ordinal))
        {
            string label = pack.Label ?? pack.Id;
            if (Matches(query, label, pack.Id)) AddOption(pack.Id, label, pack.Id);
        }
        int rows = (count + PresetColumns - 1) / PresetColumns;
        float Height(int lines) => Mathf.Max(0, lines * (PresetHeight + PresetSpacing) - PresetSpacing);
        bool overflow = rows > VisiblePresetRows;
        float gutter = overflow ? PresetScrollbarWidth + PresetSpacing : 0;
        float width = ((RectTransform)_cultureStyle.transform).rect.width - 2 * PresetFramePadding - gutter;
        content.sizeDelta = new Vector2(0, Height(rows));
        content.GetComponent<GridLayoutGroup>().cellSize = new Vector2(
            (width - (PresetColumns - 1) * PresetSpacing) / PresetColumns, PresetHeight);
        _styleResults.viewport.offsetMax = new Vector2(-PresetFramePadding - gutter, -PresetFramePadding);
        _styleResults.GetComponent<LayoutElement>().preferredHeight = Height(Mathf.Min(rows, VisiblePresetRows)) + 2 * PresetFramePadding;
        _styleResults.vertical = overflow;
        _styleResults.verticalScrollbar.gameObject.SetActive(overflow);
        _styleResults.gameObject.SetActive(count > 0);
        RebuildStyleLayout();
        _styleResults.StopMovement();
        _styleResults.verticalNormalizedPosition = 1;
    }

    // The native scroll content can keep its previous height when nested results shrink.
    private void RebuildStyleLayout() =>
        LayoutRebuilder.ForceRebuildLayoutImmediate(_stylePicker.GetComponentInParent<ScrollRect>().content);

    private void ChooseStyle(string pack, NamingPack snapshot = null)
    {
        long id = _culture.id;
        Change(() =>
        {
            if (_commitPending) Render();
            DraftCulture(id).Pack = WorldNamingStore.ApplyCultureStyle(id, pack, snapshot);
            _draft.AppliedPresets = NamingApi.Current.ActiveWorld.AppliedPresets;
            if (snapshot != null)
                foreach (string type in snapshot.Arrangements.Keys) DraftCulture(id).Arrangements.Remove(type);
            CloseArrangements();
            SelectArrangement(null);
            CloseStylePicker();
            RefreshStyleLabel();
            ScrollWindow.getCurrentWindow().resetScroll();
        });
    }

    private void CloseStylePicker()
    {
        if (_stylePicker == null) return;
        var scroll = _stylePicker.GetComponentInParent<ScrollRect>();
        _stylePicker.gameObject.SetActive(false);
        Destroy(_stylePicker.gameObject);
        _stylePicker = null;
        _styleResults = null;
        _styleSearch = null;
        _savePreset = null;
        _presetSaveForm = null;
        _styleArrow.text = "▾";
        if (scroll != null) LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
    }
}
