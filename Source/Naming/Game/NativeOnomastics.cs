using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace Chinese_Name.Naming.Game;

internal static class NativeOnomastics
{
    internal const string City = "worldbox:city";
    private const string SurnameToken = "\uE000\uE001";
    [ThreadStatic] private static DrawContext _draw;

    private static readonly Dictionary<MetaType, string> Types =
        NameSetAsset.getTypes().Where(VanillaNames.ByMeta.ContainsKey)
            .ToDictionary(meta => meta, meta => VanillaNames.ByMeta[meta]);
    private static readonly Dictionary<string, MetaType> Metas =
        Types.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    internal static readonly (string Type, string Icon, string Description, string ExampleKind)[] AdditionalCategories =
    {
        (VanillaNames.War, "iconWar", "由战争类型指定的国家取名", "normal"),
        ("worldbox:alliance", "iconAlliance", "由联盟发起国取名", null),
        ("worldbox:army", "iconArmy", "由驻地城市取名", null),
        ("worldbox:book", "iconBooks", "由作者取名，预览示例为史书", "book_name_history"),
        (VanillaNames.Plot, "iconDiplomacy", "由发起者取名，预览示例为语言分化", "language_divergence"),
        ("worldbox:motto", "iconDiplomacy", "用于国家、氏族和联盟，预览示例为国家格言", "kingdom"),
        ("worldbox:item", "iconItemType", "由制作者取名，用于传奇装备和重铸", null)
    };

    internal static IEnumerable<MetaType> Categories => Types.Keys;

    internal static MetaType? Meta(string type) => type != null && Metas.TryGetValue(type, out var meta) ? meta : (MetaType?)null;

    internal static string Type(MetaType type) => Types.TryGetValue(type, out string id) ? id : null;

    internal static bool Supports(string type) => type != null && (Meta(type).HasValue ||
        AdditionalCategories.Any(category => category.Type == type) || NamingApi.Current.CultureOnomastics.ContainsKey(type));

    internal static CardNaming Selection(WorldNaming world, Culture culture, string type)
    {
        if (culture == null || !Supports(type)) return null;
        if (world.Cultures.TryGetValue(culture.id, out var custom) && custom.Cards.TryGetValue(type, out var cards))
            return cards;
        return world.UseSavedVanillaCards && Meta(type) is MetaType meta && culture.data.onomastics?.ContainsKey(meta) == true
            ? new CardNaming { Enabled = true } : null;
    }

    internal static OnomasticsData Read(Culture culture, string type, CardNaming choice, bool reset = false)
    {
        if (!reset && (choice?.DraftTemplate ?? choice?.Template) is string template) return Parse(template);
        return Meta(type) is MetaType meta ? ReadVanilla(culture, meta, reset) : new OnomasticsData();
    }

    private static OnomasticsData ReadVanilla(Culture culture, MetaType type, bool reset)
    {
        var cards = culture._onomastics_data;
        cards.TryGetValue(type, out var existing);
        if (!reset && existing != null) return Copy(existing);
        cards.Remove(type);
        try
        {
            return Isolate(unchecked((long)((ulong)culture.id * 104729 + (ulong)type + 1)),
                () => Copy(culture.getOnomasticData(type)));
        }
        finally
        {
            cards.Remove(type);
            if (existing != null) cards[type] = existing;
        }
    }

    private static OnomasticsData Copy(OnomasticsData from)
    {
        var result = new OnomasticsData();
        result.cloneFrom(from);
        return result;
    }

    internal static OnomasticsData Parse(string template)
    {
        var result = new OnomasticsData();
        result.loadFromShortTemplate(template);
        return result;
    }

    internal static void Apply(Culture culture, string type, CardNaming choice, Action persist)
    {
        using var native = Read(culture, type, choice);
        Validate(native, type, choice);
        choice.DraftTemplate = null;
        choice.Template = Meta(type) == null ? native.getShortTemplate() : null;
        persist();
        if (Meta(type) is MetaType meta) culture._onomastics_data[meta] = Copy(native);
    }

