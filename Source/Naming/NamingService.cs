using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Chinese_Name.Naming;

public sealed class NamingService
{
    private const int ResolutionCacheLimit = 512;
    private readonly Dictionary<string, NameType> _types = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CultureTab> _cultureOnomastics = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CultureNameGlyph> _cultureGlyphs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string ExampleKind, bool WorldScoped, bool ShareDefaultLayout)> _categoryFacts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, CultureCondition>> _conditions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<Dictionary<string, object>, bool>> _contexts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Action<Dictionary<string, object>>>> _examples = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Func<string, string, string>>> _conditionIcons = new(StringComparer.Ordinal);
    private readonly HashSet<(string Type, string Field)> _scopedInputs = new();
    private Dictionary<string, NamingPack> _packs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<NameRequest, IEnumerable<Word>>> _providers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _modDefaults = new(StringComparer.Ordinal);
    private readonly LinkedList<(string Type, long? Culture, long? Language)> _recent = new();
    private readonly Dictionary<(string Type, long? Culture, long? Language),
        (LinkedListNode<(string Type, long? Culture, long? Language)> Node, EffectiveNames Names)> _resolved = new();
    private readonly string _defaultPack;
    private WorldNaming _world = new();
    internal Func<NameType, NameRequest, WorldNaming, EffectiveNames, Rule[], IEnumerable<NamingCandidate>> GameCandidates { get; set; }
    private Action<CultureNameGlyph> _registerGlyph;
    private Func<CultureNameGlyph, bool> _glyphAvailable;
    internal ArrangementCodec ArrangementCodec { get; set; }

    public NamingService(string defaultPack = null) => _defaultPack = defaultPack;

    public string DefaultPack => _defaultPack;
    public IReadOnlyList<NameType> Types => _types.Values.OrderBy(type => type.Id, StringComparer.Ordinal).ToArray();
    public IReadOnlyList<string> Packages => _packs.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray();
    public NamingPack GetPackage(string id) => _packs.TryGetValue(id, out var pack) ? NamingJson.Copy(pack) : null;
    internal NamingPack DescribePackage(string id) => _packs.TryGetValue(id, out var pack) ||
        _world.AppliedPresets.TryGetValue(id, out pack) ? pack : null;
    internal bool HasProvider(string id) => _providers.ContainsKey(id);
    internal bool IsModDefault(string id) => _modDefaults.Contains(id);
    internal Dictionary<string, NamingPack> ActivePacks(WorldNaming world, IReadOnlyDictionary<string, NamingPack> catalog = null) =>
        Configuration.ResolveDependencies(catalog ?? _packs, world, _defaultPack, _modDefaults, new List<string>());

    internal string PickHumanCulturePack(WorldNaming world, ulong seed)
    {
        var eligible = ActivePacks(world).Values.Where(pack => pack.HumanCultureDefault && !pack.Supplemental)
            .Select(pack => pack.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        return eligible.Length == 0 ? null : eligible[(int)(new NamingRandom(seed).Next() * eligible.Length)];
    }

    internal Dictionary<string, NamingPack> PackageCatalog() => new(_packs, StringComparer.Ordinal);

    internal void ReplacePackageCatalog(Dictionary<string, NamingPack> catalog)
    {
        _packs = catalog;
        ClearResolved();
    }
    internal sealed class CultureTab(string id, string icon, string label, string description, int order)
    {
        internal readonly string Id = id, Icon = icon, Label = label, Description = description;
        internal readonly int Order = order;
    }

    internal IReadOnlyDictionary<string, CultureTab> CultureOnomastics => _cultureOnomastics;
    internal IReadOnlyDictionary<string, CultureNameGlyph> CultureGlyphs => _cultureGlyphs;
    internal string ExampleKind(string type) => _categoryFacts.TryGetValue(type, out var facts) ? facts.ExampleKind : null;
    internal bool WorldScoped(string type) => _categoryFacts.TryGetValue(type, out var facts) && facts.WorldScoped;
    internal bool ShareDefaultLayout(string type) => _categoryFacts.TryGetValue(type, out var facts) && facts.ShareDefaultLayout;

    internal sealed class CultureCondition(string field, string label, IReadOnlyDictionary<string, string> values, bool presence)
    {
        internal string Field { get; } = field;
        internal string Label { get; } = label;
        internal IReadOnlyDictionary<string, string> Values { get; } = values;
        internal bool Presence { get; } = presence;

        internal string Read(string value) => Presence ? (value == null ? "absent" : "present") : value;
    }

    /// <summary>Expose stable choices for a field. A later registration may add new IDs to that field.</summary>
    public void RegisterCultureCondition(string type, string field, string label, IReadOnlyDictionary<string, string> values) =>
        AddCondition(type, NamingCategoryExtension.ValueCondition(field, label, values));

    /// <summary>Expose whether the request carries a value for this field.</summary>
    public void RegisterCulturePresence(string type, string field, string label, string present, string absent) =>
        AddCondition(type, NamingCategoryExtension.PresenceCondition(field, label, present, absent));

    private void AddCondition(string type, CultureCondition condition)
    {
        if (type == null || !_types.TryGetValue(type, out var registered))
            throw new ArgumentException("Register a naming type and compatible field first.");
        if (!_conditions.TryGetValue(type, out var fields))
            fields = new(StringComparer.Ordinal);
        AddCondition(registered, fields, condition);
        _conditions[type] = fields;
    }

    private static void AddCondition(NameType type, Dictionary<string, CultureCondition> fields, CultureCondition condition)
    {
        if (condition.Field == null || !type.Fields.TryGetValue(condition.Field, out var field) ||
            !condition.Presence && field.Kind != InputKind.Text || string.IsNullOrWhiteSpace(condition.Label))
            throw new ArgumentException("Register a naming type and compatible field first.");
        if (fields.TryGetValue(condition.Field, out var existing))
        {
            if (existing.Presence || condition.Presence)
                throw new ArgumentException("A field already uses a presence or a value condition.");
            var values = new Dictionary<string, string>(existing.Values, StringComparer.Ordinal);
            foreach (var value in condition.Values)
                if (!values.TryAdd(value.Key, value.Value))
                    throw new ArgumentException("Duplicate condition value: " + value.Key);
            condition = new CultureCondition(condition.Field, existing.Label, values, false);
        }
        fields[condition.Field] = condition;
    }

    internal IReadOnlyDictionary<string, CultureCondition> Conditions(string type) =>
        _conditions.TryGetValue(type, out var fields) ? fields : EmptyConditions;

    private static readonly Dictionary<string, CultureCondition> EmptyConditions = new(StringComparer.Ordinal);

    internal bool Matches(ConditionalArrangement arrangement, NameRequest request) =>
        !arrangement.Disabled && MatchesExpression(arrangement, request);

    internal bool MatchesExpression(ConditionalArrangement arrangement, NameRequest request) =>
        CanMatch(arrangement, _types[request.Type]) &&
        arrangement.Expression.Evaluate(term => InputApplies(request.Type, term.Field, request.Inputs)
            ? MatchesCondition(request.Type, term.Field, term,
                request.Inputs.TryGetValue(term.Field, out var value) ? NameEngine.Value(value) : null)
            : null) == true;

    internal void ScopeInput(string type, string field) => _scopedInputs.Add((type, field));

    internal bool IsScopedInput(string type, string field) => _scopedInputs.Contains((type, field));

    internal bool InputApplies(string type, string field, IReadOnlyDictionary<string, object> inputs) =>
        !IsScopedInput(type, field) || inputs.TryGetValue(field, out var value) &&
        !string.IsNullOrWhiteSpace(NameEngine.Value(value));

    internal bool CanMatch(ConditionalArrangement arrangement, NameType type) =>
        arrangement.IsConfigured && arrangement.Expression.Leaves().All(term => ValidTerm(type, term));

    private bool ValidTerm(NameType type, ArrangementCondition term)
    {
        if (!type.Fields.TryGetValue(term.Field, out var field)) return false;
        if (term.Test == ArrangementTest.Registered) return Conditions(type.Id).ContainsKey(term.Field);
        if (!ConditionTests(field.Kind).Contains(term.Test)) return false;
        return term.Test is ArrangementTest.Present or ArrangementTest.Absent || field.Kind == InputKind.Text ||
            term.Values.All(value => ArrangementExamples.Input(field, value) != null);
    }

    private static readonly ArrangementTest[] TextTests = [ArrangementTest.Equal, ArrangementTest.Prefix,
        ArrangementTest.Suffix, ArrangementTest.Contains, ArrangementTest.Present, ArrangementTest.Absent];
    private static readonly ArrangementTest[] NumberTests = [ArrangementTest.Equal, ArrangementTest.Greater, ArrangementTest.AtLeast,
        ArrangementTest.Less, ArrangementTest.AtMost, ArrangementTest.Present, ArrangementTest.Absent];
    private static readonly ArrangementTest[] FlagTests = [ArrangementTest.Equal, ArrangementTest.Present, ArrangementTest.Absent];

    internal static IReadOnlyList<ArrangementTest> ConditionTests(InputKind kind) => kind switch
    {
        InputKind.Text => TextTests,
        InputKind.Integer => NumberTests,
        _ => FlagTests
    };

    internal bool MatchesCondition(string type, string field, ArrangementCondition condition, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) text = null;
        switch (condition.Test)
        {
            case ArrangementTest.Present: return text != null;
            case ArrangementTest.Absent: return text == null;
            case ArrangementTest.Equal: return text != null && condition.Values.Contains(text);
            case ArrangementTest.Prefix: return text != null &&
                condition.Values.Any(prefix => prefix.Length != 0 && text.StartsWith(prefix, StringComparison.Ordinal));
            case ArrangementTest.Suffix: return text != null &&
                condition.Values.Any(suffix => suffix.Length != 0 && text.EndsWith(suffix, StringComparison.Ordinal));
            case ArrangementTest.Contains: return text != null &&
                condition.Values.Any(value => value.Length != 0 && text.IndexOf(value, StringComparison.Ordinal) >= 0);
            case ArrangementTest.Registered:
                return Conditions(type).TryGetValue(field, out var registered) && registered.Read(text) is string value &&
                    registered.Values.ContainsKey(value) && condition.Values.Contains(value);
            default: return Number(text) is int actual && condition.Values.Any(value => Number(value) is int limit &&
                (condition.Test switch
                {
                    ArrangementTest.Greater => actual > limit,
                    ArrangementTest.AtLeast => actual >= limit,
                    ArrangementTest.Less => actual < limit,
                    ArrangementTest.AtMost => actual <= limit,
                    _ => false
                }));
        }
    }

    internal static int? Number(string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : null;

    /// <summary>Register the category and its editing declarations together.</summary>
    public void RegisterCategory(NamingCategory category)
    {
        if (category == null) throw new ArgumentNullException(nameof(category));
        var type = ExtendType(category.Type, category);
        var conditions = ValidateTools(type, category);

        RegisterType(type, category.Defaults);
        if (category.CultureIcon != null)
            SetCultureTab(type, category.CultureIcon, category.Description, category.TabId, category.TabLabel, category.TabOrder);
        if (category.ExampleKind != null || category.WorldScoped || category.ShareDefaultLayout)
            _categoryFacts.Add(type.Id, (category.ExampleKind, category.WorldScoped, category.ShareDefaultLayout));
        if (category.PrepareContext != null) RegisterContext(type.Id, category.PrepareContext);
        ApplyTools(type.Id, category, conditions);
    }

    /// <summary>Add inputs, conditions and glyphs to a registered category without replacing its rules or editor tab.</summary>
    public void ExtendCategory(NamingCategoryExtension extension)
    {
        if (extension == null) throw new ArgumentNullException(nameof(extension));
        if (!_types.TryGetValue(extension.TypeId, out var existing))
            throw new ArgumentException("Register the naming category first.", nameof(extension));

        var type = ExtendType(existing, extension);
        var conditions = ValidateTools(type, extension);
        _types[type.Id] = type;
        ApplyTools(type.Id, extension, conditions);
        ClearResolved();
    }

    private static NameType ExtendType(NameType type, NamingCategoryExtension extension) =>
        extension.Inputs.Count == 0 ? type : new NameType(type.Id, type.Label, type.Fields.Values.Concat(extension.Inputs),
            type.MinLength, type.MaxLength, type.FixedParts);

    private Dictionary<string, CultureCondition> ValidateTools(NameType type, NamingCategoryExtension extension)
    {
        var conditions = new Dictionary<string, CultureCondition>(Conditions(type.Id), StringComparer.Ordinal);
        foreach (var condition in extension.Conditions) AddCondition(type, conditions, condition);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var codes = new HashSet<char>();
        foreach (var glyph in extension.Glyphs)
        {
            ValidateGlyph(type, glyph);
            if (!ids.Add(glyph.Id) || !codes.Add(glyph.ShortCode))
                throw new ArgumentException("Duplicate glyph ID or short code: " + glyph.Id);
        }
        return conditions;
    }

    private void ApplyTools(string type, NamingCategoryExtension extension, Dictionary<string, CultureCondition> conditions)
    {
        if (conditions.Count != 0) _conditions[type] = conditions;
        foreach (var glyph in extension.Glyphs) AddGlyph(glyph);
        Add(_examples, extension.PrepareExample);
        Add(_conditionIcons, extension.ConditionIcon);

        void Add<T>(Dictionary<string, List<T>> registry, T reader) where T : Delegate
        {
            if (reader == null) return;
            if (!registry.TryGetValue(type, out var readers)) registry[type] = readers = new();
            readers.Add(reader);
        }
    }

    internal void PrepareExample(string type, Dictionary<string, object> inputs)
    {
        if (_examples.TryGetValue(type, out var callbacks))
            foreach (var callback in callbacks) callback(inputs);
    }

    internal string ConditionIcon(string type, string field, string value) =>
        _conditionIcons.TryGetValue(type, out var readers)
            ? readers.Select(read => read(field, value)).FirstOrDefault(path => !string.IsNullOrEmpty(path))
            : null;

    public void RegisterType(NameType type, NamingPack defaults = null)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        if (_types.ContainsKey(type.Id)) throw new ArgumentException("Duplicate type: " + type.Id, nameof(type));
        if (defaults != null)
        {
            if (!defaults.Supplemental || defaults.Rules.Keys.Concat(defaults.Arrangements.Keys).Any(id => id != type.Id))
                throw new ArgumentException("Type defaults must be a supplemental pack for this type.", nameof(defaults));
            RegisterDefaultPack(defaults);
        }
        _types.Add(type.Id, type);
        ClearResolved();
    }

    /// <summary>Derive related naming fields from the supplied inputs. Return false for an impossible combination.</summary>
    /// <remarks>The callback receives a copy and should be deterministic and have no side effects.</remarks>
    public void RegisterContext(string type, Func<Dictionary<string, object>, bool> prepare)
    {
        if (type == null || !_types.ContainsKey(type)) throw new ArgumentException("Register the naming type first.", nameof(type));
        _contexts.Add(type, prepare ?? throw new ArgumentNullException(nameof(prepare)));
    }

    internal NameRequest PrepareContext(NameRequest request) => PrepareContext(request, out _);

    internal NameRequest PrepareContext(NameRequest request, out (GenerationStatus Status, string Diagnostic) failure)
    {
        failure = default;
        if (request.ContextOwner == this || request.Type == null || !_contexts.TryGetValue(request.Type, out var prepare)) return request;
        var inputs = new Dictionary<string, object>(request.Inputs, StringComparer.Ordinal);
        try
        {
            if (prepare(inputs)) return request.WithPreparedInputs(this, inputs);
            failure = (GenerationStatus.InvalidInput, "context:" + request.Type);
        }
        catch (Exception error)
        {
            failure = (GenerationStatus.ContextError, "context-exception:" + request.Type + "/" + error.GetType().Name);
        }
        return null;
    }

    internal bool HasContext(string type) => _contexts.ContainsKey(type);

    /// <summary>Add a registered type to the original culture onomastics editor.</summary>
    public void RegisterCultureOnomastics(string type, string icon = "ui/Icons/iconBooks", string description = null)
    {
        if (type == null || !_types.TryGetValue(type, out var registered))
            throw new ArgumentException("Register the naming type first.", nameof(type));
        SetCultureTab(registered, icon, description);
    }

    private void SetCultureTab(NameType type, string icon, string description, string id = null,
        string label = null, int order = int.MaxValue) =>
        _cultureOnomastics[type.Id] = new CultureTab(id, string.IsNullOrWhiteSpace(icon) ? "ui/Icons/iconBooks" : icon,
            label ?? type.Label, description ?? "编辑本文化的" + type.Label + "命名规则。", order);

    /// <summary>Register a native glyph that reads a field or draws from poolPrefix followed by the field value.</summary>
    public void RegisterCultureGlyph(string type, string id, char shortCode, string field, string label, string icon,
        string poolPrefix = null, string description = null, string fallbackPool = null)
    {
        if (type == null || !_types.TryGetValue(type, out var registered))
            throw new ArgumentException("Register the naming type first.", nameof(type));
        var glyph = new CultureNameGlyph(type, id, shortCode, field, label, icon, poolPrefix,
            description ?? "取当前对象的" + label + "。", fallbackPool);
        ValidateGlyph(registered, glyph);
        AddGlyph(glyph);
    }

    private void ValidateGlyph(NameType type, CultureNameGlyph glyph)
    {
        if (glyph.Field == null || !type.Fields.TryGetValue(glyph.Field, out var input))
            throw new ArgumentException("A glyph needs a declared input field.");
        if (!(glyph.PublishedLocalId ? Ids.Local(glyph.Id?.Replace('_', '-')) : Ids.Qualified(glyph.Id)) ||
            glyph.ShortCode is < '\uE000' or > '\uF8FF' ||
            string.IsNullOrWhiteSpace(glyph.Label) || string.IsNullOrWhiteSpace(glyph.Icon))
            throw new ArgumentException("A glyph needs a namespaced ID, stable private-use short code, label and icon.");
        if (glyph.PoolPrefix != null && (!Ids.Qualified(glyph.PoolPrefix) || input.Kind != InputKind.Text))
            throw new ArgumentException("A dynamic pool needs a qualified prefix and a text input field.");
        if (glyph.FallbackPool != null && !Ids.Qualified(glyph.FallbackPool))
            throw new ArgumentException("Use a qualified fallback pool ID.");
        if (_cultureGlyphs.ContainsKey(glyph.Id) || _cultureGlyphs.Values.Any(previous => previous.ShortCode == glyph.ShortCode) ||
            _glyphAvailable?.Invoke(glyph) == false)
            throw new ArgumentException("Duplicate glyph ID or short code: " + glyph.Id);
    }

    private void AddGlyph(CultureNameGlyph glyph)
    {
        _registerGlyph?.Invoke(glyph);
        _cultureGlyphs.Add(glyph.Id, glyph);
    }

    internal void AttachGlyphCatalog(Action<CultureNameGlyph> registrar, Func<CultureNameGlyph, bool> available)
    {
        foreach (var glyph in _cultureGlyphs.Values) registrar(glyph);
        _registerGlyph = registrar;
        _glyphAvailable = available;
    }

    public void RegisterDefaultPack(NamingPack pack)
    {
        if (pack == null || !pack.Supplemental)
            throw new ArgumentException("A mod default pack must be supplemental.", nameof(pack));
        InstallPack(pack);
        _modDefaults.Add(pack.Id);
    }

    public void RegisterProvider(string id, Func<NameRequest, IEnumerable<Word>> provider)
    {
        if (!Ids.Qualified(id)) throw new ArgumentException("Use a namespace:provider ID.", nameof(id));
        _providers.Add(id, provider ?? throw new ArgumentNullException(nameof(provider)));
    }

    public void InstallPack(NamingPack pack)
    {
        var copy = NamingJson.Copy(pack);
        Configuration.Validate(copy);
        if (_packs.ContainsKey(copy.Id)) throw new ArgumentException("Duplicate pack: " + copy.Id, nameof(pack));
        _packs.Add(copy.Id, copy);
        ClearResolved();
    }

    public void UninstallPack(string id)
    {
        if (id != _defaultPack && !_modDefaults.Contains(id) && _packs.Remove(id)) ClearResolved();
    }

    public WorldNaming GetWorld() => NamingJson.Copy(_world);

    public void SetWorld(WorldNaming world)
    {
        var copy = CopyWorld(world);
        _world = copy;
        ClearResolved();
    }

    internal void SetCulture(long id, CultureNaming settings)
    {
        _world = _world.WithCulture(id, settings);
        InvalidateResolved(key => key.Culture == id);
    }

    internal void SetLanguage(long id, LanguageNaming settings, long? parent)
    {
        var previousParents = _world.LanguageParents;
        _world = _world.WithLanguage(id, settings, parent);
        bool parentChanged = !ReferenceEquals(previousParents, _world.LanguageParents);
        InvalidateResolved(key => key.Language == id || parentChanged && key.Language.HasValue &&
            _world.Languages.TryGetValue(key.Language.Value, out var other) && other.BiasOrigin == id);
    }

    public NameResult Generate(NameRequest request, Func<string, bool> accept = null) => Run(request, _world, accept);

    internal NameResult Generate(NameRequest request, Func<string, bool> accept, NamingTrace trace) =>
        Run(request, _world, accept, trace: trace);

    public NameResult Preview(NameRequest request, WorldNaming draft = null) =>
        Run(request, draft == null ? _world : CopyWorld(draft), null);

    internal NameResult GenerateForWorld(NameRequest request, WorldNaming world, Func<string, bool> accept, EffectiveNames effective) =>
        Run(request, world, accept, effective);

    internal NameResult EvaluateArrangements(NameRequest request, EffectiveNames effective,
        Func<NameType, NameRequest, IEnumerable<NamingCandidate>> candidates,
        Func<string, bool> accept = null, NamingTrace trace = null) =>
        Run(request, _world, accept, effective, trace, candidates);

    internal IEnumerable<NameResult> PreviewSamples(NameRequest sample, WorldNaming draft, int draws)
    {
        var effective = Inspect(sample, draft);
        for (int index = 0; index < draws; index++)
        {
            var request = index == 0 ? sample : sample.WithSeed(unchecked(sample.Seed + (ulong)index));
            yield return Run(request, draft, null, effective);
        }
    }

    internal EffectiveNames Inspect(NameRequest request, WorldNaming draft) =>
        _types.TryGetValue(request.Type, out var type) ? Resolve(type, request, draft ?? _world) : null;

    internal NamingPack MakePortablePack(string id, long? culture, long? language, WorldNaming draft,
        IReadOnlyDictionary<string, (CardNaming[] Cards, string[] Pools)> arrangements,
        IReadOnlyList<NameType> types = null, bool baseVocabulary = false)
    {
        var pack = new NamingPack
        {
            Format = 1, Id = id, Label = id, UseAsLanguageDefault = baseVocabulary || arrangements?.Count > 0
        };
        var words = new Dictionary<string, Word>(StringComparer.Ordinal);
        EffectiveNames languagePools = null;
        draft ??= _world;
        foreach (var type in types ?? Types)
        {
            var effective = baseVocabulary
                ? Configuration.Resolve(type, culture, language, draft, _packs, _defaultPack, _modDefaults,
                    baseVocabulary: true, arrangementCodec: ArrangementCodec)
                : Inspect(new NameRequest(type.Id, 0, culture, language), draft);
            languagePools ??= effective;
            var rules = effective.Rules.ToList();
            if (effective.Arrangements.Count > 0)
                pack.Arrangements[type.Id] = effective.Arrangements.Select(item => NamingJson.Copy(item)).ToList();
            if (arrangements != null && arrangements.TryGetValue(type.Id, out var arrangement))
            {
                foreach (var cards in arrangement.Cards)
                {
                    var targets = cards.SourceRule == null ? effective.Rules :
                        effective.AvailableRules.Where(rule => rule.Id == cards.SourceRule).ToArray();
                    foreach (var rule in targets)
                    {
                        var packaged = NamingJson.Copy(rule);
                        packaged.PresetOnly = false;
                        packaged.Inheritance = cards.Inheritance;
                        packaged.NativeCards = NamingJson.Copy(cards);
                        packaged.NativeCards.SourceRule = null;
                        int index = rules.IndexOf(rule);
                        if (index >= 0) rules[index] = packaged;
                        else rules.Add(packaged);
                    }
                }
                foreach (var pool in arrangement.Pools) CopyPool(effective, pool);
            }
            if (rules.Count == 0) continue;
            pack.Rules[type.Id] = rules;
            if (rules.Any(rule => rule.NativeCards != null)) pack.UseAsLanguageDefault = true;
            foreach (var pool in effective.Pools)
                CopyPool(effective, pool.Key);
        }
        if (languagePools != null)
        {
            LanguageNaming settings = null;
            if (language.HasValue) draft.Languages.TryGetValue(language.Value, out settings);
            foreach (var alias in languagePools.PoolAliases)
            {
                if (alias.Value == null || !languagePools.PoolIds.Contains(alias.Value)) continue;
                if (pack.UseAsLanguageDefault && !pack.Pools.ContainsKey(alias.Value)) continue;
                CopyPool(languagePools, alias.Value);
                pack.PoolAliases.Add(alias.Key, alias.Value);
            }
            var groups = Configuration.WordGroupDefinitions(draft, settings, _packs, _defaultPack, _modDefaults, culture);
            foreach (var definition in groups.OrderBy(pair => pair.Id, StringComparer.Ordinal))
            {
                var group = definition.Group;
                if (!languagePools.PoolIds.Contains(group.Pool)) continue;
                if (pack.UseAsLanguageDefault && !pack.Pools.ContainsKey(group.Pool)) continue;
                CopyPool(languagePools, group.Pool);
                var members = group.Words.Where(pack.Pools[group.Pool].ContainsKey).Distinct(StringComparer.Ordinal).ToList();
                if (members.Count == 0) continue;
                string name = definition.Id.Substring(definition.Id.IndexOf('/') + 1);
                string key = name;
                for (int suffix = 2; pack.WordGroups.ContainsKey(key); suffix++)
                    key = name.Substring(0, Math.Min(name.Length, 53)) + "-" +
                        suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
                pack.WordGroups.Add(key, new WordGroup { Pool = group.Pool, Words = members, Landforms = group.Landforms });
            }
            foreach (var alias in languagePools.PoolMigrations.Ids)
                if (pack.Pools.ContainsKey(alias.Value)) pack.PoolRenames.Add(alias.Key, alias.Value);
        }
        pack.Words = words.Values.OrderBy(word => word.Id, StringComparer.Ordinal).ToList();
        return NamingJson.Copy(pack);

        void CopyPool(EffectiveNames effective, string pool)
        {
            pool = effective.PoolId(pool);
            if (pack.Pools.ContainsKey(pool)) return;
            var candidates = effective.GetPool(pool);
            if (baseVocabulary && candidates.Length == 0) return;
            double scale = candidates.Length == 0 ? 1 : Math.Max(1, candidates.Max(word => word.Weight) / 10000);
            pack.Pools[pool] = candidates.ToDictionary(word => word.Id, word => word.Weight / scale);
            foreach (var word in candidates)
                words[word.Id] = new Word { Id = word.Id, Text = word.Text, Chinese = word.Chinese };
            if (effective.PoolLabels.TryGetValue(pool, out string label)) pack.PoolLabels[pool] = label;
            var credits = effective.GetPoolCredits(pool);
            if (credits.Count != 0) pack.PoolCredits[pool] = credits.ToList();
        }
    }

    private NameResult Run(NameRequest request, WorldNaming world, Func<string, bool> accept,
        EffectiveNames effective = null, NamingTrace trace = null,
        Func<NameType, NameRequest, IEnumerable<NamingCandidate>> selection = null)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.Type == null || !_types.TryGetValue(request.Type, out var type))
            return new NameResult(GenerationStatus.UnknownType, null, null, new[] { "type:" + request.Type });
        request = PrepareContext(request, out var failure);
        if (request == null)
            return new NameResult(failure.Status, null, null, new[] { failure.Diagnostic });
        if (NameEngine.InputError(type, request) is string error)
            return new NameResult(GenerationStatus.InvalidInput, null, null, new[] { error });
        effective ??= Resolve(type, request, world);
        trace ??= new NamingTrace();
        if (selection != null)
            return NameEngine.Generate(type, request, effective, _providers, accept, trace, selection(type, request));
        var rules = effective.Rules.Where(rule => NameEngine.Matches(rule, request)).ToArray();
        var candidates = GameCandidates?.Invoke(type, request, world, effective, rules) ??
            rules.Select((rule, index) => NameEngine.RuleCandidate(rule, index, rules.Length));
        return NameEngine.Generate(type, request, effective, _providers, accept, trace, candidates);
    }

    private EffectiveNames Resolve(NameType type, NameRequest request, WorldNaming world)
    {
        if (world != _world) return Configuration.Resolve(type, request.Culture, request.Language, world,
            _packs, _defaultPack, _modDefaults, arrangementCodec: ArrangementCodec);
        var key = (type.Id, request.Culture, request.Language);
        if (_resolved.TryGetValue(key, out var cached))
        {
            if (cached.Node != _recent.Last)
            {
                _recent.Remove(cached.Node);
                _recent.AddLast(cached.Node);
            }
            return cached.Names;
        }
        var effective = Configuration.Resolve(type, request.Culture, request.Language, world, _packs, _defaultPack, _modDefaults,
            arrangementCodec: ArrangementCodec);
        if (_resolved.Count >= ResolutionCacheLimit)
        {
            _resolved.Remove(_recent.First.Value);
            _recent.RemoveFirst();
        }
        _resolved.Add(key, (_recent.AddLast(key), effective));
        return effective;
    }

    private void ClearResolved()
    {
        _resolved.Clear();
        _recent.Clear();
    }

    private void InvalidateResolved(Func<(string Type, long? Culture, long? Language), bool> matches)
    {
        for (var node = _recent.First; node != null;)
        {
            var next = node.Next;
            if (matches(node.Value))
            {
                _resolved.Remove(node.Value);
                _recent.Remove(node);
            }
            node = next;
        }
    }

    private static WorldNaming CopyWorld(WorldNaming world)
    {
        var copy = NamingJson.Copy(world);
        if (copy == null || copy.Format != 1) throw new FormatException("Unsupported world naming format.");
        return copy;
    }

    internal WorldNaming ActiveWorld => _world;

    internal bool EvolveCulture(CultureNaming culture, WorldNaming world, ulong seed, string landform = null,
        string species = null) =>
        Configuration.EvolveCulture(culture, world, _types.Values, _packs, _defaultPack, _modDefaults, seed, landform, species);

    internal bool EvolveLanguage(LanguageNaming language, WorldNaming world, ulong seed, bool split,
        ISet<string> preferredPools = null, long? culture = null, string landform = null) =>
        Configuration.EvolveLanguage(language, world, _packs, _defaultPack, _modDefaults, seed, split, preferredPools, culture, landform);

    internal Dictionary<string, string[]> WordGroups(WorldNaming world, LanguageNaming language, long? culture = null) =>
        Configuration.AvailableWordGroups(world, language, _packs, _defaultPack, _modDefaults, culture);
}

// The game adapter maps the current save to this service. Mods use the same service as the editor.
public static class NamingApi
{
    private static NamingService _current = new();
    internal static Action SelectWorld { get; set; }
    public static NamingService Current
    {
        get { SelectWorld?.Invoke(); return _current; }
        internal set => _current = value;
    }
}
