using System;
using System.Collections.Generic;
using System.Linq;

namespace Chinese_Name.Naming;

public sealed class CultureVariant
{
    public string Type { get; set; }
    public string Rule { get; set; }
    public string Part { get; set; }
    public string Literal { get; set; }
    public double? Chance { get; set; }
}

public sealed class WordGroup
{
    public string Pool { get; set; }
    public List<string> Words { get; set; } = new();
}

internal static partial class Configuration
{
    private const double LanguageShift = 0.2;

    private static void ValidateEvolution(NamingPack pack)
    {
        foreach (var pair in pack.CultureVariants)
        {
            var value = pair.Value;
            if (!Ids.Local(pair.Key) || value == null || !Ids.Qualified(value.Type) ||
                !Ids.Local(value.Rule) || !Ids.Local(value.Part) ||
                (value.Literal != null) == value.Chance.HasValue ||
                (value.Literal != null && !Ids.Text(value.Literal, 128)) ||
                (value.Chance.HasValue && !(value.Chance.Value > 0 && value.Chance.Value <= 1)))
                throw new FormatException("Invalid culture variant: " + pair.Key);
        }
        foreach (var pair in pack.WordGroups)
            if (!Ids.Local(pair.Key) || pair.Value == null || !Ids.Qualified(pair.Value.Pool) ||
                pair.Value.Words.Count is 0 or > NameEngine.MaxWords ||
                pair.Value.Words.Any(id => !Ids.Qualified(id)))
                throw new FormatException("Invalid word group: " + pair.Key);
    }

    private static IEnumerable<NamingPack> EvolutionPacks(Dictionary<string, NamingPack> active,
        string selected, string fallback, bool language)
    {
        foreach (var pack in active.Values)
        {
            var bases = language ? pack.LanguageBases : pack.CultureBases;
            if (pack.Supplemental ? bases.Count == 0 || bases.Contains(selected) : pack.Id == selected || pack.Id == fallback)
                yield return pack;
        }
    }

    private static CultureVariant FindVariant(Dictionary<string, NamingPack> active,
        string selected, string fallback, string id, string type, string rule)
    {
        foreach (var pack in EvolutionPacks(active, selected, fallback, false))
            if (InPack(id, pack, out var key) && pack.CultureVariants.TryGetValue(key, out var variant) &&
                variant.Type == type && variant.Rule == rule) return variant;
        return null;
    }

    private static WordGroup FindWordGroup(Dictionary<string, NamingPack> active,
        string selected, string fallback, string id, string pool, NamingPack snapshot = null)
    {
        if (id == null) return null;
        if (snapshot != null && InPack(id, snapshot, out var local) &&
            snapshot.WordGroups.TryGetValue(local, out var captured) && captured.Pool == pool) return captured;
        foreach (var pack in EvolutionPacks(active, selected, fallback, true))
            if (InPack(id, pack, out var key) && pack.WordGroups.TryGetValue(key, out var group) &&
                group.Pool == pool) return group;
        return null;
    }

    private static Dictionary<string, double> PreferredWords(LanguageNaming language, Dictionary<string, NamingPack> active,
        string selected, string fallback, string pool, List<string> diagnostics, NamingPack snapshot = null)
    {
        if (language == null) return null;
        Dictionary<string, double> words = null;
        foreach (var preference in language.PreferencesFor(pool))
        {
            if (!(preference.Value > 0 && preference.Value <= 1)) continue;
            var group = FindWordGroup(active, selected, fallback, preference.Key, pool, snapshot);
            if (group == null)
            {
                diagnostics.Add("missing-word-group:" + preference.Key);
                continue;
            }
            diagnostics.Add("语言词群 · " + preference.Key);
            words ??= new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (string word in group.Words.Distinct(StringComparer.Ordinal))
            {
                words.TryGetValue(word, out double previous);
                words[word] = previous + preference.Value;
            }
        }
        return words;
    }

    private static bool InPack(string id, NamingPack pack, out string key)
    {
        int separator = id.IndexOf('/');
        key = separator < 0 ? null : id.Substring(separator + 1);
        return separator > 0 && id.Substring(0, separator) == pack.Id;
    }