    internal static void Validate(OnomasticsData cards, string type, CardNaming choice)
    {
        var layout = cards.getFullTemplateData();
        if (layout.Count is 0 or > 30) throw new InvalidOperationException("命名卡片须有 1 至 30 张。");
        if (!choice.Enabled) return;
        if (layout.Any(card => !NativeNamingGlyphs.AppliesTo(card, type)))
            throw new InvalidOperationException("卡片含有其他命名目标的字形。");
        if (choice.Roles.Values.Any(role => !Ids.Local(role)))
            throw new InvalidOperationException("请为当前排列中的词组绑定有效部件。");
        for (int i = 0; i < layout.Count; i++)
            if (layout[i] == NativeNamingGlyphs.AsPool && (i == 0 || !IsWordGroup(layout[i - 1])))
                throw new InvalidOperationException("卡片「作为词库」须紧贴词组右侧。");
        string legacySurname = SurnameCard(layout, choice);
        bool markedSurname = layout.Contains(NativeNamingGlyphs.KeepSurname);
        if (markedSurname && (type != VanillaNames.Person || legacySurname != null))
            throw new InvalidOperationException("姓氏覆盖只用于生物，不与旧版姓氏字形混用。");
        bool hasRoles = layout.Any(id => Role(id, choice) != null);
        var groups = new List<string>();
        int branch = 1;
        foreach (string card in layout.Concat(new[] { "divider" }))
        {
            if (card != "divider") { groups.Add(card); continue; }
            if (groups.Count == 0) throw new InvalidOperationException($"第 {branch} 种排列为空，请添加字形。");
            if (markedSurname)
            {
                int marker = groups.IndexOf(NativeNamingGlyphs.KeepSurname);
                int index = marker - 1;
                if (index >= 0 && groups[index] == NativeNamingGlyphs.AsPool) index--;
                if (index < 0 || !IsWordGroup(groups[index]) || groups.LastIndexOf(NativeNamingGlyphs.KeepSurname) != marker)
                    throw new InvalidOperationException($"第 {branch} 种排列需要一枚紧贴姓氏词组或其「作为词库」字形的姓氏覆盖。");
                if (!groups.Where((id, position) => position != index).Any(IsWordGroup))
                    throw new InvalidOperationException($"第 {branch} 种排列缺少名字词组。");
            }
            if (legacySurname != null && (type != VanillaNames.Person || groups.Count(id => id == legacySurname) != 1 ||
                    groups.Count(id => Role(id, choice) == VanillaNames.Surname || id == legacySurname) != 1))
                throw new InvalidOperationException($"第 {branch} 种排列需要恰好一个姓氏字形。");
            if (hasRoles && !markedSurname)
            {
                var roles = groups.Select(id => Role(id, choice)).ToArray();
                if (type == VanillaNames.Person && !roles.Any(role => role is "given" or "first") &&
                    !groups.Any(id => IsWordGroup(id) && Role(id, choice) != VanillaNames.Surname))
                    throw new InvalidOperationException($"第 {branch} 种排列缺少给名字形。");
                if (type == City && !roles.Contains("name") && !groups.Any(IsWordGroup))
                    throw new InvalidOperationException($"第 {branch} 种排列缺少地名词组。");
            }
            groups.Clear();
            branch++;
        }
    }

    private static string Role(string card, CardNaming choice) =>
        NativeNamingGlyphs.Find(card)?.Part ?? (choice.Roles.TryGetValue(card, out var role) ? role : null);

    private static string SurnameCard(IEnumerable<string> layout, CardNaming choice) =>
        layout.FirstOrDefault(id => NativeNamingGlyphs.Find(id)?.Part == VanillaNames.Surname) ?? choice.SurnameGroup;

    private static bool IsWordGroup(string id) => AssetManager.onomastics_library.get(id)?.isGroupType() == true;

    internal static IEnumerable<string> PoolSelectorGroups(OnomasticsData cards)
    {
        var layout = cards.getFullTemplateData();
        for (int i = 1; i < layout.Count; i++)
            if (layout[i] == NativeNamingGlyphs.AsPool && IsWordGroup(layout[i - 1])) yield return layout[i - 1];
    }

    internal static IEnumerable<string> SelectorTokens(OnomasticsData cards) => PoolSelectorGroups(cards).Distinct()
        .SelectMany(group => cards.getGroup(group)?.characters ?? Array.Empty<string>());

