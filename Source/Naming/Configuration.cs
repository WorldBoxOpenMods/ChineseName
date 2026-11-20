using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Chinese_Name.Naming;

public sealed class NamingPack
{
    public const int CurrentFormat = 1;
    public int Format { get; set; }
    public string Id { get; set; }
    public string Label { get; set; }
    public int Version { get; set; } = 1;
    public int Priority { get; set; }
    public bool Supplemental { get; set; }
    public bool UseAsLanguageDefault { get; set; }
    public bool FallbackPools { get; set; }
    public Dictionary<string, int> Requires { get; set; } = new();
    public List<string> CultureBases { get; set; } = new();
    public List<string> LanguageBases { get; set; } = new();
    public Dictionary<string, List<Rule>> Rules { get; set; } = new();
    public List<Word> Words { get; set; } = new();
    public Dictionary<string, Dictionary<string, double>> Pools { get; set; } = new();
    public List<PoolPatch> PoolPatches { get; set; } = new();
    public Dictionary<string, RuleEdit> RuleEdits { get; set; } = new();
    public Dictionary<string, PoolEdit> LanguageEdits { get; set; } = new();
    public Dictionary<string, CultureVariant> CultureVariants { get; set; } = new();
    public Dictionary<string, WordGroup> WordGroups { get; set; } = new();
    public Dictionary<string, string> PoolLabels { get; set; } = new();
    public Dictionary<string, string> PoolAliases { get; set; } = new();
}

public enum PoolOperation { Append, Replace, Exclude }

public sealed class PoolPatch
{
    public string Pool { get; set; }
    public PoolOperation Operation { get; set; }
    public Dictionary<string, double> Words { get; set; } = new();
}

public sealed class WorldNaming
{
    public int Format { get; set; } = 1;
    public string CulturePack { get; set; }
    public string LanguagePack { get; set; }
    public List<string> EnabledPacks { get; set; } = new();
    [JsonProperty]
    internal List<string> InitializedBundledPacks { get; set; }
    public Dictionary<string, int> PackPriorities { get; set; } = new();
    public Dictionary<long, CultureNaming> Cultures { get; set; } = new();
    public Dictionary<long, LanguageNaming> Languages { get; set; } = new();
    public Dictionary<long, long> LanguageParents { get; set; } = new();
    [JsonProperty]
    internal Dictionary<string, NamingPack> AppliedPresets { get; set; } = new();
    public bool UseSavedVanillaCards { get; set; }

    internal WorldNaming WithoutAppliedPresets()
    {
        var draft = (WorldNaming)MemberwiseClone();
        draft.AppliedPresets = null;
        return draft;
    }
}

internal sealed class PlayerNamingPreset
{
    public int Format { get; set; }
    public NamingPack Snapshot { get; set; }
}

public sealed class CultureNaming
{
    public string Pack { get; set; }
    public Dictionary<string, RuleEdit> Rules { get; set; } = new();
    public Dictionary<string, CardNaming> Cards { get; set; } = new();
    public Dictionary<string, string> Presets { get; set; } = new();
    public long? Parent { get; set; }
    public Dictionary<string, string> Variants { get; set; } = new();
}

public sealed class CardNaming
{
    public bool Enabled { get; set; }
    public Dictionary<string, string> Pools { get; set; } = new();
    public string SurnameGroup { get; set; }
    public SurnameOrder Inheritance { get; set; }
    public string SourceRule { get; set; }
    public bool ExplicitSeparators { get; set; }
    public Dictionary<string, string> Roles { get; set; } = new();
    // Categories without a vanilla culture slot store their applied cards with the culture settings.
    public string Template { get; set; }
    public string DraftTemplate { get; set; }
}

public sealed class RuleEdit
{
    public SurnameOrder? Inheritance { get; set; }
    public List<string> Order { get; set; }
    public Dictionary<string, Part> Parts { get; set; } = new();
    public Dictionary<string, PartEdit> Fields { get; set; } = new();
    public Dictionary<string, string> When { get; set; }
    public NameWriting? Writing { get; set; }
    public string ChineseSeparator { get; set; }
    public string OriginalSeparator { get; set; }
}

public sealed class PartEdit
{
    public bool? Optional { get; set; }
    public double? Chance { get; set; }
    public Part Operation { get; set; }
}

public sealed class LanguageNaming
{
    public string Pack { get; set; }
    public Dictionary<string, PoolEdit> Pools { get; set; } = new();
    public Dictionary<string, string> Groups { get; set; } = new();
    public Dictionary<string, Dictionary<string, double>> GroupPreferences { get; set; } = new();
    public long? BiasOrigin { get; set; }

    internal IEnumerable<KeyValuePair<string, double>> PreferencesFor(string pool)
    {
        if (GroupPreferences.TryGetValue(pool, out var preferences)) return preferences;
        return Groups.TryGetValue(pool, out string group) && group != null
            ? new[] { new KeyValuePair<string, double>(group, 1) }
            : Array.Empty<KeyValuePair<string, double>>();
    }
}

public sealed class PoolEdit
{
    public List<string> ExcludeIds { get; set; } = new();
    public List<string> ExcludeText { get; set; } = new();
    public Dictionary<string, double> Bias { get; set; } = new();
    public string Group { get; set; }
}

