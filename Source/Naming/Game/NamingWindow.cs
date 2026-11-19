using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using NeoModLoader.General;
using NeoModLoader.General.UI.Tab;
using UnityEngine;

namespace Chinese_Name.Naming.Game;

public sealed partial class NamingWindow : MonoBehaviour
{
    private static readonly string[] Sections = { "文化规则", "语言词库", "命名包", "世界方案" };
    private static readonly string[] Surnames = { "父系优先", "母系优先", "随机一方", "新取姓氏" };
    private static readonly string[] Operations = { "词库", "固定文字", "输入字段", "引用部件", "扩展候选", "文化词表" };
    private static NamingWindow _instance;
    private MapStats _map;
    private WorldNaming _draft;
    private string _type = VanillaNames.Person;
    private string _variant;
    private string _pool;
    private string _newPart = "";
    private string _filter = "";
    private string _ruleFilter = "";
    private string _exportId = "local:my-names";
    private string _message;
    private string[] _examples = Array.Empty<string>();
    private string[] _diagnostics = Array.Empty<string>();
    private readonly Dictionary<string, string> _sample = new(StringComparer.Ordinal);
    private long? _culture;
    private long? _language;
    private int _section;
    private int _seed;
    private bool _open;
    private Vector2 _scroll;
    private Rect _rect = new(22, 46, 830, 580);
    private static Font _font;
    private Texture2D _background;
    private EffectiveNames _inspection;
    private (string, long?, long?) _inspectionKey;
    private long _surnameActor = -1;
    private string _surnameText = "";
    private Rule _previewRule;
    private bool _editingText;
    private int _closedFrame = -1;
    private int _fieldIndex;
    private GUIStyle _wrappedLabel;

    internal static void Register(GameObject owner)
    {
        _instance = owner.AddComponent<NamingWindow>();
        const string id = "chinesename_editor";
        LM.AddToCurrentLocale(id, "命名管理");
        LM.AddToCurrentLocale(id + " Description", "编辑本世界的文化规则、语言词库和命名包。");
        var icon = SpriteTextureLoader.getSprite("ui/icons/culture_traits/culture_trait_FamilyNameFromFather");
        var tab = TabManager.CreateTab("ChineseName", id, id + " Description", icon);
        var button = PowerButtonCreator.CreateSimpleButton(id, () =>
        {
            if (_instance._open) _instance.Close();
            else Open();
        }, icon);
        tab.SetLayout(new List<string> { "Editor" });
        tab.PutElement("Editor", button.transform as RectTransform, Vector2.zero, true);
    }

    public static void Open() { if (_instance != null) _instance._open = true; }

    internal static Font EditorFont() => _font ??= Font.CreateDynamicFontFromOSFont(
        new[] { "Noto Sans CJK SC", "Microsoft YaHei", "PingFang SC", "WenQuanYi Zen Hei" }, 14);

    internal static void OpenCulture(Culture culture, string type)
    {
        if (_instance == null) return;
        _instance._open = false;
        NativeNamingTab.OpenCulture(culture, type);
    }

    private void SetOnomasticsContext(Culture culture, string type)
    {
        SyncWorld();
        _culture = culture.id;
        _type = type;
        _language = (type == NativeOnomastics.City && SelectedMetas.selected_city is { } city && city.culture == culture
            ? city.language : SelectedUnit.unit?.culture == culture ? SelectedUnit.unit.language : null)?.id ??
            culture.units.FirstOrDefault(actor => actor.language != null)?.language.id;
        _section = 0;
        _variant = _pool = null;
        _previewRule = null;
        _inspection = null;
        _message = null;
    }