    internal static IEnumerable<string> ArrangementPools(OnomasticsData cards, CardNaming choice, Rule rule, EffectiveNames effective)
    {
        var layout = cards.getFullTemplateData();
        for (int i = 0; i < layout.Count; i++)
        {
            string id = layout[i];
            if (IsWordGroup(id) && i + 1 < layout.Count && layout[i + 1] == NativeNamingGlyphs.AsPool)
            {
                foreach (var token in cards.getGroup(id)?.characters ?? Array.Empty<string>())
                    if (effective.ResolvePoolToken(token, out _) is string pool) yield return pool;
                continue;
            }
            if (IsWordGroup(id) && !cards.isGroupEmpty(id)) continue;
            var part = rule?.Parts.FirstOrDefault(candidate => candidate.Id == Role(id, choice));
            if (NativeNamingGlyphs.FindInput(id) is { } glyph && (part == null || NativeNamingGlyphs.ForInput(glyph.Type, part) != glyph))
                part = new Part { Pool = glyph.PoolPrefix, PoolInput = glyph.Field, FallbackPool = glyph.FallbackPool };
            foreach (var pool in effective.ReferencedPools(part)) yield return pool;
            if (part == null && choice.Pools.TryGetValue(id, out string bound)) yield return bound;
        }
    }

    internal static HashSet<string> CulturePools(NamingService service, Actor founder)
    {
        if (founder == null) return null;
        var result = new HashSet<string>(StringComparer.Ordinal);
        var culture = founder.culture;
        foreach (var type in service.Types.Where(type => Supports(type.Id)))
        {
            string kind = AdditionalCategories.FirstOrDefault(category => category.Type == type.Id).ExampleKind;
            var request = VanillaNames.Request(type.Id, founder, 0, kind: kind);
            var effective = service.Inspect(request, null);
            var choice = Selection(service.ActiveWorld, culture, type.Id);
            var source = BoundRule(choice, request, effective);
            if (choice?.Enabled == true && (choice.SourceRule == null || source != null))
            {
                using var cards = Read(culture, type.Id, choice);
                result.UnionWith(ArrangementPools(cards, choice, source, effective));
            }
            else if ((source ?? MatchedRule(request, effective)) is { } rule)
            {
                using var cards = NativeNamingArrangement.ReadPackaged(type.Id, rule.NativeCards);
                result.UnionWith(cards == null ? rule.Parts.SelectMany(effective.ReferencedPools) :
                    ArrangementPools(cards, rule.NativeCards, rule, effective));
            }
        }
        return result;
    }

    internal static Rule MatchedRule(NameRequest request, EffectiveNames effective) =>
        effective.SelectedPreset is { } preset && NameEngine.Matches(preset, request) ? preset :
        effective.Rules.FirstOrDefault(rule => rule.NativeCards != null && NameEngine.Matches(rule, request)) ??
            effective.Rules.FirstOrDefault(rule => NameEngine.Matches(rule, request));

    internal static Rule BoundRule(CardNaming choice, NameRequest request, EffectiveNames effective) =>
        choice?.SourceRule == null ? MatchedRule(request, effective) :
            effective.AvailableRules.FirstOrDefault(rule => rule.Id == choice.SourceRule && NameEngine.Matches(rule, request));

    internal static Dictionary<string, (CardNaming Cards, string[] Pools)> ExportArrangements(NamingService service,
        Culture culture, long? language, WorldNaming world)
    {
        var arrangements = new Dictionary<string, (CardNaming, string[])>(StringComparer.Ordinal);
        foreach (var type in service.Types.Where(type => Supports(type.Id)))
        {
            var effective = service.Inspect(new NameRequest(type.Id, 0, culture.id, language), world);
            var choice = Selection(world, culture, type.Id);
            var rules = choice?.SourceRule == null ? effective.Rules :
                effective.AvailableRules.Where(rule => rule.Id == choice.SourceRule).ToArray();
            var pools = new HashSet<string>(StringComparer.Ordinal);
            CardNaming snapshot = null;
            if (choice?.Enabled == true && rules.Length != 0)
            {
                using var cards = Read(culture, type.Id, choice);
                Validate(cards, type.Id, choice);
                snapshot = NamingJson.Copy(choice);
                snapshot.Template = cards.getShortTemplate();
                snapshot.DraftTemplate = null;
                foreach (var rule in rules) pools.UnionWith(ArrangementPools(cards, choice, rule, effective));
            }
            foreach (var rule in effective.Rules.Where(rule => rule.NativeCards != null && (snapshot == null || !rules.Contains(rule))))
            {
                using var cards = NativeNamingArrangement.ReadPackaged(type.Id, rule.NativeCards);
                if (cards != null) pools.UnionWith(ArrangementPools(cards, rule.NativeCards, rule, effective));
            }
            if (snapshot != null || pools.Count > 0) arrangements.Add(type.Id, (snapshot, pools.ToArray()));
        }
        return arrangements;
    }

