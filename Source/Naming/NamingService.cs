using System;
using System.Collections.Generic;
using System.Linq;

namespace Chinese_Name.Naming;

public sealed class NamingService
{
    private readonly Dictionary<string, NameType> _types = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Icon, string Description)> _cultureOnomastics = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CultureNameGlyph> _cultureGlyphs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NamingPack> _packs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<NameRequest, IEnumerable<Word>>> _providers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _modDefaults = new(StringComparer.Ordinal);
    private readonly Dictionary<(string, long?, long?), EffectiveNames> _resolved = new();
    private readonly string _defaultPack;
    private WorldNaming _world = new();
    internal Func<NameType, NameRequest, WorldNaming, EffectiveNames, Func<string, bool>, NameResult> GameRule { get; set; }
    internal Action<CultureNameGlyph> CultureGlyphRegistrar { get; set; }

    public NamingService(string defaultPack = null) => _defaultPack = defaultPack;

    public string DefaultPack => _defaultPack;
    public IReadOnlyList<NameType> Types => _types.Values.OrderBy(type => type.Id, StringComparer.Ordinal).ToArray();
    public IReadOnlyList<string> Packages => _packs.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray();
    public NamingPack GetPackage(string id) => _packs.TryGetValue(id, out var pack) ? NamingJson.Copy(pack) : null;
    internal NamingPack DescribePackage(string id) => _packs.TryGetValue(id, out var pack) ? pack : null;
    internal bool IsModDefault(string id) => _modDefaults.Contains(id);
    internal Dictionary<string, NamingPack> ActivePacks(WorldNaming world) =>
        Configuration.ResolveDependencies(_packs, world, _defaultPack, _modDefaults, new List<string>());
    internal IReadOnlyDictionary<string, (string Icon, string Description)> CultureOnomastics => _cultureOnomastics;

    public void RegisterType(NameType type, NamingPack defaults = null)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        if (_types.ContainsKey(type.Id)) throw new ArgumentException("Duplicate type: " + type.Id, nameof(type));
        if (defaults != null)
        {
            if (!defaults.Supplemental || defaults.Rules.Keys.Any(id => id != type.Id))
                throw new ArgumentException("Type defaults must be a supplemental pack for this type.", nameof(defaults));
            RegisterDefaultPack(defaults);
        }
        _types.Add(type.Id, type);
        _resolved.Clear();
    }

    /// <summary>Add a registered type to the original culture onomastics editor.</summary>
    public void RegisterCultureOnomastics(string type, string icon = "ui/Icons/iconBooks", string description = null)
    {
        if (type == null || !_types.TryGetValue(type, out var registered))
            throw new ArgumentException("Register the naming type first.", nameof(type));
        _cultureOnomastics[type] = (string.IsNullOrWhiteSpace(icon) ? "ui/Icons/iconBooks" : icon,
            description ?? "编辑本文化的" + registered.Label + "命名规则。");
    }

    /// <summary>Register a native glyph that reads a field or draws from poolPrefix followed by the field value.</summary>
    public void RegisterCultureGlyph(string type, string id, char shortCode, string field, string label, string icon,
        string poolPrefix = null, string description = null, string fallbackPool = null)
    {
        if (type == null || !_types.TryGetValue(type, out var registered) ||
            field == null || !registered.Fields.TryGetValue(field, out var input))
            throw new ArgumentException("Register the naming type and input field first.", nameof(field));
        if (!Ids.Qualified(id) || shortCode is < '\uE000' or > '\uF8FF' ||
            string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(icon))
            throw new ArgumentException("A glyph needs a namespaced ID, stable private-use short code, label and icon.", nameof(id));
        if (poolPrefix != null && (!Ids.Qualified(poolPrefix) || input.Kind != InputKind.Text))
            throw new ArgumentException("A dynamic pool needs a qualified prefix and a text input field.", nameof(poolPrefix));
        if (fallbackPool != null && !Ids.Qualified(fallbackPool))
            throw new ArgumentException("Use a qualified fallback pool ID.", nameof(fallbackPool));
        if (_cultureGlyphs.ContainsKey(id) || _cultureGlyphs.Values.Any(glyph => glyph.ShortCode == shortCode))
            throw new ArgumentException("Duplicate glyph ID or short code: " + id, nameof(id));

        var glyph = new CultureNameGlyph(type, id, shortCode, field, label, icon, poolPrefix,
            description ?? "取当前对象的" + label + "。", fallbackPool);
        CultureGlyphRegistrar?.Invoke(glyph);
        _cultureGlyphs.Add(id, glyph);
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
        _resolved.Clear();
    }

    public void UninstallPack(string id)
    {
        if (id != _defaultPack && !_modDefaults.Contains(id) && _packs.Remove(id)) _resolved.Clear();
    }

    public WorldNaming GetWorld() => NamingJson.Copy(_world);

    public void SetWorld(WorldNaming world)
    {
        var copy = CopyWorld(world);
        _world = copy;
        _resolved.Clear();
    }

    public NameResult Generate(NameRequest request, Func<string, bool> accept = null) => Run(request, _world, accept);

    public NameResult Preview(NameRequest request, WorldNaming draft = null) =>
        Run(request, draft == null ? _world : CopyWorld(draft), null);

    internal IEnumerable<NameResult> PreviewRule(NameRequest sample, WorldNaming draft, string rule, int draws)
    {
        var effective = Inspect(sample, draft);
        for (int index = 0; index < draws; index++)
        {
            var request = index == 0 ? sample : new NameRequest(sample.Type, unchecked(sample.Seed + (ulong)index),
                sample.Culture, sample.Language, sample.Inputs, sample.FixedParts, sample.Writing);
            yield return Run(request, draft, null, rule, effective);
        }
    }

    internal EffectiveNames Inspect(NameRequest request, WorldNaming draft) =>
        _types.TryGetValue(request.Type, out var type) ? Resolve(type, request, draft ?? _world) : null;

    internal NamingPack MakePortablePack(string id, long? culture, long? language, WorldNaming draft,
        IReadOnlyDictionary<string, (CardNaming Cards, string[] Pools)> arrangements)
    {
        var pack = new NamingPack { Format = 1, Id = id, Label = id, UseAsLanguageDefault = arrangements?.Count > 0 };
        var words = new Dictionary<string, Word>(StringComparer.Ordinal);
        EffectiveNames languagePools = null;
        foreach (var type in Types)
        {
            var effective = Inspect(new NameRequest(type.Id, 0, culture, language), draft);
            languagePools ??= effective;
            var rules = effective.Rules.ToList();
            if (arrangements != null && arrangements.TryGetValue(type.Id, out var arrangement))
            {
                if (arrangement.Cards is { } cards)
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
            draft ??= _world;
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
                pack.WordGroups.Add(key, new WordGroup { Pool = group.Pool, Words = members });
            }
        }
        pack.Words = words.Values.OrderBy(word => word.Id, StringComparer.Ordinal).ToList();
        return NamingJson.Copy(pack);

        void CopyPool(EffectiveNames effective, string pool)
        {
            if (pack.Pools.ContainsKey(pool)) return;
            var candidates = effective.GetPool(pool);
            double scale = candidates.Length == 0 ? 1 : Math.Max(1, candidates.Max(word => word.Weight) / 10000);
            pack.Pools[pool] = candidates.ToDictionary(word => word.Id, word => word.Weight / scale);
            foreach (var word in candidates)
                words[word.Id] = new Word { Id = word.Id, Text = word.Text, Chinese = word.Chinese };
            if (effective.PoolLabels.TryGetValue(pool, out string label)) pack.PoolLabels[pool] = label;
        }
    }

    private NameResult Run(NameRequest request, WorldNaming world, Func<string, bool> accept, string selectedRule = null,
        EffectiveNames effective = null)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.Type == null || !_types.TryGetValue(request.Type, out var type))
            return new NameResult(GenerationStatus.UnknownType, null, null, new[] { "type:" + request.Type });
        if (NameEngine.InputError(type, request) is string error)
            return new NameResult(GenerationStatus.InvalidInput, null, null, new[] { error });
        effective ??= Resolve(type, request, world);
        var game = GameRule?.Invoke(type, request, world, effective, accept);
        if (game?.Success == true) return game;
        var generated = NameEngine.Generate(type, request, effective, _providers, accept, selectedRule);
        if (game == null) return generated;
        var diagnostics = game.Diagnostics.Append("中文规则回退").Concat(generated.Diagnostics).Distinct();
        return new NameResult(generated.Status, generated.Text, generated.Parts.ToDictionary(pair => pair.Key, pair => pair.Value),
            diagnostics, generated.PartOrder, generated.Sources.ToDictionary(pair => pair.Key, pair => pair.Value), generated.Separator);
    }

    private EffectiveNames Resolve(NameType type, NameRequest request, WorldNaming world)
    {
        if (world != _world) return Configuration.Resolve(type, request.Culture, request.Language, world,
            _packs, _defaultPack, _modDefaults);
        var key = (type.Id, request.Culture, request.Language);
        if (_resolved.TryGetValue(key, out var effective)) return effective;
        effective = Configuration.Resolve(type, request.Culture, request.Language, world, _packs, _defaultPack, _modDefaults);
        if (_resolved.Count >= 512) _resolved.Clear();
        _resolved.Add(key, effective);
        return effective;
    }

    private static WorldNaming CopyWorld(WorldNaming world)
    {
        var copy = NamingJson.Copy(world);
        if (copy == null || copy.Format != 1) throw new FormatException("Unsupported world naming format.");
        return copy;
    }

    internal WorldNaming ActiveWorld => _world;

    internal bool EvolveCulture(CultureNaming culture, WorldNaming world, ulong seed) =>
        Configuration.EvolveCulture(culture, world, _types.Values, _packs, _defaultPack, _modDefaults, seed);

    internal bool EvolveLanguage(LanguageNaming language, WorldNaming world, ulong seed, bool split,
        ISet<string> preferredPools = null, long? culture = null) =>
        Configuration.EvolveLanguage(language, world, _packs, _defaultPack, _modDefaults, seed, split, preferredPools, culture);

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