internal sealed class EffectiveNames
{
    internal Rule[] Rules { get; set; }
    internal Rule[] AvailableRules { get; set; }
    internal Rule SelectedPreset { get; set; }
    internal Dictionary<string, Word[]> Pools { get; set; }
    internal string[] Diagnostics { get; set; }
    internal string[] PoolIds { get; set; }
    internal Dictionary<string, string> PoolLabels { get; set; }
    internal Dictionary<string, string> PoolAliases { get; set; }
    internal Func<string, Word[]> ReadAdditionalPool { get; set; }

    internal Word[] GetPool(string id) => Pools.TryGetValue(id, out var words) ? words : ReadAdditionalPool(id);

    internal IEnumerable<string> ReferencedPools(Part part)
    {
        if (part == null) yield break;
        if (part.FallbackPool != null) yield return part.FallbackPool;
        if (part.Pool == null) yield break;
        if (part.PoolInput == null) { yield return part.Pool; yield break; }
        string prefix = NameEngine.PoolPrefix(part.Pool);
        foreach (var id in PoolIds.Where(id => id.StartsWith(prefix, StringComparison.Ordinal))) yield return id;
    }

    internal string ResolvePoolToken(string token, out bool ambiguous)
    {
        ambiguous = false;
        if (token.IndexOf('/') >= 0)
        {
            string qualified = token.Replace('/', ':').ToLowerInvariant();
            if (Ids.Qualified(qualified) && PoolIds.Contains(qualified)) return qualified;
        }
        if (!PoolAliases.TryGetValue(token, out string pool)) return null;
        ambiguous = pool == null;
        return pool;
    }
}

public static class NamingJson
{
    private const int MaxInputSize = 4_000_000;
    private const int MaxWorldSize = 32_000_000;
    private static readonly JsonSerializerSettings Settings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        MaxDepth = 24,
        Converters = { new StringEnumConverter { AllowIntegerValues = false } },
        ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
    };

    public static string Write<T>(T value) => JsonConvert.SerializeObject(value, Formatting.Indented, Settings);

    internal static string WritePack(NamingPack pack)
    {
        string text = Write(pack);
        return text.Length <= MaxInputSize ? text : WriteLimited(pack, MaxInputSize, "命名包超过大小上限。");
    }
    internal static string WritePreset(PlayerNamingPreset preset) => WriteLimited(preset, MaxInputSize, "命名预设超过大小上限。");
    internal static string WriteWorld(WorldNaming world) => WriteLimited(world, MaxWorldSize, "世界命名配置超过大小上限。");

    private static string WriteLimited<T>(T value, int limit, string error)
    {
        string text = JsonConvert.SerializeObject(value, Formatting.None, Settings);
        if (text.Length > limit) throw new InvalidOperationException(error);
        return text;
    }

    public static T Read<T>(string text)
    {
        int limit = typeof(T) == typeof(WorldNaming) ? MaxWorldSize : MaxInputSize;
        if (text == null || text.Length > limit) throw new FormatException("Invalid naming JSON size.");
        // Reject duplicate keys rather than silently accepting the last spelling.
        using var reader = new JsonTextReader(new System.IO.StringReader(text)) { MaxDepth = Settings.MaxDepth };
        var value = Newtonsoft.Json.Linq.JToken.ReadFrom(reader, new Newtonsoft.Json.Linq.JsonLoadSettings
        {
            DuplicatePropertyNameHandling = Newtonsoft.Json.Linq.DuplicatePropertyNameHandling.Error
        });
        if (reader.Read()) throw new FormatException("Trailing naming JSON content.");
        return value.ToObject<T>(JsonSerializer.Create(Settings));
    }

    internal static T Copy<T>(T value)
    {
        if (value is null) return default;
        // The input size guard applies to files, not to an already parsed value with expanded defaults.
        var serializer = JsonSerializer.Create(Settings);
        return Newtonsoft.Json.Linq.JToken.FromObject(value, serializer).ToObject<T>(serializer);
    }
}