    internal static bool UsesSurname(Culture culture, CardNaming choice)
    {
        if ((choice.DraftTemplate ?? choice.Template) is string template)
        {
            using var draft = Parse(template);
            return UsesSurname(draft, choice);
        }
        culture._onomastics_data.TryGetValue(MetaType.Unit, out var data);
        return UsesSurname(data, choice);
    }

    internal static bool UsesSurname(OnomasticsData data, CardNaming choice)
    {
        var layout = data?.getFullTemplateData();
        return layout != null && (layout.Contains(NativeNamingGlyphs.KeepSurname) || SurnameCard(layout, choice) != null);
    }

    internal static NameResult Generate(NameType type, NameRequest request, WorldNaming world, EffectiveNames effective,
        Func<string, bool> accept)
    {
        if (!request.Culture.HasValue || !Supports(type.Id)) return null;
        if (Meta(type.Id) == null && request.FixedParts.Count > 0) return null;
        var culture = World.world?.cultures.get(request.Culture.Value);
        if (culture == null) return null;
        var choice = Selection(world, culture, type.Id);
        var sourceRule = BoundRule(choice, request, effective);
        using var cards = choice?.Enabled == true && (choice.SourceRule == null || sourceRule != null)
            ? Read(culture, type.Id, choice)
            : new OnomasticsData();
        if (choice?.Enabled != true || choice.SourceRule != null && sourceRule == null)
        {
            sourceRule = MatchedRule(request, effective);
            if (Meta(type.Id) == null && sourceRule?.UseNativeWordGroups != true && sourceRule?.NativeCards == null) return null;
            if (!NativeNamingArrangement.FillDefault(cards, type.Id, sourceRule, effective, out _)) return null;
            choice = NativeNamingArrangement.ForRule(type.Id, sourceRule);
        }
        if (NativeNamingArrangement.TryUpgrade(type.Id, cards, sourceRule, choice, out var upgraded)) choice = upgraded;
        var diagnostics = new List<string>(effective.Diagnostics) { "原版命名学" };
        var layout = cards.getFullTemplateData();
        var writing = request.Writing ?? sourceRule?.Writing ?? NameWriting.Original;
        bool explicitSeparators = choice.ExplicitSeparators || layout.Contains(NativeNamingGlyphs.RuleSeparator);
        var separator = sourceRule == null || explicitSeparators ? "" : NameEngine.Separator(sourceRule, writing);
        var roles = layout.Select(id => Role(id, choice)).Where(role => role != null).ToHashSet();
        var required = Meta(type.Id) != null ? roles : layout.Where(id =>
            IsWordGroup(id) && cards.isGroupEmpty(id) && !choice.Pools.ContainsKey(id))
            .Select(id => Role(id, choice)).Where(role => role != null).ToHashSet();
        if (required.Count != 0 && (sourceRule == null || !required.IsSubsetOf(sourceRule.Parts.Select(part => part.Id))))
            return new NameResult(GenerationStatus.NoRule, null, null, diagnostics.Append("卡片绑定的规则或部件已不可用。"));
        if (sourceRule?.Parts.Any(part => part.Provider != null && required.Contains(part.Id)) == true)
            return new NameResult(GenerationStatus.NoRule, null, null, diagnostics.Append("卡片绑定的部件需要由模组提供。"));
        try { Validate(cards, type.Id, choice); }
        catch (InvalidOperationException error)
        {
            diagnostics.Add(error.Message);
            return new NameResult(GenerationStatus.NoRule, null, null, diagnostics);
        }
        var parts = new Dictionary<string, string>();
        var pools = new Dictionary<string, Word[]>();
        foreach (var binding in choice.Pools.Where(binding => layout.Contains(binding.Key)))
        {
            if (!effective.PoolIds.Contains(binding.Value))
            {
                diagnostics.Add("文化词组 · " + binding.Key + " · 缺少 " + binding.Value);
                continue;
            }
            pools[binding.Key] = effective.GetPool(binding.Value);
            diagnostics.Add("语言词库 · " + binding.Value);
        }
        var sex = request.Inputs.TryGetValue("sex", out var value)
            ? Equals(value, "male") ? ActorSex.Male : ActorSex.Female : ActorSex.None;
        request.FixedParts.TryGetValue(VanillaNames.Surname, out var fixedSurname);
        string surnameCard = SurnameCard(layout, choice);
        bool markedSurname = layout.Contains(NativeNamingGlyphs.KeepSurname);
        var scope = new DrawContext(cards, pools, choice, surnameCard, fixedSurname, request, effective, sourceRule, writing, diagnostics);
        for (int i = 0; i < NameEngine.Attempts; i++)
        {
            string text = Isolate(unchecked((long)(request.Seed + (ulong)i * 104729)), () =>
            {
                var previous = _draw;
                _draw = scope;
                try { return cards.generateName(sex); }
                finally { _draw = previous; }
            });
            if (scope.Missing || markedSurname && scope.SurnameIndex < 0 || text == OnomasticsData.DEFAULT_NAME_FAILED) continue;
            parts.Clear();
            Dictionary<string, NamePartSource> sources = null;
            string join = "";
            var branch = cards.getCurrentSubgroup();
            bool hasInputGlyph = branch.Any(card => NativeNamingGlyphs.FindInput(card) != null);
            bool simple = !markedSurname && branch.Select((id, index) =>
                id == NativeNamingGlyphs.AsPool || scope.Picked.ContainsKey(index)).All(picked => picked) &&
                branch.Distinct().Count() == branch.Count;
            if (simple && (roles.Count != 0 || hasInputGlyph))
            {
                sources = new Dictionary<string, NamePartSource>();
                for (int position = 0; position < branch.Count; position++)
                {
                    string group = branch[position];
                    if (group == NativeNamingGlyphs.AsPool) continue;
                    var draw = scope.Picked[position];
                    string role = scope.RoleAt(position, group);
                    string id = role != null && !parts.ContainsKey(role) ? role : group;
                    parts.Add(id, draw.Text);
                    sources.Add(id, scope.CardSource(draw.Source, group, position));
                }
                join = separator;
                text = NameEngine.Join(parts.Values, join);
            }
            else if (TryDescribeSeparatedName(branch, scope, text, parts,
                hasInputGlyph || explicitSeparators && roles.Count != 0, out sources))
            {
                text = string.Concat(parts.Values);
            }
            else if (surnameCard != null || markedSurname)
            {
                string fragment = markedSurname ? scope.Surname : SurnameToken;
                int index = markedSurname ? scope.SurnameOffset : text.IndexOf(SurnameToken, StringComparison.Ordinal);
                if (scope.Surname == null || index < 0 || fragment.Length == 0 || index + fragment.Length > text.Length ||
                    string.Compare(text, index, fragment, 0, fragment.Length, StringComparison.OrdinalIgnoreCase) != 0 ||
                    !markedSurname && text.IndexOf(SurnameToken, index + fragment.Length, StringComparison.Ordinal) >= 0) continue;
                if (index > 0) parts["before-surname"] = text.Substring(0, index);
                parts[VanillaNames.Surname] = scope.Surname;
                if (index + fragment.Length < text.Length)
                    parts["after-surname"] = text.Substring(index + fragment.Length);
                text = string.Concat(parts.Values);
                if (scope.Picked.TryGetValue(scope.SurnameIndex, out var draw))
                    sources = new Dictionary<string, NamePartSource> { [VanillaNames.Surname] =
                        scope.CardSource(draw.Source, branch[scope.SurnameIndex], scope.SurnameIndex) };
            }
            else parts["name"] = text;
            if (!Ids.Text(text, type.MaxLength) || text.Trim().Length < type.MinLength ||
                (accept != null && !accept(text))) continue;
            return new NameResult(GenerationStatus.Success, text, parts, diagnostics, parts.Keys, sources, join, branch.ToArray());
        }
        diagnostics.Add(scope.MissingSurname ? "姓氏词组没有可用候选。" : "命名卡片没有生成有效结果。");
        return new NameResult(GenerationStatus.Exhausted, null, null, diagnostics);
    }

