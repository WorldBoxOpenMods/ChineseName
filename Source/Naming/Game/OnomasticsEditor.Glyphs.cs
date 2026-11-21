using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LayoutGroupExt;
using UnityEngine;
using UnityEngine.UI;

namespace Chinese_Name.Naming.Game;

public sealed partial class OnomasticsEditor
{
    private OnomasticsAssetButton[] _palette;
    private bool _loadingNative;
    private int? _placementBranch;
    private GridLayoutGroupExtended _paletteGrid;
    private RectTransform _palettePanel;
    private float _originalPanelHeightDelta;
    private GridLayoutGroupExtended.Constraint _originalConstraint;
    private int _originalColumns;
    private GridLayoutGroupExtended.Axis _originalAxis;
    private readonly Dictionary<RectTransform, float> _paletteLeftEdges = new();
    private float _paletteWidth;
    private float _paletteCenter;

    private void AttachPalette()
    {
        _paletteGrid = _tab.parent_asset_specials.GetComponent<GridLayoutGroupExtended>();
        _palettePanel = (RectTransform)_tab.parent_asset_specials.parent;
        _originalPanelHeightDelta = _palettePanel.sizeDelta.y;
        if (_paletteGrid == null) return;
        _originalConstraint = _paletteGrid.constraint;
        _originalColumns = _paletteGrid.constraintCount;
        _originalAxis = _paletteGrid.startAxis;
    }

    private void FitPalette()
    {
        if (_paletteGrid == null) return;
        if (!_integrated)
        {
            _paletteGrid.constraint = _originalConstraint;
            _paletteGrid.constraintCount = _originalColumns;
            _paletteGrid.startAxis = _originalAxis;
            _palettePanel.sizeDelta = new Vector2(_palettePanel.sizeDelta.x, _originalPanelHeightDelta);
            _paletteWidth = 0;
            _paletteLeftEdges.Clear();
            return;
        }
        if (!_palettePanel.gameObject.activeInHierarchy) return;
        var rect = (RectTransform)_paletteGrid.transform;
        var panel = _palettePanel.rect;
        float width = panel.width;
        float center = rect.InverseTransformPoint(_palettePanel.TransformPoint(panel.center)).x - rect.rect.xMin;
        if (width <= 0 || Mathf.Approximately(width, _paletteWidth) && Mathf.Approximately(center, _paletteCenter)) return;

        var cell = _paletteGrid.cellSize;
        float minStep = cell.x + _paletteGrid.spacing.x;
        if (minStep <= 0) return;
        _paletteWidth = width;
        _paletteCenter = center;
        var padding = _paletteGrid.padding;
        float inset = Mathf.Max(3, Mathf.Max(padding.left, padding.right));
        float usableWidth = width - inset * 2;
        int columns = 1 + Mathf.Max(0, Mathf.FloorToInt((usableWidth - cell.x) / minStep));
        float step = columns > 1 ? (usableWidth - cell.x) / (columns - 1) : 0;
        var visible = _palette.Where(button => button.gameObject.activeSelf).ToArray();
        _paletteLeftEdges.Clear();
        for (int first = 0; first < visible.Length; first += columns)
        {
            int count = Mathf.Min(columns, visible.Length - first);
            float left = center - (cell.x + (count - 1) * step) / 2;
            for (int column = 0; column < count; column++)
                _paletteLeftEdges.Add(visible[first + column].getRect(), left + column * step);
        }

        _paletteGrid.constraint = GridLayoutGroupExtended.Constraint.FixedColumnCount;
        _paletteGrid.constraintCount = columns;
        _paletteGrid.startAxis = GridLayoutGroupExtended.Axis.Horizontal;
        int rows = Mathf.Max(1, Mathf.CeilToInt(visible.Length / (float)columns));
        float height = padding.vertical + rows * cell.y + (rows - 1) * _paletteGrid.spacing.y + 3;
        _palettePanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
    }

    // 只调整字形的横坐标，让原版网格继续维护动画和拖拽位置。
    [HarmonyPatch(typeof(LayoutGroupExtended), nameof(LayoutGroupExtended.SetPosition))]
    internal static class PalettePosition
    {
        private static void Prefix(LayoutGroupExtended __instance, RectTransform rect, ref Vector2 pos, int axis)
        {
            var editor = _active;
            if (axis == 0 && editor != null && editor._integrated && __instance == editor._paletteGrid &&
                editor._paletteLeftEdges.TryGetValue(rect, out float left))
                pos.x = left + rect.rect.width * rect.pivot.x;
        }
    }

    internal void BeforeNativeLoad()
    {
        _loadingNative = true;
        _placementBranch = null;
        if (_page != null) _dirty = true;
    }

    internal void AfterNativeLoad()
    {
        _loadingNative = false;
        UpdatePalette();
    }

    private void UpdatePalette()
    {
        string target = NativeNamingTab.CurrentType(_tab);
        bool current = target != null && _type?.Id == target && _culture == SelectedMetas.selected_culture;
        foreach (var button in _palette)
        {
            string id = button.onomastics_asset.id;
            bool show;
            if (NativeNamingGlyphs.Find(id) != null) show = false; // 旧短码只用于读取和迁移。
            else if (NativeNamingGlyphs.FindInput(id) != null || id is NativeNamingGlyphs.AsPool or NativeNamingGlyphs.KeepSurname)
                show = current && NativeNamingGlyphs.AppliesTo(id, target);
            else if (id == NativeNamingGlyphs.RuleSeparator)
                show = current && _rule != null &&
                    (!string.IsNullOrEmpty(_rule.ChineseSeparator) || !string.IsNullOrEmpty(_rule.OriginalSeparator));
            else continue;
            if (button.gameObject.activeSelf == show) continue;
            button.gameObject.SetActive(show);
            _paletteWidth = 0;
        }
    }

