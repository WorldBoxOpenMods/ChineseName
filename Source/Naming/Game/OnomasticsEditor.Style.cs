using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static Chinese_Name.Naming.Game.NativeNamingSettings;

namespace Chinese_Name.Naming.Game;

public sealed partial class OnomasticsEditor
{
    private KeyValueField _cultureStyle;
    private RectTransform _stylePicker;
    private RectTransform _styleResults;
    private KeyValueField _cultureExport;
    private RectTransform _exportBox;

    private void AttachCultureStyle()
    {
        var categories = _tab._tab_groups.transform;
        var section = NativeNamingSettings.Stack(categories.parent, "CultureStyle", inset: true);
        section.SetSiblingIndex(categories.GetSiblingIndex());
        _cultureStyle = NativeNamingSettings.Row(section, "CultureNamingStyle", "命名风格", "随物种",
            ToggleStylePicker, "iconCulture");
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
        SetActionText(_cultureStyle, id == null || id == service.DefaultPack ? SpeciesStyle() : active[id].Label ?? id);
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
        Search(_stylePicker, "CultureStyleSearch", "搜索风格", query => ShowStyles(query.Trim()));
        _styleResults = NativeNamingSettings.Stack(_stylePicker, "CultureStyleResults", spacing: 0);
        ShowStyles("");
    }

    private void ShowStyles(string query)
    {
        NativeNamingSettings.Clear(_styleResults);
        var service = NamingApi.Current;
        var world = service.ActiveWorld;
        if (string.IsNullOrWhiteSpace(query))
        {
            string automatic = world.CulturePack == null ? null : service.DefaultPack;
            NativeNamingSettings.Row(_styleResults, "CultureStyle.Auto", SpeciesStyle(), "选择", () => ChooseStyle(automatic));
            if (world.CulturePack != null)
                NativeNamingSettings.Row(_styleResults, "CultureStyle.Inherit", "随世界", "选择", () => ChooseStyle(null));
        }
        foreach (var pack in service.ActivePacks(world).Values.Where(pack => !pack.Supplemental && pack.Id != service.DefaultPack)
                     .OrderBy(pack => pack.Label ?? pack.Id, StringComparer.Ordinal))
        {
            string label = pack.Label ?? pack.Id;
            if (Matches(query, label, pack.Id))
                NativeNamingSettings.Row(_styleResults, "CultureStyle." + pack.Id, label, "选择", () => ChooseStyle(pack.Id));
        }
        // The native scroll content can keep its previous height when the nested results shrink.
        LayoutRebuilder.ForceRebuildLayoutImmediate(_stylePicker.GetComponentInParent<ScrollRect>().content);
    }

    private void ChooseStyle(string pack)
    {
        long id = _culture.id;
        Change(() =>
        {
            WorldNamingStore.ApplyCultureStyle(id, pack);
            DraftCulture(id).Pack = pack;
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
        if (scroll != null) LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
    }
}