    // 分隔符由原版执行。只在能复原实际输出时，把字形与文字片段关联起来。
    private static bool TryDescribeSeparatedName(IReadOnlyList<string> cards, DrawContext scope, string generated,
        Dictionary<string, string> parts, bool preserveSpelling, out Dictionary<string, NamePartSource> sources)
    {
        sources = null;
        var fragments = new List<(string Id, string Text, NamePartSource Source)>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        string previous = "";
        for (int index = 0; index < cards.Count; index++)
        {
            string card = cards[index];
            string text;
            NamePartSource source = null;
            string id = "separator-" + index;
            if (card is NativeNamingGlyphs.KeepSurname or NativeNamingGlyphs.AsPool) continue;
            if (scope.Picked.TryGetValue(index, out var draw))
            {
                id = scope.RoleAt(index, card) ?? card;
                if (!ids.Add(id))
                {
                    if (id == VanillaNames.Surname) return false;
                    id = card + "-" + index;
                    ids.Add(id);
                }
                text = card == scope.SurnameCard ? SurnameToken : draw.Text;
                source = scope.CardSource(draw.Source, card, index);
            }
            else if (card == NativeNamingGlyphs.RuleSeparator)
                text = scope.RuleSeparator;
            else if (card is "space" or "hyphen" or "silent_space")
                text = card == "hyphen" ? "-" : card == "space" && previous.Length > 0 && !previous.EndsWith(" ") ? " " : "";
            else return false;
            if (text.Length == 0) continue;
            previous = text;
            fragments.Add((id, text, source));
        }
        string joined = string.Concat(fragments.Select(fragment => fragment.Text)).TrimEnd(' ', '-');
        if (!string.Equals(joined, generated, StringComparison.OrdinalIgnoreCase)) return false;

        sources = new Dictionary<string, NamePartSource>();
        int offset = 0;
        foreach (var fragment in fragments)
        {
            int length = Math.Min(fragment.Text.Length, generated.Length - offset);
            if (length <= 0) break;
            parts.Add(fragment.Id, fragment.Id == VanillaNames.Surname ? scope.Surname :
                preserveSpelling ? fragment.Text.Substring(0, length) : generated.Substring(offset, length));
            if (fragment.Source != null) sources.Add(fragment.Id, fragment.Source);
            offset += length;
        }
        return true;
    }