internal static partial class Configuration
{
    internal static void Validate(NamingPack pack)
    {
        if (pack == null || pack.Format != NamingPack.CurrentFormat || !Ids.Qualified(pack.Id) || pack.Version < 1)
            throw new FormatException("Unsupported pack format, ID or version.");
        if ((pack.FallbackPools && !pack.Supplemental) ||
            pack.Requires.Any(pair => !Ids.Qualified(pair.Key) || pair.Value < 1) ||
            pack.CultureBases.Any(id => !Ids.Qualified(id)) || pack.LanguageBases.Any(id => !Ids.Qualified(id)))
            throw new FormatException("Invalid dependency in " + pack.Id);
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var word in pack.Words)
        {
            if (word == null || !Ids.Qualified(word.Id) || !Ids.Text(word.Text, 128) ||
                (word.Chinese != null && (!Ids.Text(word.Chinese, 128) || string.IsNullOrWhiteSpace(word.Chinese))) ||
                string.IsNullOrWhiteSpace(word.Text) || !Ids.Weight(word.Weight) || !unique.Add(word.Id))
                throw new FormatException("Invalid or duplicate word in " + pack.Id);
        }
        foreach (var pool in pack.Pools) ValidatePool(pool.Key, pool.Value);
        if (pack.PoolLabels.Any(pair => !Ids.Qualified(pair.Key) || !Ids.Text(pair.Value, 64)))
            throw new FormatException("Invalid pool name in " + pack.Id);
        if (pack.PoolAliases.Any(pair => !Ids.Text(pair.Key, 64) || !pair.Key.Any(char.IsLetter) ||
            pair.Key.Any(c => !char.IsLetter(c) && c != '\'') || !Ids.Qualified(pair.Value)))
            throw new FormatException("Invalid pool alias in " + pack.Id);
        foreach (var patch in pack.PoolPatches)
        {
            if (patch == null || !Enum.IsDefined(typeof(PoolOperation), patch.Operation))
                throw new FormatException("Invalid pool alteration in " + pack.Id);
            ValidatePool(patch.Pool, patch.Words);
        }
        foreach (var edit in pack.LanguageEdits)
            if (!Ids.Qualified(edit.Key) || edit.Value == null ||
                edit.Value.Bias.Any(pair => !Ids.Qualified(pair.Key) || !Ids.Weight(pair.Value)) ||
                edit.Value.ExcludeIds.Any(id => !Ids.Qualified(id)) ||
                edit.Value.ExcludeText.Any(text => !Ids.Text(text, 128)))
                throw new FormatException("Invalid language edit: " + edit.Key);
        foreach (var type in pack.Rules)
        {
            if (!Ids.Qualified(type.Key) || type.Value.Count > 256 ||
                type.Value.Any(rule => !Valid(rule)) ||
                type.Value.Select(rule => rule.Id).Distinct().Count() != type.Value.Count)
                throw new FormatException("Invalid rules for " + type.Key + " in " + pack.Id);
        }
        ValidateEvolution(pack);
    }

    private static void ValidatePool(string id, Dictionary<string, double> words)
    {
        if (!Ids.Qualified(id) || words.Count > NameEngine.MaxWords ||
            words.Any(pair => !Ids.Qualified(pair.Key) || !Ids.Weight(pair.Value)))
            throw new FormatException("Invalid pool: " + id);
    }

    private static bool Valid(Rule rule)
    {
        if (rule == null || !Ids.Local(rule.Id) || rule.Parts.Count is < 1 or > 32 ||
            !Enum.IsDefined(typeof(SurnameOrder), rule.Inheritance) ||
            !Enum.IsDefined(typeof(NameWriting), rule.Writing) ||
            rule.NativeCards is { } cards && (!cards.Enabled || !Ids.Text(cards.Template, 16384) ||
                cards.Template.IndexOf('|') is < 1 or > 30 || cards.Template.IndexOf('|') != cards.Template.LastIndexOf('|') ||
                cards.DraftTemplate != null || cards.SourceRule != null ||
                !Enum.IsDefined(typeof(SurnameOrder), cards.Inheritance) ||
                cards.Roles.Values.Any(role => !Ids.Local(role)) || cards.Pools.Values.Any(pool => !Ids.Qualified(pool))) ||
            (rule.ChineseSeparator != null && !Ids.Text(rule.ChineseSeparator, 4)) ||
            (rule.OriginalSeparator != null && !Ids.Text(rule.OriginalSeparator, 4)) ||
            rule.When.Any(pair => !Ids.Local(pair.Key) || !Ids.Text(pair.Value, 256)) ||
            rule.WhenStartsWith.Any(pair => !Ids.Local(pair.Key) || !Ids.Text(pair.Value, 256) || pair.Value.Length == 0) ||
            rule.WhenOneOf.Any(pair => !Ids.Local(pair.Key) || pair.Value is not { Count: > 0 and <= 256 } ||
                pair.Value.Any(value => !Ids.Text(value, 256)) || pair.Value.Distinct(StringComparer.Ordinal).Count() != pair.Value.Count))
            return false;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in rule.Parts)
        {
            if (part == null || !Ids.Local(part.Id) || !ValidPart(part, ids) || !ids.Add(part.Id)) return false;
        }
        return true;
    }

    private static bool ValidPart(Part part, HashSet<string> earlier)
    {
        if (!(part.Chance >= 0 && part.Chance <= 1) || (!part.Optional && part.Chance != 1)) return false;
        var operations = new object[] { part.Pool, part.Literal, part.Input, part.Copy, part.Provider, part.Choices };
        return operations.Count(value => value != null) == 1 &&
               (part.Pool == null || Ids.Qualified(part.Pool)) &&
               (part.PoolInput == null || (part.Pool != null && Ids.Local(part.PoolInput))) &&
               (part.FallbackPool == null || Ids.Qualified(part.FallbackPool) && (part.Input != null || part.PoolInput != null)) &&
               (part.Provider == null || Ids.Qualified(part.Provider)) &&
               (part.Literal == null || Ids.Text(part.Literal, 128)) &&
               (part.Choices == null || (part.Choices.Count is > 0 and <= 256 &&
                   part.Choices.All(text => Ids.Text(text, 128) && !string.IsNullOrWhiteSpace(text)))) &&
               (part.Input == null || Ids.Local(part.Input)) &&
               (part.Copy == null || earlier.Contains(part.Copy));
    }

    internal static EffectiveNames Resolve(NameType type, long? culture, long? language, WorldNaming world,
        IReadOnlyDictionary<string, NamingPack> packs, string defaultPack, IEnumerable<string> modDefaults,
        bool baseVocabulary = false)
    {
        var diagnostics = new List<string>();
        CultureNaming cultureEdit = null;
        LanguageNaming languageEdit = null;
        if (culture.HasValue) world.Cultures.TryGetValue(culture.Value, out cultureEdit);
        if (language.HasValue) world.Languages.TryGetValue(language.Value, out languageEdit);
        var active = ResolveCulturePacks(packs, world, cultureEdit, defaultPack, modDefaults, diagnostics, out var snapshot);
        var culturePack = SelectCulturePack(active, diagnostics, world, culture, defaultPack);
        var languagePack = SelectLanguagePack(active, diagnostics, world, languageEdit, culturePack, defaultPack);
        var rules = ResolveRules(type, active, culturePack, defaultPack, cultureEdit, world.PackPriorities, diagnostics, snapshot);
        var poolIds = active.Values.SelectMany(pack => pack.Pools.Keys
            .Concat(pack.PoolPatches.Select(patch => patch.Pool))).Distinct(StringComparer.Ordinal).ToArray();
        var boundPools = cultureEdit != null && cultureEdit.Cards.TryGetValue(type.Id, out var cards) && cards.Enabled
            ? cards.Pools.Values : Enumerable.Empty<string>();
        var availableRules = rules;
        rules = availableRules.Where(rule => !rule.PresetOnly).ToArray();
        Rule selectedPreset = null;
        if (cultureEdit?.Presets.TryGetValue(type.Id, out var preset) == true &&
            (selectedPreset = availableRules.FirstOrDefault(rule => rule.Id == preset)) != null)
            rules = rules.Where(rule => rule.Id != preset).Prepend(selectedPreset).ToArray();
        var allParts = availableRules.SelectMany(rule => rule.Parts).ToArray();
        var usedPools = allParts.Where(part => part.Pool != null)
            .SelectMany(part => part.PoolInput == null ? new[] { part.Pool } :
                poolIds.Where(id => id.StartsWith(NameEngine.PoolPrefix(part.Pool), StringComparison.Ordinal)))
            .Concat(allParts.Select(part => part.FallbackPool).Where(id => id != null))
            .Concat(boundPools.Where(Ids.Qualified))
            .Distinct(StringComparer.Ordinal).ToArray();
        long? biasLanguage = baseVocabulary ? null : languageEdit?.BiasOrigin ?? language;
        long? parentLanguage = biasLanguage.HasValue && world.LanguageParents.TryGetValue(biasLanguage.Value, out long parent)
            ? parent : null;
        var aliases = EvolutionPacks(active, languagePack, defaultPack, language: true)
            .SelectMany(pack => pack.PoolAliases)
            .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key,
                group => group.Select(pair => pair.Value).Distinct(StringComparer.Ordinal).Count() == 1 ? group.First().Value : null,
                StringComparer.OrdinalIgnoreCase);
        if (snapshot != null)
            foreach (var alias in snapshot.PoolAliases) aliases[alias.Key] = alias.Value;
        var vocabulary = baseVocabulary ? null : languageEdit;
        var additional = new Dictionary<string, Word[]>(StringComparer.Ordinal);
        return new EffectiveNames
        {
            Rules = rules,
            AvailableRules = availableRules,
            SelectedPreset = selectedPreset,
            PoolIds = poolIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            PoolLabels = active.Values.OrderBy(pack => Priority(pack, world.PackPriorities))
                .ThenBy(pack => pack.Id, StringComparer.Ordinal).SelectMany(pack => pack.PoolLabels)
                .GroupBy(pair => pair.Key).ToDictionary(group => group.Key, group => group.Last().Value),
            Pools = ResolvePools(active, languagePack, defaultPack, usedPools, biasLanguage, parentLanguage, vocabulary,
                world.PackPriorities, diagnostics, snapshot: snapshot, baseVocabulary: baseVocabulary),
            PoolAliases = aliases,
            ReadAdditionalPool = id =>
            {
                if (!additional.TryGetValue(id, out var words))
                    additional[id] = words = poolIds.Contains(id)
                        ? ResolvePools(active, languagePack, defaultPack, new[] { id }, biasLanguage, parentLanguage, vocabulary,
                            world.PackPriorities, new List<string>(), snapshot: snapshot, baseVocabulary: baseVocabulary)[id]
                        : Array.Empty<Word>();
                return words;
            },
            Diagnostics = diagnostics.ToArray()
        };
    }

    private static Dictionary<string, NamingPack> ResolveCulturePacks(IReadOnlyDictionary<string, NamingPack> packs,
        WorldNaming world, CultureNaming culture, string fallback, IEnumerable<string> defaults, List<string> diagnostics,
        out NamingPack snapshot)
    {
        var active = ResolveDependencies(packs, world, fallback, defaults, diagnostics);
        snapshot = null;
        if (culture?.Pack != null && world.AppliedPresets.TryGetValue(culture.Pack, out snapshot))
            active[snapshot.Id] = snapshot;
        return active;
    }

    internal static Dictionary<string, NamingPack> ResolveDependencies(IReadOnlyDictionary<string, NamingPack> packs,
        WorldNaming world, string defaultPack, IEnumerable<string> modDefaults, List<string> diagnostics)
    {
        var active = new Dictionary<string, NamingPack>(StringComparer.Ordinal);
        var pending = new HashSet<string>(world.EnabledPacks.Where(id => id != null), StringComparer.Ordinal);
        pending.UnionWith(modDefaults);
        if (defaultPack != null) pending.Add(defaultPack);
        foreach (var id in pending.Where(id => !packs.ContainsKey(id)).OrderBy(id => id, StringComparer.Ordinal).ToArray())
        {
            diagnostics.Add("missing-pack:" + id);
            pending.Remove(id);
        }
        bool changed;
        do
        {
            changed = false;
            foreach (var id in pending.OrderBy(id => id, StringComparer.Ordinal).ToArray())
            {
                var pack = packs[id];
                if (!pack.Requires.All(dep => active.TryGetValue(dep.Key, out var other) && other.Version >= dep.Value))
                    continue;
                pending.Remove(id);
                active.Add(id, pack);
                changed = true;
            }
        } while (changed && pending.Count != 0);
        foreach (var id in pending.OrderBy(id => id, StringComparer.Ordinal))
            foreach (var dep in packs[id].Requires.OrderBy(dep => dep.Key, StringComparer.Ordinal))
                if (!active.TryGetValue(dep.Key, out var other) || other.Version < dep.Value)
                    diagnostics.Add("dependency:" + id + "/" + dep.Key);
        return active;
    }

    private static string Select(Dictionary<string, NamingPack> active, List<string> diagnostics, params string[] choices)
    {
        foreach (var choice in choices)
        {
            if (string.IsNullOrEmpty(choice)) continue;
            if (active.TryGetValue(choice, out var pack) && !pack.Supplemental) return choice;
            diagnostics.Add("binding:" + choice);
        }
        return null;
    }

    private static string SelectCulturePack(Dictionary<string, NamingPack> active, List<string> diagnostics,
        WorldNaming world, long? culture, string fallback)
    {
        CultureNaming settings = null;
        if (culture.HasValue) world.Cultures.TryGetValue(culture.Value, out settings);
        return Select(active, diagnostics, settings?.Pack, world.CulturePack, fallback);
    }

    private static string SelectLanguagePack(Dictionary<string, NamingPack> active, List<string> diagnostics,
        WorldNaming world, LanguageNaming language, string culturePack, string fallback)
    {
        string companion = culturePack != null && active[culturePack].UseAsLanguageDefault ? culturePack : null;
        return Select(active, diagnostics, language?.Pack, world.LanguagePack, companion, fallback);
    }

    private static Rule[] ResolveRules(NameType type, Dictionary<string, NamingPack> active,
        string selected, string fallback, CultureNaming edit, Dictionary<string, int> priorities, List<string> diagnostics,
        NamingPack snapshot = null)
    {
        var rules = new Dictionary<string, Rule>(StringComparer.Ordinal);
        var scoped = snapshot?.Rules.ContainsKey(type.Id) == true ? Array.Empty<NamingPack>() :
            active.Values.Where(pack => pack.Supplemental &&
                (pack.CultureBases.Count == 0 || pack.CultureBases.Contains(selected))).ToArray();
        if (fallback != null && active.TryGetValue(fallback, out var baseline) && baseline.Rules.TryGetValue(type.Id, out var defaults))
            foreach (var rule in defaults) rules[rule.Id] = rule;
        if (selected != null && selected != fallback && active[selected].Rules.TryGetValue(type.Id, out var selectedRules))
            foreach (var rule in selectedRules) rules[rule.Id] = rule;
        foreach (var group in scoped.GroupBy(pack => Priority(pack, priorities)).OrderBy(group => group.Key))
        {
            var keys = group.SelectMany(pack => pack.Rules.TryGetValue(type.Id, out var variants)
                         ? variants.Select(rule => rule.Id) : Array.Empty<string>())
                .Concat(group.SelectMany(pack => pack.RuleEdits.Keys).Where(key => key.StartsWith(type.Id + "/", StringComparison.Ordinal))
                    .Select(key => key.Substring(type.Id.Length + 1))).Distinct();
            foreach (var id in keys)
            {
                string key = type.Id + "/" + id;
                var replacements = group.Where(pack => pack.Rules.ContainsKey(type.Id))
                    .SelectMany(pack => pack.Rules[type.Id]).Where(rule => rule.Id == id).ToArray();
                var edits = group.Where(pack => pack.RuleEdits.ContainsKey(key))
                    .Select(pack => pack.RuleEdits[key]).Where(change => change != null).ToArray();
                if (replacements.Select(NamingJson.Write).Distinct().Skip(1).Any() ||
                    (replacements.Length != 0 && edits.Length != 0))
                {
                    diagnostics.Add("rule-conflict:" + key);
                    continue;
                }
                if (replacements.Length != 0) rules[id] = replacements[0];
                if (edits.Length == 0) continue;
                if (!rules.TryGetValue(id, out var previous))
                {
                    diagnostics.Add("missing-rule:" + key);
                    continue;
                }
                rules[id] = ApplyEdit(previous, MergeEdits(edits, diagnostics, key), diagnostics, key);
            }
        }
        var effective = new List<Rule>();
        foreach (var original in rules.Values)
        {
            var rule = original;
            var key = type.Id + "/" + rule.Id;
            RuleEdit change = null;
            edit?.Rules.TryGetValue(key, out change);
            if (edit != null && edit.Variants.TryGetValue(key, out string variantId))
            {
                var variant = FindVariant(active, selected, fallback, variantId, type.Id, rule.Id);
                if (variant != null && ApplyVariant(rule, variant, change) is Rule evolved)
                {
                    rule = evolved;
                    diagnostics.Add("文化演化 · " + variantId);
                }
                else if (variant == null) diagnostics.Add("missing-variant:" + variantId);
            }
            if (change != null) rule = ApplyEdit(rule, change, diagnostics, key);
            if (rule.When.Keys.Any(key => !type.Fields.ContainsKey(key)) ||
                rule.WhenOneOf.Keys.Any(key => !type.Fields.ContainsKey(key)) ||
                rule.WhenStartsWith.Keys.Any(key => !type.Fields.TryGetValue(key, out var field) || field.Kind != InputKind.Text) ||
                rule.Parts.Any(part => (part.Input != null && !type.Fields.ContainsKey(part.Input)) ||
                    (part.PoolInput != null && (!type.Fields.TryGetValue(part.PoolInput, out var input) || input.Kind != InputKind.Text))))
                diagnostics.Add("undeclared-input:" + type.Id + "/" + rule.Id);
            else effective.Add(rule);
        }
        return effective.OrderByDescending(rule => rule.Priority)
            .ThenByDescending(rule => rule.When.Count + rule.WhenStartsWith.Count + rule.WhenOneOf.Count)
            .ThenBy(rule => rule.Id, StringComparer.Ordinal).ToArray();
    }

    private static RuleEdit MergeEdits(RuleEdit[] edits, List<string> diagnostics, string key)
    {
        T Field<T>(string path, IEnumerable<T> values)
        {
            var distinct = values.Where(value => value != null).GroupBy(NamingJson.Write).ToArray();
            if (distinct.Length < 2) return distinct.Length == 0 ? default : distinct[0].First();
            diagnostics.Add("edit-conflict:" + key + "/" + path);
            return default;
        }
        var result = new RuleEdit
        {
            Order = Field("order", edits.Select(edit => edit.Order)),
            Inheritance = Field("inheritance", edits.Select(edit => edit.Inheritance)),
            Writing = Field("writing", edits.Select(edit => edit.Writing)),
            ChineseSeparator = Field("chinese-separator", edits.Select(edit => edit.ChineseSeparator)),
            OriginalSeparator = Field("original-separator", edits.Select(edit => edit.OriginalSeparator)),
            When = Field("when", edits.Select(edit => edit.When))
        };
        foreach (var part in edits.SelectMany(edit => edit.Parts.Keys.Concat(edit.Fields.Keys)).Distinct())
        {
            var definitions = edits.Where(edit => edit.Parts.ContainsKey(part)).Select(edit => edit.Parts[part]).ToArray();
            var changes = edits.Where(edit => edit.Fields.ContainsKey(part)).Select(edit => edit.Fields[part])
                .Where(edit => edit != null).ToArray();
            if (definitions.Length != 0 && changes.Length != 0)
            {
                diagnostics.Add("edit-conflict:" + key + "/" + part);
                continue;
            }
            var definition = Field(part, definitions);
            if (definition != null) result.Parts[part] = definition;
            if (changes.Length == 0) continue;
            result.Fields[part] = new PartEdit
            {
                Optional = Field(part + "/optional", changes.Select(edit => edit.Optional)),
                Chance = Field(part + "/chance", changes.Select(edit => edit.Chance)),
                Operation = Field(part + "/operation", changes.Select(edit => edit.Operation))
            };
        }
        return result;
    }

    private static Rule ApplyEdit(Rule rule, RuleEdit change, List<string> diagnostics, string key)
    {
        var byId = rule.Parts.ToDictionary(part => part.Id, StringComparer.Ordinal);
        foreach (var patch in change.Parts)
            if (patch.Value != null && patch.Value.Id == patch.Key) byId[patch.Key] = patch.Value;
        foreach (var patch in change.Fields)
        {
            if (!byId.TryGetValue(patch.Key, out var part) || patch.Value == null)
            {
                diagnostics.Add("missing-part:" + key + "/" + patch.Key);
                continue;
            }
            var replacement = patch.Value.Operation ?? part;
            byId[patch.Key] = new Part
            {
                Id = part.Id, Pool = replacement.Pool, PoolInput = replacement.PoolInput, Literal = replacement.Literal, Input = replacement.Input,
                FallbackPool = replacement.FallbackPool, Copy = replacement.Copy, Provider = replacement.Provider, Choices = replacement.Choices,
                Optional = patch.Value.Optional ?? part.Optional,
                Chance = patch.Value.Chance ?? part.Chance
            };
        }
        var order = change.Order ?? rule.Parts.Select(part => part.Id).ToList();
        if (order.Any(id => id == null || !byId.ContainsKey(id)))
        {
            diagnostics.Add("missing-part:" + key);
            return rule;
        }
        var changed = new Rule
        {
            Id = rule.Id, Priority = rule.Priority, PresetOnly = rule.PresetOnly, Inheritance = change.Inheritance ?? rule.Inheritance,
            UseNativeWordGroups = rule.UseNativeWordGroups,
            NativeCards = rule.NativeCards,
            Writing = change.Writing ?? rule.Writing, ChineseSeparator = change.ChineseSeparator ?? rule.ChineseSeparator,
            OriginalSeparator = change.OriginalSeparator ?? rule.OriginalSeparator,
            When = change.When ?? rule.When, WhenStartsWith = rule.WhenStartsWith, WhenOneOf = rule.WhenOneOf,
            Parts = order.Select(id => byId[id]).ToList()
        };
        if (change.Order != null)
            foreach (var pending in byId.Keys.Where(id => !order.Contains(id)))
                diagnostics.Add("pending-part:" + key + "/" + pending);
        if (Valid(changed)) return changed;
        diagnostics.Add("invalid-edit:" + key);
        return rule;
    }

    private static Dictionary<string, Word[]> ResolvePools(Dictionary<string, NamingPack> active, string selected,
        string fallback, string[] usedPools, long? languageId, long? languageParent, LanguageNaming language, Dictionary<string, int> priorities,
        List<string> diagnostics, ISet<string> restrictedPools = null, NamingPack snapshot = null, bool baseVocabulary = false)
    {
        var result = new Dictionary<string, Word[]>(StringComparer.Ordinal);
        if (usedPools.Length == 0) return result;
        var words = new Dictionary<string, Word>(StringComparer.Ordinal);
        var groups = active.Values.Where(pack => pack != snapshot).GroupBy(pack => Priority(pack, priorities))
            .OrderBy(group => group.Key).ToArray();
        var capturedWords = snapshot?.Words.ToDictionary(word => word.Id, StringComparer.Ordinal);
        foreach (var group in groups)
        foreach (var entries in group.SelectMany(pack => pack.Words).GroupBy(word => word.Id))
        {
            var first = entries.First();
                if (entries.Any(word => word.Text != first.Text || word.Chinese != first.Chinese || word.Weight != first.Weight))
                diagnostics.Add("word-conflict:" + entries.Key);
            else words[entries.Key] = first;
        }
        foreach (var id in usedPools)
        {
            var weights = new Dictionary<string, double>(StringComparer.Ordinal);
            Dictionary<string, double> capturedPool = null;
            bool captured = snapshot != null && snapshot.Pools.TryGetValue(id, out capturedPool) &&
                (selected == snapshot.Id || selected == null || !active[selected].Pools.ContainsKey(id));
            var basePack = selected != null && active[selected].Pools.ContainsKey(id) ? selected : fallback;
            bool hasBasePool = false;
            if (captured) MergeMax(weights, capturedPool);
            else if (basePack != null && active.TryGetValue(basePack, out var primary) && primary.Pools.TryGetValue(id, out var pool))
            {
                MergeMax(weights, pool);
                hasBasePool = true;
            }
            foreach (var priority in groups)
            {
                if (captured) break;
                var contributions = priority.Select(pack => Collapse(Operations(pack, id, selected, fallback, hasBasePool)))
                    .Where(patch => patch.Replacement != null || patch.Members.Count != 0).ToArray();
                var replacements = contributions.Where(patch => patch.Replacement != null).ToArray();
                if (replacements.Length != 0)
                {
                    string signature = Signature(replacements[0].Replacement);
                    if (replacements.Skip(1).Any(patch => Signature(patch.Replacement) != signature) ||
                        contributions.Any(patch => patch.Replacement == null))
                    {
                        diagnostics.Add("pool-conflict:" + id);
                        continue;
                    }
                    weights.Clear();
                    MergeMax(weights, replacements[0].Replacement);
                }
                foreach (var entry in contributions.SelectMany(patch => patch.Members).GroupBy(pair => pair.Key))
                {
                    if (entry.Any(pair => !pair.Value.HasValue))
                    {
                        if (entry.Any(pair => pair.Value.HasValue)) diagnostics.Add("pool-conflict:" + id + "/" + entry.Key);
                        else weights.Remove(entry.Key);
                    }
                    else
                    {
                        double max = entry.Max(pair => pair.Value.Value);
                        if (!weights.TryGetValue(entry.Key, out double previous) || previous < max) weights[entry.Key] = max;
                    }
                }
            }
            PoolEdit edit = null;
            language?.Pools.TryGetValue(id, out edit);
            var excludedIds = new HashSet<string>(edit?.ExcludeIds ?? new List<string>(), StringComparer.Ordinal);
            var excludedText = new HashSet<string>(edit?.ExcludeText ?? new List<string>(), StringComparer.Ordinal);
            var packBias = new Dictionary<string, double>(StringComparer.Ordinal);
            string packGroup = null;
            foreach (var group in active.Values.Where(pack => !baseVocabulary && pack.Supplemental && pack.LanguageEdits.ContainsKey(id) &&
                         (pack.LanguageBases.Count == 0 || pack.LanguageBases.Contains(selected)))
                         .GroupBy(pack => Priority(pack, priorities)).OrderBy(group => group.Key))
            {
                var changes = group.Select(pack => pack.LanguageEdits[id]).Where(change => change != null).ToArray();
                var groupChoices = changes.Select(change => change.Group).Where(choice => choice != null)
                    .Distinct(StringComparer.Ordinal).ToArray();
                if (groupChoices.Length == 1) packGroup = groupChoices[0];
                else if (groupChoices.Length > 1) diagnostics.Add("word-group-conflict:" + id);
                foreach (var change in changes)
                {
                    excludedIds.UnionWith(change.ExcludeIds);
                    excludedText.UnionWith(change.ExcludeText);
                }
                foreach (var entry in changes.SelectMany(change => change.Bias).GroupBy(pair => pair.Key))
                {
                    var values = entry.Select(pair => pair.Value).Distinct().ToArray();
                    if (values.Length != 1) diagnostics.Add("bias-conflict:" + id + "/" + entry.Key);
                    else if (Ids.Weight(values[0])) packBias[entry.Key] = values[0];
                }
            }
            string groupId = edit?.Group ?? packGroup;
            var wordGroup = FindWordGroup(active, selected, fallback, groupId, id, snapshot);
            var restricted = wordGroup == null ? null : new HashSet<string>(wordGroup.Words, StringComparer.Ordinal);
            if (groupId != null)
            {
                restrictedPools?.Add(id);
                diagnostics.Add(restricted == null ? "missing-word-group:" + groupId : "语言词群 · " + groupId);
            }
            var preferred = groupId == null ? PreferredWords(language, active, selected, fallback, id, diagnostics, snapshot) : null;
            var candidates = new List<Word>();
            var vocabulary = captured ? capturedWords : words;
            foreach (var weight in weights.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (!vocabulary.TryGetValue(weight.Key, out var word))
                {
                    diagnostics.Add("missing-word:" + id + "/" + weight.Key);
                    continue;
                }
                if (excludedIds.Contains(word.Id) || excludedText.Contains(word.Text) ||
                    (word.Chinese != null && excludedText.Contains(word.Chinese))) continue;
                double bias = 1;
                bool manuallyWeighted = false;
                if (edit != null && edit.Bias.TryGetValue(word.Id, out double custom) && Ids.Weight(custom))
                {
                    bias = custom;
                    manuallyWeighted = true;
                }
                else if (packBias.TryGetValue(word.Id, out double fromPack)) bias = fromPack;
                else if (languageId.HasValue)
                {
                    bias = Bias(languageId.Value, id, word.Id);
                    if (languageParent.HasValue) bias = 0.7 * Bias(languageParent.Value, id, word.Id) + 0.3 * bias;
                }
                if (restricted != null && !restricted.Contains(word.Id)) continue;
                double preference = !manuallyWeighted && preferred != null && preferred.TryGetValue(word.Id, out double extra) ? extra : 0;
                double weightValue = word.Weight * weight.Value * bias *
                    (1 + preference);
                if (weightValue <= 0) continue;
                candidates.Add(new Word { Id = word.Id, Text = word.Text, Chinese = word.Chinese, Weight = weightValue });
                if (candidates.Count == NameEngine.MaxWords) break;
            }
            result.Add(id, candidates.ToArray());
        }
        return result;
    }

    private static IEnumerable<PoolPatch> Operations(NamingPack pack, string id, string selected, string fallback, bool hasBasePool)
    {
        if (pack.Supplemental && pack.LanguageBases.Count > 0 && !pack.LanguageBases.Contains(selected)) yield break;
        if (pack.Supplemental && (!pack.FallbackPools || !hasBasePool) && pack.Pools.TryGetValue(id, out var pool))
            yield return new PoolPatch { Pool = id, Words = pool };
        if (pack.Supplemental || pack.Id == selected || pack.Id == fallback)
            foreach (var patch in pack.PoolPatches)
                if (patch.Pool == id) yield return patch;
    }

    private static (Dictionary<string, double> Replacement, Dictionary<string, double?> Members) Collapse(IEnumerable<PoolPatch> patches)
    {
        Dictionary<string, double> replacement = null;
        var members = new Dictionary<string, double?>(StringComparer.Ordinal);
        foreach (var patch in patches)
        {
            if (patch.Operation == PoolOperation.Replace)
            {
                replacement = new Dictionary<string, double>(patch.Words, StringComparer.Ordinal);
                members.Clear();
                continue;
            }
            foreach (var pair in patch.Words)
            {
                if (replacement != null)
                {
                    if (patch.Operation == PoolOperation.Exclude) replacement.Remove(pair.Key);
                    else if (!replacement.TryGetValue(pair.Key, out double previous) || previous < pair.Value)
                        replacement[pair.Key] = pair.Value;
                }
                else if (patch.Operation == PoolOperation.Exclude) members[pair.Key] = null;
                else if (!members.TryGetValue(pair.Key, out double? previous) || !previous.HasValue || previous < pair.Value)
                    members[pair.Key] = pair.Value;
            }
        }
        return (replacement, members);
    }

    private static int Priority(NamingPack pack, Dictionary<string, int> priorities) =>
        priorities.TryGetValue(pack.Id, out int value) ? value : pack.Priority;

    private static double Bias(long language, string pool, string word) =>
        0.75 + (NamingRandom.Hash(language.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                     "/" + pool + "/" + word) % 501) / 1000.0;

    private static string Signature(Dictionary<string, double> words) =>
        NamingJson.Write(words.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray());

    private static void MergeMax(Dictionary<string, double> target, Dictionary<string, double> words)
    {
        foreach (var word in words)
            if (!target.TryGetValue(word.Key, out var weight) || weight < word.Value) target[word.Key] = word.Value;
    }
}