    private void SyncArrangement()
    {
        if (NativeNamingTab.HasChanged(_culture.id, _type.Id)) return;
        var data = _tab.getOnomasticsData();
        if (data == null) return;
        if (UsesCards(_card, _rule))
        {
            string template = _card.DraftTemplate ?? _card.Template;
            bool refreshed = template != null && data.getShortTemplate() != template;
            using var decoded = refreshed ? NativeNamingArrangement.ReadTemplate(template) : null;
            if (refreshed && decoded == null)
            {
                if (!NativeNamingArrangement.FillDefault(data, _type.Id, _rule, _page.Effective, out bool changed) || !changed) return;
            }
            else if (decoded != null) NativeNamingArrangement.ReplaceData(data, decoded);
            if ((!refreshed || decoded != null) && NativeNamingArrangement.TryUpgrade(_type.Id, data, _rule, _card, out var upgraded))
            {
                _card = upgraded;
                DraftCulture(_culture.id).Cards[_type.Id] = upgraded;
                _page.PreviewRevision++;
            }
            else if (!refreshed && !NativeNamingArrangement.ShowLegacySeparators(data, _card, _rule)) return;
            if (refreshed) _page.PreviewRevision++;
        }
        else
        {
            if (!NativeNamingArrangement.FillDefault(data, _type.Id, _rule, _page.Effective, out bool changed) || !changed) return;
        }
        NativeNamingTab.RememberDefault(_culture.id, _type.Id, data);
        _tab.loadInitialButtons();
        _tab.name_variation_1_drag_container.markForUpdate();
    }

    private CardNaming ReadArrangementChoice()
    {
        var choice = NativeOnomastics.Selection(_draft, _culture, _type.Id, _page.Effective, _rule);
        if (!NativeNamingTab.HasChanged(_culture.id, _type.Id)) return choice;
        if (!UsesCards(choice, _rule)) return NativeNamingArrangement.ForRule(_type.Id, _rule);
        if (choice.ExplicitSeparators || !NativeNamingTab.HadRuleSeparator(_culture.id, _type.Id)) return choice;
        choice = NamingJson.Copy(choice);
        choice.ExplicitSeparators = true;
        return choice;
    }

    internal bool ResetArrangement()
    {
        if (Visible && CurrentContext && _editingArrangement != null)
        {
            using var restored = NativeNamingArrangement.ReadTemplate(_editingArrangement.Cards.Template);
            if (restored == null) return true;
            NativeNamingArrangement.ReplaceData(_conditionalDraft, restored);
            _page.Message = null;
            _page.PreviewRevision++;
            _tab.loadFromTemplate();
            return true;
        }
        if (!Visible || _rule == null || _culture != SelectedMetas.selected_culture ||
            _type.Id != NativeNamingTab.CurrentType(_tab) ||
            NativeNamingArrangement.Default(_type.Id, _rule) == null) return false;
        Change(() =>
        {
            var settings = DraftCulture(_culture.id);
            settings.Rules.Remove(RuleKey(_type, _rule));
            settings.DefaultArrangementEdits.Remove(CultureNaming.DefaultArrangementKey(_type.Id, _page.Effective.ArrangementBasis, _rule.Id));
            settings.Cards[_type.Id] = new CardNaming { Enabled = false };
            NativeNamingTab.ForgetDraft(_culture.id, _type.Id);
            _tab.loadFromTemplate();
        }, commit: true);
        return true;
    }

    private OnomasticsAssetButton[] Cards() => _tab.parent_name_variation_1.Cast<Transform>()
        .Where(child => child.gameObject.activeSelf).Select(child => child.GetComponent<OnomasticsAssetButton>())
        .Where(button => button != null).ToArray();

    private int BranchOf(OnomasticsAssetButton selection) => Cards().TakeWhile(card => card != selection)
        .Count(card => card.onomastics_asset.id == "divider");

    private OnomasticsAssetButton PlaceInBranch(OnomasticsAssetButton[] cards, int branch)
    {
        var added = cards.Last();
        var border = cards.Where(card => card.onomastics_asset.id == "divider").Skip(branch).FirstOrDefault();
        if (border != null) added.transform.SetSiblingIndex(border.transform.GetSiblingIndex());
        return added;
    }

    internal void PaletteClicked(int previousCount)
    {
        if (!Visible || _culture != SelectedMetas.selected_culture || _type.Id != NativeNamingTab.CurrentType(_tab)) return;
        var cards = Cards();
        if (cards.Length <= previousCount) return;
        var card = cards.Last();
        if (card.onomastics_asset.id == "divider")
        {
            // 新分支出现后，后续新增字形可以直接填入。
            _placementBranch = BranchOf(card) + 1;
            _dirty = true;
        }
        else if (NativeNamingGlyphs.Find(card.onomastics_asset.id) == null &&
            _placementBranch.HasValue && cards.Any(item => item.onomastics_asset.id == "divider"))
        {
            PlaceInBranch(cards, _placementBranch.Value);
            _tab.resetNameGenerationTextBox();
            _tab.name_variation_1_drag_container.markForUpdate();
        }
    }

}