    private static T Isolate<T>(long seed, Func<T> action)
    {
        var unity = UnityEngine.Random.state;
        var math = Randy.rand;
        var system = Randy.rnd;
        try
        {
            Randy.resetSeed(unchecked((int)(seed ^ (seed >> 32))));
            return action();
        }
        finally
        {
            Randy.rand = math;
            Randy.rnd = system;
            UnityEngine.Random.state = unity;
        }
    }

    private sealed class DrawContext
    {
        internal readonly OnomasticsData Cards;
        internal readonly Dictionary<string, Word[]> Pools;
        internal readonly CardNaming Choice;
        internal readonly string SurnameCard;
        internal readonly string FixedSurname;
        internal readonly NameRequest Request;
        internal readonly EffectiveNames Effective;
        internal readonly Rule Rule;
        internal readonly NameWriting Writing;
        internal string RuleSeparator => Rule == null ? "" : NameEngine.Separator(Rule, Writing);
        internal readonly Dictionary<int, (string Text, NamePartSource Source)> Picked = new();
        internal readonly Dictionary<string, string> Draws = new();
        private readonly Dictionary<string, Word[]> _unusedProviders = new();
        private readonly Dictionary<string, string[]> _poolOptions = new(StringComparer.Ordinal);
        private readonly List<string> _diagnostics;
        internal NamingRandom Random;
        internal string Surname;
        internal int SurnameIndex;
        internal int SurnameOffset;
        internal bool Missing;
        internal bool MissingSurname;