    private static bool PointerOverEditor => _instance is { _open: true } window && !SmoothLoader.isLoading() &&
        window._rect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.isOverUI))]
    internal static class EditorPointer
    {
        private static bool Prefix(ref bool __result)
        {
            if (!PointerOverEditor) return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(HotkeyLibrary), nameof(HotkeyLibrary.checkHotKeyActions))]
    internal static class EditorKeyboard
    {
        private static bool Prefix() =>
            !OnomasticsEditor.ConsumesKeyboard && !(_instance is { } window && (window._closedFrame == Time.frameCount ||
                (window._open && !SmoothLoader.isLoading() &&
                    (window._editingText || PointerOverEditor || Input.GetKeyDown(KeyCode.Escape)))));
    }

    private void Update()
    {
        NativeNamingTab.OpenWhenReady();
        if (!_open || SmoothLoader.isLoading()) return;
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
        else if (Input.GetMouseButtonDown(0) && !PointerOverEditor) _editingText = false;
    }

    private void OnGUI()
    {
        if (!_open || World.world?.map_stats == null || SmoothLoader.isLoading()) return;
        SyncWorld();
        EditorFont();
        if (_background == null)
        {
            _background = new Texture2D(1, 1);
            _background.SetPixel(0, 0, new Color(.10f, .14f, .18f, .99f));
            _background.Apply();
        }
        var previous = GUI.skin.font;
        if (_font != null) GUI.skin.font = _font;
        _wrappedLabel ??= new GUIStyle(GUI.skin.label) { wordWrap = true };
        _rect.width = Math.Min(990, Screen.width - 16);
        _rect.height = Math.Min(580, Screen.height - 36);
        _rect.x = Mathf.Clamp(_rect.x, 8, Screen.width - _rect.width - 8);
        _rect.y = Mathf.Clamp(_rect.y, 8, Screen.height - _rect.height - 8);
        try { _rect = GUILayout.Window(GetInstanceID(), _rect, Draw, "命名管理"); }
        finally { GUI.skin.font = previous; }
    }

    private void SyncWorld()
    {
        if (!ReferenceEquals(_map, World.world.map_stats))
        {
            _map = World.world.map_stats;
            _draft = WorldNamingStore.GetSavedDraft();
            _section = 0;
            _culture = _language = null;
            _message = null;
            _examples = Array.Empty<string>();
            _diagnostics = Array.Empty<string>();
            _previewRule = null;
            _sample.Clear();
            _partEditor.Clear();
        }
    }

    private void Draw(int id)
    {
        _fieldIndex = 0;
        GUI.DrawTexture(new Rect(3, 21, _rect.width - 6, _rect.height - 24), _background);
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            Close();
            Event.current.Use();
            return;
        }
        bool previewShortcut = Event.current.type == EventType.KeyDown && Event.current.control &&
            (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        if (previewShortcut) Event.current.Use();
        int selected = GUILayout.Toolbar(_section, Sections);
        if (selected != _section)
        {
            _section = selected;
            _scroll = Vector2.zero;
            GUI.FocusControl(null);
            GUIUtility.ExitGUI();
        }
        using (new GUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("保存草稿", GUILayout.Width(85)))
                Try(() => { SavePartsDraft(); _message = "草稿将随世界存档保存。"; });
            if (GUILayout.Button("关闭", GUILayout.Width(64))) Close();
        }
        var service = NamingApi.Current;
        var types = service.Types;
        var type = types.FirstOrDefault(value => value.Id == _type) ?? types[0];
        if (Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) _inspection = null;
        _scroll = GUILayout.BeginScrollView(_scroll);
        bool partsPage = false;
        switch (_section)
        {
            case 0:
                SelectContext();
                type = SelectType(types, type);
                partsPage = _culture.HasValue && NativeOnomastics.Meta(type.Id).HasValue;
                if (partsPage)
                {
                    var settings = DraftCulture(_culture.Value);
                    settings.Pack = Binding(service, settings.Pack);
                    if (GUILayout.Button("打开命名规则") && World.world.cultures.get(_culture.Value) is { } culture)
                        OpenCulture(culture, type.Id);
                    EditSurname();
                }
                else
                {
                    if (_culture.HasValue && NativeOnomastics.Supports(type.Id) &&
                        GUILayout.Button("打开命名规则") && World.world.cultures.get(_culture.Value) is { } culture)
                        OpenCulture(culture, type.Id);
                    EditCulture(service, type);
                    EditPreviewInputs(type);
                    EditSurname();
                }
                break;
            case 1:
                SelectContext();
                type = SelectType(types, type);
                EditLanguage(service, type);
                break;
            case 2: EditPacks(service); break;
            case 3: EditWorld(service); break;
        }
        GUILayout.EndScrollView();
        if (!string.IsNullOrEmpty(_message)) GUILayout.Label(_message, _wrappedLabel);
        if (partsPage)
        {
            var world = service.ActiveWorld;
            string pack = DraftCulture(_culture.Value).Pack;
            world.Cultures.TryGetValue(_culture.Value, out var current);
            if (pack != current?.Pack && GUILayout.Button("应用当前页", GUILayout.Width(105)))
                Try(() => ApplyCulturePack(_culture.Value, pack));
        }
        else
        {
            if (_examples.Length > 0) GUILayout.Label(string.Join("   ·   ", _examples), _wrappedLabel);
            if (_diagnostics.Length > 0) GUILayout.Label(string.Join("  ·  ", _diagnostics.Take(3)), _wrappedLabel);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("生成预览", GUILayout.Width(100)) || previewShortcut) Try(() => Preview(type));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("应用当前页", GUILayout.Width(105))) Try(Apply);
            }
        }
        _editingText = GUI.GetNameOfFocusedControl().StartsWith("cn-field-", StringComparison.Ordinal);
        GUI.DragWindow(new Rect(0, 0, _rect.width, 20));
    }

    private void Close()
    {
        if (Event.current != null) GUI.FocusControl(null);
        _closedFrame = Time.frameCount;
        _editingText = false;
        _open = false;
    }

    private void EditSurname()
    {
        var actor = SelectedUnit.unit;
        if (actor == null || actor.isRekt()) return;
        if (_surnameActor != actor.getID())
        {
            _surnameActor = actor.getID();
            _surnameText = VanillaNames.SurnameOf(actor) ?? "";
        }
        _surnameText = Text("选中生物的姓氏", _surnameText, 24);
        if (GUILayout.Button("单独修改姓氏", GUILayout.Width(155)))
            Try(() => { VanillaNames.ChangeSurname(actor, _surnameText); _message = "姓氏已修改。"; });
    }

    private void SelectContext()
    {
        var cultures = World.world.cultures.OrderBy(c => c.id).ToArray();
        var languages = World.world.languages.OrderBy(l => l.id).ToArray();
        using (new GUILayout.VerticalScope())
        {
            var culture = Cycle("文化", _culture, new long?[] { null }.Concat(cultures.Select(c => (long?)c.id)).ToArray(),
                id => id.HasValue ? cultures.FirstOrDefault(c => c.id == id.Value)?.name ?? id.Value.ToString() : "世界默认");
            bool showLanguage = _section != 0 || !culture.HasValue || !NativeOnomastics.Meta(_type).HasValue;
            var language = showLanguage ? Cycle("语言", _language, new long?[] { null }.Concat(languages.Select(l => (long?)l.id)).ToArray(),
                id => id.HasValue ? languages.FirstOrDefault(l => l.id == id.Value)?.name ?? id.Value.ToString() : "世界默认") : _language;
            if (culture != _culture || language != _language)
            {
                _variant = _pool = null;
                _previewRule = null;
                _sample.Clear();
            }
            _culture = culture;
            _language = language;
        }
    }

    private NameType SelectType(IReadOnlyList<NameType> types, NameType current)
    {
        var type = Cycle("类型", current, types, entry => entry.Label);
        if (_type != type.Id)
        {
            _type = type.Id;
            _variant = _pool = null;
            _previewRule = null;
            _sample.Clear();
        }
        return type;
    }

    private void EditCulture(NamingService service, NameType type)
    {
        if (!_culture.HasValue) { _previewRule = null; GUILayout.Label("选择文化后编辑它的规则。", _wrappedLabel); return; }
        long id = _culture.Value;
        if (!_draft.Cultures.TryGetValue(id, out var culture)) _draft.Cultures[id] = culture = new CultureNaming();
        culture.Pack = Binding(service, culture.Pack);
        if (GUILayout.Button("使用此文化的语言", GUILayout.Width(180)))
        {
            var leader = World.world.cultures.FirstOrDefault(value => value.id == id)?.units.FirstOrDefault();
            if (leader?.language != null) _language = leader.language.id;
            _section = 1;
        }
        var effective = Inspect(service, type);
        if (effective.Rules.Length == 0) { GUILayout.Label("当前方案没有这个类型的规则。"); return; }
        _ruleFilter = Text("搜索规则", _ruleFilter, 64);
        var variants = effective.Rules.Where(rule => rule.Id.IndexOf(_ruleFilter, StringComparison.OrdinalIgnoreCase) >= 0)
            .Select(rule => rule.Id).ToArray();
        if (variants.Length == 0) { GUILayout.Label("没有匹配的规则。"); return; }
        _variant = Cycle("规则", variants.Contains(_variant) ? _variant : variants[0], variants, value => value);
        var rule = effective.Rules.First(value => value.Id == _variant);
        if (_previewRule?.Id != rule.Id) _sample.Clear();
        _previewRule = rule;
        string key = type.Id + "/" + rule.Id;
        if (!culture.Rules.TryGetValue(key, out var change)) change = new RuleEdit();
        if (type.FixedParts.Contains(VanillaNames.Surname))
        {
            var current = change.Inheritance ?? rule.Inheritance;
            var updated = Cycle("姓氏优先", current, new[] { SurnameOrder.Father, SurnameOrder.Mother, SurnameOrder.Random },
                value => Surnames[(int)value]);
            if (current != updated) change.Inheritance = updated;
        }
        foreach (var field in type.Fields.Values)
        {
            if (field.Id == VanillaNames.CultureSpecies) continue;
            string condition = (change.When ?? rule.When).TryGetValue(field.Id, out var original) ? original : "";
            string update = Text("条件 · " + field.Label, condition, 256);
            if (update == condition) continue;
            change.When ??= new Dictionary<string, string>(rule.When);
            if (update.Length == 0) change.When.Remove(field.Id);
            else change.When[field.Id] = update;
        }
        string[] order = rule.Parts.Select(part => part.Id).ToArray();
        for (int index = 0; index < rule.Parts.Count; index++)
        {
            var part = rule.Parts[index];
            var modified = EditPart(EditingPart(part, change));
            if (modified != null) SavePartEdit(part, change, modified);
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (index > 0 && GUILayout.Button("上移", GUILayout.Width(66)))
                {
                    (order[index - 1], order[index]) = (order[index], order[index - 1]);
                    change.Order = order.ToList();
                }
                if (!type.FixedParts.Contains(part.Id) && order.Length > 1 && GUILayout.Button("移除", GUILayout.Width(66)))
                    change.Order = order.Where(value => value != part.Id).ToList();
            }
        }
        using (new GUILayout.HorizontalScope())
        {
            _newPart = GUILayout.TextField(_newPart, 64);
            if (GUILayout.Button("添加部件", GUILayout.Width(96)) && Ids.Local(_newPart) && !order.Contains(_newPart))
            {
                change.Parts[_newPart] = new Part { Id = _newPart, Literal = "" };
                change.Order = order.Concat(new[] { _newPart }).ToList();
                _newPart = "";
            }
            if (GUILayout.Button("重置这条规则", GUILayout.Width(120))) { culture.Rules.Remove(key); return; }
        }
        if (change.Order != null || change.Inheritance.HasValue || change.When != null ||
            change.Parts.Count != 0 || change.Fields.Count != 0)
            culture.Rules[key] = change;
    }

    private void CaptureCardDraft()
    {
        if (!_culture.HasValue || !NativeOnomastics.Supports(_type) ||
            !_draft.Cultures.TryGetValue(_culture.Value, out var settings)) return;
        var template = NativeNamingTab.DraftTemplate(_culture.Value, _type);
        if (template == null) return;
        if (!settings.Cards.TryGetValue(_type, out var choice))
        {
            var culture = World.world.cultures.get(_culture.Value);
            settings.Cards[_type] = choice = NativeOnomastics.Selection(_draft, culture, _type) ?? new CardNaming();
        }
        choice.DraftTemplate = template;
    }

    private void EditPreviewInputs(NameType type)
    {
        foreach (var field in type.Fields.Values)
        {
            if (field.Id == VanillaNames.CultureSpecies) continue;
            string original = SampleValue(field);
            string value = Text("预览 · " + field.Label, original, 256);
            if (original != value) _sample[field.Id] = value;
        }
    }

    private string SampleValue(InputField field)
    {
        if (_sample.TryGetValue(field.Id, out var value)) return value;
        if (_previewRule != null && _previewRule.When.TryGetValue(field.Id, out value)) return value;
        return field.Example == null ? "" : NameEngine.Value(field.Example);
    }

    private static Part EditingPart(Part part, RuleEdit change)
    {
        if (change.Parts.TryGetValue(part.Id, out var added)) return added;
        if (!change.Fields.TryGetValue(part.Id, out var edit)) return part;
        var operation = edit.Operation ?? part;
        return new Part
        {
            Id = part.Id, Pool = operation.Pool, PoolInput = operation.PoolInput, Literal = operation.Literal, Input = operation.Input,
            FallbackPool = operation.FallbackPool, Copy = operation.Copy, Provider = operation.Provider, Choices = operation.Choices,
            Optional = edit.Optional ?? part.Optional,
            Chance = edit.Chance ?? part.Chance
        };
    }

    private static void SavePartEdit(Part baseline, RuleEdit change, Part value)
    {
        if (change.Parts.ContainsKey(value.Id)) { change.Parts[value.Id] = value; return; }
        var current = EditingPart(baseline, change);
        if (!change.Fields.TryGetValue(value.Id, out var edit)) change.Fields[value.Id] = edit = new PartEdit();
        if (current.Optional != value.Optional) edit.Optional = value.Optional;
        if (current.Chance != value.Chance) edit.Chance = value.Chance;
        if (current.Pool != value.Pool || current.PoolInput != value.PoolInput || current.Literal != value.Literal || current.Input != value.Input ||
            current.FallbackPool != value.FallbackPool || current.Copy != value.Copy || current.Provider != value.Provider ||
            !(current.Choices ?? new List<string>()).SequenceEqual(value.Choices ?? new List<string>()))
            edit.Operation = new Part { Id = value.Id, Pool = value.Pool, PoolInput = value.PoolInput, Literal = value.Literal, Input = value.Input,
                FallbackPool = value.FallbackPool, Copy = value.Copy, Provider = value.Provider, Choices = value.Choices };
    }

    private Part EditPart(Part part)
    {
        GUILayout.Space(6);
        int op = part.Pool != null ? 0 : part.Literal != null ? 1 : part.Input != null ? 2 : part.Copy != null ? 3 : part.Provider != null ? 4 : 5;
        int next = Cycle("部件 · " + part.Id, op, new[] { 0, 1, 2, 3, 4, 5 }, value => Operations[value]);
        string initial = part.Pool ?? part.Literal ?? part.Input ?? part.Copy ?? part.Provider ?? string.Join("、", part.Choices);
        string value = Text("内容", next == op ? initial : "", 128);
        string poolInput = next == 0 ? Text("词库选择字段", part.PoolInput, 64) : "";
        if (poolInput.Length == 0) poolInput = null;
        bool optional = GUILayout.Toggle(part.Optional, "可省略");
        double chance = optional ? part.Chance : 1;
        if (optional)
        {
            GUILayout.Label("出现概率 " + chance.ToString("P0", CultureInfo.InvariantCulture));
            float slider = GUILayout.HorizontalSlider((float)chance, 0, 1);
            if (Math.Abs(slider - chance) > .001) chance = Math.Round(slider, 2);
        }
        if (op == next && value == initial && poolInput == part.PoolInput && optional == part.Optional && chance == part.Chance) return null;
        var result = new Part { Id = part.Id, Optional = optional, Chance = chance,
            FallbackPool = op == next && value == initial && poolInput == part.PoolInput ? part.FallbackPool : null };
        switch (next)
        {
            case 0: result.Pool = value; result.PoolInput = poolInput; break;
            case 1: result.Literal = value; break;
            case 2: result.Input = value; break;
            case 3: result.Copy = value; break;
            case 4: result.Provider = value; break;
            case 5: result.Choices = SplitTerms(value); break;
        }
        return result;
    }

    private void EditLanguage(NamingService service, NameType type)
    {
        if (!_language.HasValue) { GUILayout.Label("选择语言后调整它的用词。"); return; }
        if (!_draft.Languages.TryGetValue(_language.Value, out var language))
            _draft.Languages[_language.Value] = language = new LanguageNaming();
        language.Pack = Binding(service, language.Pack);
        var effective = Inspect(service, type);
        var ids = effective.Pools.Keys.Concat(UsedAliasPools(type.Id, effective))
            .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (ids.Length == 0) { GUILayout.Label("当前类型没有使用语言词库。"); return; }
        _pool = Cycle("词库", ids.Contains(_pool) ? _pool : ids[0], ids, value => value);
        if (!language.Pools.TryGetValue(_pool, out var edit)) edit = new PoolEdit();
        var groups = service.WordGroups(_draft, language, _culture);
        var inherited = language.PreferencesFor(_pool).OrderByDescending(pair => pair.Value)
            .Select(pair => $"{pair.Key} {pair.Value:P0}").ToArray();
        if (_draft.LanguageParents.TryGetValue(_language.Value, out long parent))
            GUILayout.Label("源语言 · " + (World.world.languages.get(parent)?.name ?? parent.ToString()));
        if (inherited.Length > 0) GUILayout.Label("词群偏好 · " + string.Join(" · ", inherited), _wrappedLabel);
        if (groups.TryGetValue(_pool, out var choices) || edit.Group != null)
        {
            var options = new[] { (string)null, edit.Group }.Concat(choices ?? Array.Empty<string>()).Distinct().ToArray();
            edit.Group = Cycle("限定词群", edit.Group, options, id => id ?? "继承默认");
        }
        _filter = Text("筛选", _filter, 80);
        var words = effective.GetPool(_pool);
        double total = words.Sum(word => word.Weight);
        foreach (var group in words.Where(word => word.Text.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0)
                     .GroupBy(word => word.Text, StringComparer.Ordinal).Take(50))
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label(group.Key + "  ·  " + (group.Sum(word => word.Weight) / total).ToString("P1") +
                    (group.Count() > 1 ? "  ·  " + group.Count() + " 条" : ""));
                if (GUILayout.Button("禁用这个词", GUILayout.Width(100))) edit.ExcludeText.Add(group.Key);
            }
            foreach (var word in group)
            {
                using var row = new GUILayout.HorizontalScope();
                GUILayout.Label(word.Id);
                if (GUILayout.Button("增权", GUILayout.Width(55)))
                    edit.Bias[word.Id] = Math.Min(10000, edit.Bias.TryGetValue(word.Id, out double old) ? old * 2 : 2);
                if (GUILayout.Button("禁用此条", GUILayout.Width(75))) edit.ExcludeIds.Add(word.Id);
            }
        }
        foreach (var excluded in edit.ExcludeText.Concat(edit.ExcludeIds).Distinct().ToArray())
            if (GUILayout.Button("恢复 · " + excluded)) { edit.ExcludeText.Remove(excluded); edit.ExcludeIds.Remove(excluded); }
        if (GUILayout.Button("重置此词库")) language.Pools.Remove(_pool);
        else if (edit.Group != null || edit.Bias.Count > 0 || edit.ExcludeIds.Count > 0 || edit.ExcludeText.Count > 0)
            language.Pools[_pool] = edit;
        else language.Pools.Remove(_pool);
    }

    private IEnumerable<string> UsedAliasPools(string type, EffectiveNames effective)
    {
        if (!_culture.HasValue || !NativeOnomastics.Supports(type)) yield break;
        var culture = World.world.cultures.get(_culture.Value);
        if (culture == null) yield break;
        var choice = NativeOnomastics.Selection(_draft, culture, type);
        using var cards = choice?.Enabled == true ? NativeOnomastics.Read(culture, type, choice) : null;
        var packaged = effective.Rules.Select(rule => rule.NativeCards).FirstOrDefault(card => card != null);
        using var defaults = cards == null ? NativeNamingArrangement.ReadPackaged(type, packaged) : null;
        if ((cards ?? defaults) is not { } arrangement) yield break;
        foreach (string alias in NativeOnomastics.SelectorTokens(arrangement))
            if (effective.ResolvePoolToken(alias, out _) is string pool && effective.PoolIds.Contains(pool))
                yield return pool;
    }

    private void EditPacks(NamingService service)
    {
        GUILayout.Label(WorldNamingStore.PackageDirectory, _wrappedLabel);
        if (GUILayout.Button("重新扫描目录")) Try(() => _message = string.Join("  ·  ", WorldNamingStore.ReloadPacks()));
        foreach (var id in service.Packages.Concat(_draft.EnabledPacks).Distinct().OrderBy(id => id, StringComparer.Ordinal))
        {
            var pack = service.DescribePackage(id);
            bool active = _draft.EnabledPacks.Contains(id);
            if (id == service.DefaultPack || service.IsModDefault(id)) { GUILayout.Label((pack?.Label ?? id) + "  ·  " + id); continue; }
            bool enabled = GUILayout.Toggle(active, (pack?.Label ?? id) + "  ·  " + id + (pack == null ? " · 文件缺失" : ""));
            if (enabled && !active) _draft.EnabledPacks.Add(id);
            if (!enabled && active) _draft.EnabledPacks.Remove(id);
            if (enabled && pack != null)
            {
                int priority = _draft.PackPriorities.TryGetValue(id, out int assigned) ? assigned : pack.Priority;
                int adjusted = Cycle("包优先级", priority,
                    new[] { Math.Max(-10000, priority - 1), priority, Math.Min(10000, priority + 1) }, value => value.ToString());
                if (priority != adjusted) _draft.PackPriorities[id] = adjusted;
            }
        }
        _exportId = Text("导出包 ID", _exportId, 129);
        if (_culture.HasValue && _draft.Cultures.ContainsKey(_culture.Value) &&
            GUILayout.Button("导出所选文化的规则修改"))
        {
            Try(() => _message = WorldNamingStore.ExportEdits(_exportId, _culture, null, _draft));
        }
        if (_language.HasValue && _draft.Languages.ContainsKey(_language.Value) &&
            GUILayout.Button("导出所选语言的词库修改"))
            Try(() => _message = WorldNamingStore.ExportEdits(_exportId, null, _language, _draft));
        if (GUILayout.Button("导出当前组合为独立基础包"))
            Try(() => _message = WorldNamingStore.ExportCombination(_exportId, _culture, _language, _draft));
    }

    private void EditWorld(NamingService service)
    {
        GUILayout.Label("世界默认文化规则");
        _draft.CulturePack = Binding(service, _draft.CulturePack);
        GUILayout.Label("世界默认语言词库");
        _draft.LanguagePack = Binding(service, _draft.LanguagePack);
        if (GUILayout.Button("丢弃草稿并恢复生效设置"))
        {
            WorldNamingStore.SaveDraft(null);
            _draft = service.GetWorld();
            _inspection = null;
            _message = "已恢复生效设置。";
        }
    }

    private string Binding(NamingService service, string value)
    {
        var ids = new[] { (string)null }.Concat(_draft.EnabledPacks.Where(id => service.DescribePackage(id)?.Supplemental == false))
            .Concat(value == null ? Array.Empty<string>() : new[] { value }).Distinct().ToArray();
        return Cycle("基础包", value, ids, id => id == null ? "继承默认" : service.DescribePackage(id)?.Label ?? id);
    }

    private void ApplyCulturePack(long cultureId, string pack)
    {
        var world = NamingApi.Current.GetWorld();
        if (!world.Cultures.TryGetValue(cultureId, out var culture)) world.Cultures[cultureId] = culture = new CultureNaming();
        culture.Pack = pack;
        WorldNamingStore.Apply(world);
        _message = "已应用。设置随世界存档保存。";
    }

    private NameRequest Request(NameType type, ulong seed)
    {
        var inputs = new Dictionary<string, object>();
        foreach (var field in type.Fields.Values)
        {
            string value = SampleValue(field);
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (field.Kind == InputKind.Integer && int.TryParse(value, out int integer)) inputs[field.Id] = integer;
            else if (field.Kind == InputKind.Flag && bool.TryParse(value, out bool flag)) inputs[field.Id] = flag;
            else inputs[field.Id] = value;
        }
        if (type.Fields.ContainsKey(VanillaNames.CultureSpecies) && _culture is long culture &&
            World.world.cultures.get(culture)?.data.original_actor_asset is string origin)
            inputs[VanillaNames.CultureSpecies] = origin;
        if (type.Id == VanillaNames.Person && !inputs.ContainsKey("kind")) inputs["kind"] = "sapient";
        return new NameRequest(type.Id, seed, _culture, _language, inputs);
    }

    private EffectiveNames Inspect(NamingService service, NameType type)
    {
        var key = (type.Id, _culture, _language);
        if (_inspection == null || key != _inspectionKey)
        {
            _inspection = service.Inspect(Request(type, 0), _draft);
            _inspectionKey = key;
        }
        return _inspection;
    }

    private void Preview(NameType type)
    {
        CaptureCardDraft();
        var service = NamingApi.Current;
        var preview = CurrentPageWorld();
        var requests = Enumerable.Range(_seed, 5).Select(seed => service.Preview(Request(type, (ulong)seed), preview)).ToArray();
        _seed += 5;
        _examples = requests.Where(result => result.Success).Select(result => result.Text).Distinct().ToArray();
        _diagnostics = requests.SelectMany(result => result.Diagnostics).Distinct().ToArray();
        _message = _examples.Length == 0 ? "无法按这份草稿生成名字。" : null;
    }

    private WorldNaming CurrentPageWorld()
    {
        var current = NamingApi.Current.GetWorld();
        switch (_section)
        {
            case 0:
                if (_culture.HasValue && _draft.Cultures.TryGetValue(_culture.Value, out var culture))
                    current.Cultures[_culture.Value] = NamingJson.Copy(culture);
                break;
            case 1:
                if (_language.HasValue && _draft.Languages.TryGetValue(_language.Value, out var language))
                    current.Languages[_language.Value] = language;
                break;
            case 2: current.EnabledPacks = _draft.EnabledPacks; current.PackPriorities = _draft.PackPriorities; break;
            case 3: current.CulturePack = _draft.CulturePack; current.LanguagePack = _draft.LanguagePack; break;
        }
        return current;
    }

    private void Apply()
    {
        CaptureCardDraft();
        var current = CurrentPageWorld();
        var service = NamingApi.Current;
        if (_section == 0 && _culture.HasValue)
        {
            var issues = service.Types.SelectMany(type =>
                service.Inspect(new NameRequest(type.Id, 0, _culture, _language), current).Diagnostics)
                .Where(value => value.StartsWith("invalid-edit:", StringComparison.Ordinal) ||
                                value.StartsWith("undeclared-input:", StringComparison.Ordinal) ||
                                value.StartsWith("missing-part:", StringComparison.Ordinal)).Distinct().ToArray();
            if (issues.Length > 0) throw new InvalidOperationException("请修复规则草稿 · " + string.Join(" · ", issues.Take(3)));
            if (current.Cultures.TryGetValue(_culture.Value, out var settings))
            {
                settings.Cards.TryGetValue(_type, out var choice);
                settings.Cards = service.ActiveWorld.Cultures.TryGetValue(_culture.Value, out var previous)
                    ? NamingJson.Copy(previous.Cards) : new Dictionary<string, CardNaming>();
                if (choice != null && NativeOnomastics.Supports(_type))
                {
                    var culture = World.world.cultures.get(_culture.Value);
                    if (culture == null) throw new InvalidOperationException("该文化已不存在。");
                    settings.Cards[_type] = choice;
                    if (choice.Enabled)
                    {
                        NativeOnomastics.Apply(culture, _type, choice, () => WorldNamingStore.Apply(current));
                        _message = "已应用。设置随世界存档保存。";
                        return;
                    }
                    choice.DraftTemplate = null;
                }
            }
        }
        WorldNamingStore.Apply(current);
        _message = "已应用。设置随世界存档保存。";
    }

    private void Try(Action action)
    {
        try { action(); }
        catch (Exception error) { _message = error.Message; }
    }

    private static T Cycle<T>(string label, T current, IReadOnlyList<T> values, Func<T, string> display)
    {
        using var row = new GUILayout.HorizontalScope();
        GUILayout.Label(label, GUILayout.Width(106));
        int index = Array.IndexOf(values.ToArray(), current);
        bool back = GUILayout.Button("‹", GUILayout.Width(30));
        GUILayout.Label(display(current), GUILayout.MinWidth(60));
        bool next = GUILayout.Button("›", GUILayout.Width(30));
        if (back) return values[(Math.Max(index, 0) + values.Count - 1) % values.Count];
        if (next) return values[(index + 1) % values.Count];
        return current;
    }

    private string Text(string label, string value, int limit)
    {
        using var row = new GUILayout.HorizontalScope();
        GUILayout.Label(label, GUILayout.Width(130));
        GUI.SetNextControlName("cn-field-" + _fieldIndex++);
        return GUILayout.TextField(value ?? "", limit);
    }
}
