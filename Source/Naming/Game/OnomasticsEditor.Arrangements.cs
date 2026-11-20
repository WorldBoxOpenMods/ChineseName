using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chinese_Name.Naming.Game;

public sealed partial class OnomasticsEditor
{
    private const float ArrangementTile = 23;
    private const float ArrangementTileGap = 2;
    private RectTransform _arrangementPanel;
    private RectTransform _conditionPanel;
    private RectTransform _arrangementEditTitle;
    private RectTransform _nativeSectionTitle;
    private RectTransform _nativeArrangement;
    private Text _panelCaptionSource;
    private ConditionalArrangement _editingArrangement;
    private OnomasticsData _conditionalDraft;
    private readonly List<(string Id, RectTransform Tile, Image Marker)> _arrangementTiles = new();

    private IReadOnlyList<ConditionalArrangement> Arrangements =>
        _page?.Effective?.Arrangements ?? Array.Empty<ConditionalArrangement>();

    private void AttachArrangements()
    {
        _nativeSectionTitle = (RectTransform)transform.Find("tab_title_container_onomastics");
        _panelCaptionSource = Resources.Load<GameObject>("windows/unit").GetComponentsInChildren<LocalizedText>(true)
            .Where(label => label.key == "traits").Select(label => label.GetComponent<Text>())
            .First(label => label != null && label.color.a < .3f);
        _arrangementPanel = NativeNamingSettings.Stack(transform, "NamingArrangements", spacing: 3);
        _arrangementPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _nativeSectionTitle.rect.width);
        _nativeArrangement = (RectTransform)_tab.parent_name_variation_1.parent;
        _arrangementPanel.SetSiblingIndex(_nativeArrangement.GetSiblingIndex());
        _conditionPanel = NativeNamingSettings.Stack(transform, "NamingConditions", spacing: 2);
        _conditionPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _nativeArrangement.rect.width);
        _conditionPanel.SetSiblingIndex(_nativeArrangement.GetSiblingIndex());
        _arrangementEditTitle = ArrangementTitle(transform, "ArrangementEditTitle", "排列编辑",
            SpriteTextureLoader.getSprite("ui/Icons/iconBooksWritten"));
        _arrangementEditTitle.transform.SetSiblingIndex(_nativeArrangement.GetSiblingIndex());
        AddPanelCaption(_nativeArrangement, "排列行");
        AddPanelCaption((RectTransform)_tab.parent_asset_specials.parent, "字形盘");
        AddPanelCaption((RectTransform)_tab.parent_asset_groups.parent, "词组盘");
        _arrangementPanel.gameObject.SetActive(false);
        _conditionPanel.gameObject.SetActive(false);
        _arrangementEditTitle.gameObject.SetActive(false);
    }

    private void CloseArrangements()
    {
        CloseConditionEditor();
        if (_arrangementPanel != null) _arrangementPanel.gameObject.SetActive(false);
        if (_conditionPanel != null) _conditionPanel.gameObject.SetActive(false);
        if (_arrangementEditTitle != null) _arrangementEditTitle.gameObject.SetActive(false);
    }

    private void ClearConditionalDraft()
    {
        _editingArrangement = null;
        _conditionalDraft = null;
    }

    internal OnomasticsData ConditionalDraft(Culture culture, string type) =>
        _culture == culture && _type?.Id == type && ReferenceEquals(_map, World.world?.map_stats) ? _conditionalDraft : null;

    internal static bool IsConditionalDraft(OnomasticsData data, out CardNaming cards)
    {
        cards = _active?._editingArrangement?.Cards;
        return cards != null && ReferenceEquals(data, _active._conditionalDraft);
    }

    private void RefreshArrangements()
    {
        bool visible = NamingApi.Current.Conditions(_type.Id).Count != 0;
        _arrangementPanel.gameObject.SetActive(visible);
        _conditionPanel.gameObject.SetActive(visible && _editingArrangement != null);
        _arrangementEditTitle.gameObject.SetActive(visible);
        if (!visible) return;

        Clear(_arrangementPanel);
        _arrangementTiles.Clear();
        MatchSectionFont(_arrangementEditTitle.GetComponentInChildren<Text>());
        ArrangementTitle(_arrangementPanel, "AllArrangementsTitle", "所有排列",
            SpriteTextureLoader.getSprite("ui/Icons/iconBooks"));
        int columns = Mathf.FloorToInt((PreviewWidth + ArrangementTileGap) / (ArrangementTile + ArrangementTileGap));
        int rows = Mathf.CeilToInt((Arrangements.Count + 2) / (float)columns);
        var grid = new GameObject("ArrangementGrid", typeof(RectTransform), typeof(GridLayoutGroup));
        grid.transform.SetParent(_arrangementPanel, false);
        Measure(grid, PreviewWidth, rows * (ArrangementTile + ArrangementTileGap) + 2);
        var layout = grid.GetComponent<GridLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 2, 2);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = columns;
        layout.cellSize = Vector2.one * ArrangementTile;
        layout.spacing = Vector2.one * ArrangementTileGap;
        layout.childAlignment = TextAnchor.UpperCenter;
        foreach (var item in Arrangements) DrawArrangementTile(grid.transform, item);
        DrawArrangementTile(grid.transform, null);
        NativeNamingSettings.LabelButton(grid.transform, "ArrangementAdd", "＋", false, () => Change(AddArrangement));

        Clear(_conditionPanel);
        if (_editingArrangement != null)
        {
            DrawConditionArea(_conditionPanel, _editingArrangement);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_conditionPanel);
        }
        var scroll = _arrangementPanel.GetComponentInParent<ScrollRect>();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_arrangementPanel);
        if (scroll != null) LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
    }

    private RectTransform ArrangementTitle(Transform parent, string id, string title, Sprite icon = null)
    {
        var section = Instantiate(_nativeSectionTitle, parent, false);
        section.name = id;
        var label = section.GetComponentInChildren<Text>();
        SetPanelText(label, title);
        foreach (var graphic in section.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
        foreach (var button in section.GetComponentsInChildren<Button>()) button.enabled = false;
        section.Find("icon_left").GetComponent<Image>().sprite = icon;
        section.Find("icon_right").GetComponent<Image>().sprite = icon;
        return section;
    }

    private void AddPanelCaption(RectTransform panel, string caption)
    {
        var label = Instantiate(_panelCaptionSource, panel, false);
        label.name = "NamingPanelCaption";
        SetPanelText(label, caption);
        Inset(label.rectTransform, 0, 0);
        label.transform.SetAsFirstSibling();
    }

    private void SetPanelText(Text label, string value)
    {
        if (label.GetComponent<LocalizedText>() is { } localization)
        {
            localization.autoField = false;
            localization.key = LocalizedText.DEFAULT_KEY;
            localization.enabled = false;
            Destroy(localization);
        }
        label.text = value;
        MatchSectionFont(label);
    }

    private void MatchSectionFont(Text label)
    {
        var nativeFont = LocalizedTextManager.current_font ?? _nativeSectionTitle.GetComponentInChildren<Text>().font;
        label.font = label.text.All(nativeFont.HasCharacter) ? nativeFont : NativeNamingSettings.EditorFont() ?? nativeFont;
    }

    private void FrameConditionPanel(RectTransform frame)
    {
        var original = _nativeArrangement.GetComponent<Image>();
        var image = frame.gameObject.AddComponent<Image>();
        image.sprite = original.sprite;
        image.type = original.type;
        image.pixelsPerUnitMultiplier = original.pixelsPerUnitMultiplier;
        image.color = original.color;
        image.raycastTarget = false;
    }

    private void DrawArrangementTile(Transform grid, ConditionalArrangement item)
    {
        string id = item?.Id ?? "Default";
        bool active = _editingArrangement?.Id == item?.Id;
        var button = PreviewButton(grid, "Arrangement." + id, "", active, () => Change(() => SelectArrangement(item)));
        button.GetComponent<TipButton>().setHoverAction(() => ShowArrangementTooltip(button, item));
        if (item == null)
        {
            Icon(button.transform, "Default", "ui/Icons/iconBooks", 18);
            return;
        }

        using var preview = NativeNamingArrangement.ReadPackaged(_type.Id, item.Cards);
        var glyph = preview?.getFullTemplateData().Select(glyphId => AssetManager.onomastics_library.get(glyphId))
            .FirstOrDefault(asset => asset != null && !asset.is_divider);
        var image = new GameObject("ArrangementIcon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(button.transform, false);
        Inset(image.rectTransform, 2, 2);
        image.preserveAspect = true;
        image.raycastTarget = false;
        var condition = _type.Id == VanillaNames.War ? WarArrangementIcon(item) :
            item.Expression.Leaves().FirstOrDefault() is { } first ? ClauseIcon(first) : null;
        image.sprite = CompactIcon(condition ?? glyph?.getSprite()) ?? SpriteTextureLoader.getSprite("ui/Icons/iconBooks");
        if (item.Disabled || !item.IsConfigured) image.color = new Color(.65f, .65f, .65f, 1);

        var marker = new GameObject("DropMarker", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        marker.transform.SetParent(button.transform, false);
        Inset(marker.rectTransform, 0, 0);
        marker.color = new Color(1f, .75f, .3f, .4f);
        marker.raycastTarget = false;
        marker.gameObject.SetActive(false);
        _arrangementTiles.Add((id, (RectTransform)button.transform, marker));
        button.gameObject.AddComponent<ArrangementGrip>().Bind(this, id);
    }

    private Sprite WarArrangementIcon(ConditionalArrangement item)
    {
        var expression = item.Expression;
        if (_type.Id != VanillaNames.War || expression.Any || expression.Negated) return null;
        var kind = expression.Children.FirstOrDefault(term => !term.Negated && term.Field == "kind" &&
            term.Test is ArrangementTest.Equal or ArrangementTest.Registered && term.Values.Count == 1);
        if (kind == null) return null;
        return WarConditionIcon(kind.Values[0]);
    }

    private void SelectArrangement(ConditionalArrangement arrangement)
    {
        if (_editingArrangement?.Id == arrangement?.Id) return;
        if (_commitPending) Render();
        var data = arrangement == null ? null : NativeNamingArrangement.ReadPackaged(_type.Id, arrangement.Cards);
        if (arrangement != null && data == null) throw new InvalidOperationException("当前排列所需的字形不可用。");
        ClearConditionalDraft();
        CloseConditionEditor();
        _editingArrangement = arrangement;
        _conditionalDraft = data;
        _tab.loadFromTemplate();
    }

    private void AddArrangement()
    {
        if (Arrangements.Count >= 256) throw new InvalidOperationException("此类别最多可添加 256 条条件排列。");
        if (_commitPending) Render();
        var cards = NamingJson.Copy(_editingArrangement?.Cards ??
            (UsesCards(_card, _rule) ? _card : NativeNamingArrangement.ForRule(_type.Id, _rule)));
        cards.SourceRule = null;
        cards.Enabled = true;
        cards.Template = _tab.getOnomasticsData().getShortTemplate();
        cards.DraftTemplate = null;
        NativeOnomastics.Validate(_tab.getOnomasticsData(), _type.Id, cards);

        var item = new ConditionalArrangement { Id = "arrangement-" + Guid.NewGuid().ToString("N"), Cards = cards };
        PersistArrangements(Arrangements.Append(item).ToList());
        SelectArrangement(item);
    }

    private void ToggleArrangement(ConditionalArrangement item)
    {
        if (_commitPending) Render();
        var copy = NamingJson.Copy(Arrangements.First(entry => entry.Id == item.Id));
        copy.Disabled = !copy.Disabled;
        ReplaceArrangement(copy);
    }

    private void DeleteArrangement()
    {
        if (_commitPending) Render();
        PersistArrangements(Arrangements.Where(other => other.Id != _editingArrangement.Id).ToList());
        SelectArrangement(null);
    }

    private void ReplaceArrangement(ConditionalArrangement item)
    {
        PersistArrangements(Arrangements.Select(other => other.Id == item.Id ? item : other).ToList());
        if (_editingArrangement?.Id == item.Id) _editingArrangement = item;
    }

    private void PersistArrangements(List<ConditionalArrangement> arrangements)
    {
        var service = NamingApi.Current;
        var world = service.GetWorld();
        if (!world.Cultures.TryGetValue(_culture.id, out var settings))
            world.Cultures[_culture.id] = settings = new CultureNaming();
        settings.Arrangements[_type.Id] = arrangements;
        WorldNamingStore.Apply(world);
        DraftCulture(_culture.id).Arrangements[_type.Id] = arrangements;
        _applied = service.ActiveWorld;
        _page.Effective.Arrangements = arrangements;
        if (WorldNamingStore.HasSavedDraft()) SavePartsDraft();
    }

    private void RenderConditional()
    {
        var service = NamingApi.Current;
        SetIntegrated(true);
        if (_commitPending)
        {
            _commitPending = false;
            string template = _conditionalDraft.getShortTemplate();
            if (template != _editingArrangement.Cards.Template)
                try
                {
                    var item = NamingJson.Copy(_editingArrangement);
                    NativeOnomastics.Validate(_conditionalDraft, _type.Id, item.Cards);
                    item.Cards.Template = template;
                    ReplaceArrangement(item);
                }
                catch (InvalidOperationException error) { _page.Message = error.Message; }
        }

        var request = Scenario(_editingArrangement, out _previewMatchesExpression);
        if (_page.PreviewRevision != _page.RenderedRevision)
        {
            _page.RenderedRevision = _page.PreviewRevision;
            CollectPreview(service, request, service.ActiveWorld, null, _page);
        }
        _page.Effective = service.Inspect(request, service.ActiveWorld);
        UpdatePalette();
        RefreshArrangements();
        DrawPreview();
    }

    internal void DragArrangement(string id, PointerEventData pointer, bool drop)
    {
        int from = _arrangementTiles.FindIndex(item => item.Id == id);
        if (from < 0) return;
        var scroll = _arrangementPanel.GetComponentInParent<ScrollRect>();
        scroll.StopMovement();
        int to = from;
        float closest = float.PositiveInfinity;
        var grid = (RectTransform)_arrangementTiles[from].Tile.parent;
        if (RectTransformUtility.RectangleContainsScreenPoint(grid, pointer.position, pointer.pressEventCamera))
        {
            for (int index = 0; index < _arrangementTiles.Count; index++)
            {
                var tile = _arrangementTiles[index].Tile;
                var center = RectTransformUtility.WorldToScreenPoint(pointer.pressEventCamera, tile.TransformPoint(tile.rect.center));
                float distance = (center - pointer.position).sqrMagnitude;
                if (distance >= closest) continue;
                closest = distance;
                to = index;
            }
        }
        for (int index = 0; index < _arrangementTiles.Count; index++)
            _arrangementTiles[index].Marker.gameObject.SetActive(!drop && from != to && index == to);
        if (!drop || from == to) return;
        Change(() =>
        {
            if (_commitPending) Render();
            var items = Arrangements.ToList();
            var moving = items[from];
            items.RemoveAt(from);
            items.Insert(to, moving);
            PersistArrangements(items);
        });
    }
}

public sealed class ArrangementGrip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private OnomasticsEditor _editor;
    private string _id;

    internal void Bind(OnomasticsEditor editor, string id)
    {
        _editor = editor;
        _id = id;
    }

    public void OnBeginDrag(PointerEventData pointer) => _editor.DragArrangement(_id, pointer, drop: false);
    public void OnDrag(PointerEventData pointer) => _editor.DragArrangement(_id, pointer, drop: false);
    public void OnEndDrag(PointerEventData pointer) => _editor.DragArrangement(_id, pointer, drop: true);
}