        internal DrawContext(OnomasticsData cards, Dictionary<string, Word[]> pools, CardNaming choice, string surnameCard, string surname,
            NameRequest request, EffectiveNames effective, Rule rule, NameWriting writing, List<string> diagnostics)
        {
            Cards = cards;
            Pools = pools;
            Choice = choice;
            SurnameCard = surnameCard;
            FixedSurname = surname;
            Request = request;
            Effective = effective;
            Rule = rule;
            Writing = writing;
            _diagnostics = diagnostics;
            Random = new NamingRandom(request.Seed);
        }

        private bool IsMarked(int index)
        {
            var branch = Cards.getCurrentSubgroup();
            int next = index + (HasPoolSelector(index) ? 2 : 1);
            return next < branch.Count && branch[next] == NativeNamingGlyphs.KeepSurname;
        }

        internal string RoleAt(int index, string group)
        {
            if (IsMarked(index) || group == SurnameCard) return VanillaNames.Surname;
            var role = NativeNamingGlyphs.FindInput(group)?.Field ?? Role(group, Choice);
            return role == VanillaNames.Surname ? null : role;
        }

        internal NamePartSource CardSource(NamePartSource source, string group, int position) =>
            new NamePartSource(source.Kind, source.Reference, source.MissingTranslation, group, position);

        internal string PickInput(CultureNameGlyph glyph, int index)
        {
            if (Choice.Roles.TryGetValue(glyph.Id, out string role) && Rule?.Parts.FirstOrDefault(part => part.Id == role) is Part bound &&
                NativeNamingGlyphs.ForInput(Request.Type, bound) == glyph)
            {
                string drawn = NameEngine.DrawPart(bound, Request, Effective, Writing, Draws, _unusedProviders,
                    null, null, ref Random, out var origin);
                if (string.IsNullOrWhiteSpace(drawn)) Missing = true;
                return Record(glyph.Id, index, 0, glyph.Field, drawn, origin, surname: false);
            }
            string text = Request.Inputs.TryGetValue(glyph.Field, out var value) ? NameEngine.Value(value) : null;
            var source = new NamePartSource("input", glyph.Field);
            if (glyph.PoolPrefix != null || string.IsNullOrWhiteSpace(text) && glyph.FallbackPool != null)
            {
                string prefix = glyph.PoolPrefix;
                string pool = prefix != null && Ids.Local(text)
                    ? NameEngine.PoolPrefix(prefix) + text : null;
                var words = pool == null ? Array.Empty<Word>() : Effective.GetPool(pool);
                if (words.Length == 0)
                {
                    pool = glyph.FallbackPool ?? glyph.PoolPrefix;
                    words = Effective.GetPool(pool);
                }
                text = NameEngine.RenderWord(NameEngine.PickWord(words, ref Random), Writing, "pool", pool, out source);
            }
            if (string.IsNullOrWhiteSpace(text))
            {
                Missing = true;
                string issue = "命名卡片缺少输入或候选 · " + Request.Type + "/" + glyph.Field;
                if (!_diagnostics.Contains(issue)) _diagnostics.Add(issue);
            }
            return Record(glyph.Id, index, 0, glyph.Field, text, source, surname: false);
        }

        internal string Pick(string group, int index, int offset)
        {
            string value;
            NamePartSource source;
            string role = Role(group, Choice);
            var part = Rule?.Parts.FirstOrDefault(candidate => candidate.Id == role);
            bool surname = IsMarked(index) || SurnameCard == group;
            if (surname && FixedSurname != null)
            {
                value = FixedSurname;
                source = new NamePartSource("inherited");
            }
            else if (HasPoolSelector(index))
            {
                string pool = SelectPool(group);
                var word = pool == null ? null : NameEngine.PickWord(Effective.GetPool(pool), ref Random);
                value = NameEngine.RenderWord(word, Writing, "pool", pool, out source);
                if (value == null) Missing = true;
            }
            else if (Cards.getGroup(group)?.characters is { Length: > 0 } local && NativeNamingGlyphs.Find(group) == null)
            {
                value = local.GetRandom();
                source = new NamePartSource("culture");
            }
            else if (part != null)
            {
                if (part.Chance < 1 && Random.Next() >= part.Chance) value = "";
                else
                {
                    value = NameEngine.DrawPart(part, Request, Effective, Writing, Draws, _unusedProviders,
                        null, null, ref Random, out source);
                    if (value == null && !part.Optional) Missing = true;
                    return Record(group, index, offset, role, value, source, surname);
                }
                source = new NamePartSource("optional");
            }
            else if (Pools.TryGetValue(group, out var words))
            {
                value = NameEngine.RenderWord(NameEngine.PickWord(words, ref Random), Writing, "pool", Choice.Pools[group], out source);
                if (value == null) Missing = true;
            }
            else
            {
                var terms = Cards.getGroup(group)?.characters;
                value = terms is { Length: > 0 } ? terms.GetRandom() : null;
                source = new NamePartSource("culture");
            }
            return Record(group, index, offset, role, value, source, surname);
        }