    private static Rule ApplyVariant(Rule rule, CultureVariant variant, RuleEdit edit)
    {
        if (variant.Part == "surname" || edit?.Parts.ContainsKey(variant.Part) == true ||
            (edit?.Order != null && !edit.Order.Contains(variant.Part))) return null;
        PartEdit fields = null;
        edit?.Fields.TryGetValue(variant.Part, out fields);
        var part = rule.Parts.FirstOrDefault(value => value.Id == variant.Part);
        if (part == null) return null;
        var updated = NamingJson.Copy(part);
        if (variant.Literal != null && part.Literal != null && fields?.Operation == null)
            updated.Literal = variant.Literal;
        else if (variant.Chance.HasValue && part.Optional && fields?.Chance == null && fields?.Optional != false)
            updated.Chance = variant.Chance.Value;
        else return null;
        var result = new Rule
        {
            Id = rule.Id, Priority = rule.Priority, PresetOnly = rule.PresetOnly, Inheritance = rule.Inheritance, When = rule.When,
            UseNativeWordGroups = rule.UseNativeWordGroups, WhenOneOf = rule.WhenOneOf,
            NativeCards = rule.NativeCards,
            Writing = rule.Writing, ChineseSeparator = rule.ChineseSeparator, OriginalSeparator = rule.OriginalSeparator,
            WhenStartsWith = rule.WhenStartsWith, Parts = rule.Parts.Select(value => value == part ? updated : value).ToList()
        };
        return Valid(result) ? result : null;
    }

    internal static bool EvolveCulture(CultureNaming culture, WorldNaming world, IEnumerable<NameType> types,
        IReadOnlyDictionary<string, NamingPack> packs, string fallback, IEnumerable<string> defaults, ulong seed)
    {
        var diagnostics = new List<string>();
        var active = ResolveCulturePacks(packs, world, culture, fallback, defaults, diagnostics, out var snapshot);
        var selected = Select(active, diagnostics, culture.Pack, world.CulturePack, fallback);
        var byType = types.ToDictionary(type => type.Id, StringComparer.Ordinal);
        var rules = new Dictionary<string, Rule[]>(StringComparer.Ordinal);
        var options = new List<(string Rule, string Variant)>();
        foreach (var pack in EvolutionPacks(active, selected, fallback, false))
        foreach (var pair in pack.CultureVariants)
        {
            var variant = pair.Value;
            if (!byType.TryGetValue(variant.Type, out var type) ||
                (culture.Cards.TryGetValue(variant.Type, out var cards) && cards.Enabled)) continue;
            string key = variant.Type + "/" + variant.Rule;
            string id = pack.Id + "/" + pair.Key;
            if (culture.Variants.TryGetValue(key, out var previous) && previous == id) continue;
            if (!rules.TryGetValue(type.Id, out var available))
                rules[type.Id] = available = ResolveRules(type, active, selected, fallback, culture, world.PackPriorities, diagnostics, snapshot);
            var rule = available.FirstOrDefault(value => value.Id == variant.Rule);
            culture.Rules.TryGetValue(key, out var playerEdit);
            var evolved = rule == null ? null : ApplyVariant(rule, variant, playerEdit);
            if (evolved != null && NamingJson.Write(rule.Parts) != NamingJson.Write(evolved.Parts))
                options.Add((key, id));
        }
        if (options.Count == 0) return false;
        var next = options.OrderBy(pair => pair.Variant, StringComparer.Ordinal).ElementAt(Index(options.Count, seed));
        culture.Variants[next.Rule] = next.Variant;
        return true;
    }

    internal static Dictionary<string, string[]> AvailableWordGroups(WorldNaming world, LanguageNaming settings,
        IReadOnlyDictionary<string, NamingPack> packs, string fallback, IEnumerable<string> defaults, long? culture = null)
        => GroupIds(WordGroupDefinitions(world, settings, packs, fallback, defaults, culture));

    internal static (string Id, WordGroup Group)[] WordGroupDefinitions(WorldNaming world, LanguageNaming settings,
        IReadOnlyDictionary<string, NamingPack> packs, string fallback, IEnumerable<string> defaults, long? culture = null)
    {
        var diagnostics = new List<string>();
        CultureNaming cultureSettings = null;
        if (culture.HasValue) world.Cultures.TryGetValue(culture.Value, out cultureSettings);
        var active = ResolveCulturePacks(packs, world, cultureSettings, fallback, defaults, diagnostics, out var snapshot);
        var basis = SelectCulturePack(active, diagnostics, world, culture, fallback);
        var selected = SelectLanguagePack(active, diagnostics, world, settings, basis, fallback);
        return WordGroupDefinitions(active, selected, fallback, snapshot).ToArray();
    }