        private bool HasPoolSelector(int index)
        {
            var branch = Cards.getCurrentSubgroup();
            return index >= 0 && index + 1 < branch.Count && branch[index + 1] == NativeNamingGlyphs.AsPool;
        }

        private string SelectPool(string group)
        {
            if (!_poolOptions.TryGetValue(group, out var options))
            {
                var pools = new List<string>();
                foreach (string alias in Cards.getGroup(group)?.characters ?? Array.Empty<string>())
                {
                    string pool = Effective.ResolvePoolToken(alias, out bool ambiguous);
                    if (ambiguous)
                        _diagnostics.Add($"词库字形 · 「{alias}」指向多个词库。请填写作者/词库。");
                    else if (pool == null)
                        _diagnostics.Add($"词库字形 · 「{alias}」未登记。");
                    else if (Effective.GetPool(pool).Length == 0)
                        _diagnostics.Add($"词库字形 · 「{alias}」在当前语言没有可用词条。");
                    else pools.Add(pool);
                }
                _poolOptions[group] = options = pools.ToArray();
                if (options.Length == 0) _diagnostics.Add("词库字形 · 左侧词组需要至少一个可用的词库名称。");
            }
            return options.Length == 0 ? null : options.GetRandom();
        }

        private string Record(string group, int index, int offset, string role, string value, NamePartSource source, bool surname)
        {
            Picked[index] = (value ?? "", source);
            if (role != null) Draws[role] = value;
            if (!surname) return value ?? "";
            Surname = value;
            SurnameIndex = index;
            SurnameOffset = offset;
            if (string.IsNullOrEmpty(value) && FixedSurname == null) Missing = MissingSurname = true;
            return group == SurnameCard ? SurnameToken : value ?? "";
        }
    }

    [HarmonyPatch(typeof(OnomasticsData), nameof(OnomasticsData.generateName))]
    internal static class BeginArrangementDraw
    {
        private static void Prefix(OnomasticsData __instance)
        {
            var scope = _draw;
            if (scope == null || !ReferenceEquals(scope.Cards, __instance)) return;
            scope.Surname = null;
            scope.SurnameIndex = scope.SurnameOffset = -1;
            scope.Missing = scope.MissingSurname = false;
            scope.Picked.Clear();
            scope.Draws.Clear();
        }
    }

    internal static string DrawRuleSeparator(OnomasticsData data) =>
        _draw is { } scope && ReferenceEquals(scope.Cards, data) ? scope.RuleSeparator : "";

    internal static string DrawGlyph(string id, OnomasticsData data, int index, int offset) =>
        _draw is { } scope && ReferenceEquals(scope.Cards, data) ? scope.Pick(id, index, offset) : "";

    internal static string DrawInputGlyph(CultureNameGlyph glyph, OnomasticsData data, int index) =>
        _draw is { } scope && ReferenceEquals(scope.Cards, data) ? scope.PickInput(glyph, index) : "";

    [HarmonyPatch(typeof(OnomasticsLibrary), nameof(OnomasticsLibrary.groupAction))]
    internal static class DrawGroup
    {
        private static bool Prefix(OnomasticsAsset pAsset, OnomasticsData pData, StringBuilderPool pStringBuilder, int pIndex, ref string __result)
        {
            var scope = _draw;
            if (scope == null || !ReferenceEquals(scope.Cards, pData)) return true;
            __result = scope.Pick(pAsset.id, pIndex, pStringBuilder.Length);
            return false;
        }
    }
}