    private static IEnumerable<(string Id, WordGroup Group)> WordGroupDefinitions(Dictionary<string, NamingPack> active,
        string selected, string fallback, NamingPack snapshot) => EvolutionPacks(active, selected, fallback, true)
        .Concat(snapshot != null && selected != snapshot.Id ? new[] { snapshot } : Array.Empty<NamingPack>())
        .SelectMany(pack => pack.WordGroups.Select(pair => (pack.Id + "/" + pair.Key, pair.Value)));

    private static Dictionary<string, string[]> GroupIds(IEnumerable<(string Id, WordGroup Group)> groups) =>
        groups.GroupBy(pair => pair.Group.Pool, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
    internal static bool EvolveLanguage(LanguageNaming language, WorldNaming world,
        IReadOnlyDictionary<string, NamingPack> packs, string fallback, IEnumerable<string> defaults, ulong seed, bool split,
        ISet<string> preferredPools = null, long? culture = null)
    {
        var diagnostics = new List<string>();
        CultureNaming cultureSettings = null;
        if (culture.HasValue) world.Cultures.TryGetValue(culture.Value, out cultureSettings);
        var active = ResolveCulturePacks(packs, world, cultureSettings, fallback, defaults, diagnostics, out var snapshot);
        var basis = SelectCulturePack(active, diagnostics, world, culture, fallback);
        var selected = SelectLanguagePack(active, diagnostics, world, language, basis, fallback);
        var definitions = WordGroupDefinitions(active, selected, fallback, snapshot);
        if (!split && preferredPools != null) definitions = definitions.Where(group => preferredPools.Contains(group.Group.Pool));
        var groups = GroupIds(definitions);
        if (groups.Count == 0) return false;
        var locked = new HashSet<string>(StringComparer.Ordinal);
        var available = ResolvePools(active, selected, fallback, groups.Keys.ToArray(), null, null, language,
            world.PackPriorities, diagnostics, locked, snapshot);
        var options = new List<(string Pool, string[] Groups)>();
        foreach (var pool in groups.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (locked.Contains(pool.Key)) continue;
            language.Pools.TryGetValue(pool.Key, out var player);
            var words = available[pool.Key];
            if (words.Length < 2) continue;
            var preferred = split ? PreferredWords(language, active, selected, fallback, pool.Key, diagnostics, snapshot) : null;
            var eligible = new List<string>();
            foreach (string id in pool.Value)
            {
                var group = FindWordGroup(active, selected, fallback, id, pool.Key, snapshot);
                var members = new HashSet<string>(group.Words, StringComparer.Ordinal);
                if (words.Any(word => members.Contains(word.Id) && !HasCustomWeight(player, word.Id)) &&
                    (!split || ChangesRelativeWeight(words, members, preferred, player)))
                    eligible.Add(id);
            }
            if (eligible.Count > 0) options.Add((pool.Key, eligible.ToArray()));
        }
        if (options.Count == 0) return false;
        if (split)
        {
            if (preferredPools != null && options.Any(option => preferredPools.Contains(option.Pool)))
                options.RemoveAll(option => !preferredPools.Contains(option.Pool));
            var (pool, choices) = options[Index(options.Count, seed)];
            string next = choices[Index(choices.Length, seed ^ NamingRandom.Hash(pool))];
            var preferences = language.PreferencesFor(pool).ToDictionary(pair => pair.Key, pair => pair.Value * (1 - LanguageShift),
                StringComparer.Ordinal);
            preferences.TryGetValue(next, out double weight);
            preferences[next] = weight + LanguageShift;
            language.GroupPreferences[pool] = preferences;
        }
        else
            foreach (var pool in options)
                language.Groups[pool.Pool] = pool.Groups[Index(pool.Groups.Length, seed ^ NamingRandom.Hash(pool.Pool))];
        return true;
    }

    private static bool HasCustomWeight(PoolEdit edit, string word) =>
        edit != null && edit.Bias.TryGetValue(word, out double weight) && Ids.Weight(weight);

    private static bool ChangesRelativeWeight(Word[] words, HashSet<string> members, Dictionary<string, double> preferred,
        PoolEdit player)
    {
        // 同比增减所有词的权重不会改变抽取分布。
        double Ratio(string word)
        {
            if (HasCustomWeight(player, word)) return 1;
            double old = preferred != null && preferred.TryGetValue(word, out double share) ? share : 0;
            double next = old * (1 - LanguageShift) + (members.Contains(word) ? LanguageShift : 0);
            return (1 + next) / (1 + old);
        }

        double first = Ratio(words[0].Id);
        return words.Skip(1).Any(word => Math.Abs(Ratio(word.Id) - first) > 1e-12);
    }

    private static int Index(int count, ulong seed) => (int)(new NamingRandom(seed).Next() * count);
}
