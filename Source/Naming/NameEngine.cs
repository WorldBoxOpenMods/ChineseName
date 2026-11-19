using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Chinese_Name.Naming;

internal struct NamingRandom
{
    private ulong _state;
    internal NamingRandom(ulong seed) => _state = seed;

    internal double Next()
    {
        unchecked
        {
            ulong value = (_state += 0x9e3779b97f4a7c15UL);
            value = (value ^ (value >> 30)) * 0xbf58476d1ce4e5b9UL;
            value = (value ^ (value >> 27)) * 0x94d049bb133111ebUL;
            return ((value ^ (value >> 31)) >> 11) * (1.0 / (1UL << 53));
        }
    }

    internal static ulong Hash(string text)
    {
        unchecked
        {
            ulong value = 14695981039346656037UL;
            foreach (byte item in Encoding.UTF8.GetBytes(text)) value = (value ^ item) * 1099511628211UL;
            return value;
        }
    }
}

internal static class NameEngine
{
    internal const int Attempts = 8;
    internal const int MaxWords = 4096;

    internal static string Value(object input) => input switch
    {
        bool flag => flag ? "true" : "false",
        int number => number.ToString(CultureInfo.InvariantCulture),
        string text => text,
        _ => null
    };

    internal static bool Matches(Rule rule, NameRequest request) =>
        rule.When.All(pair => request.Inputs.TryGetValue(pair.Key, out var value) &&
                              string.Equals(Value(value), pair.Value, StringComparison.Ordinal)) &&
        rule.WhenStartsWith.All(pair => request.Inputs.TryGetValue(pair.Key, out var value) &&
                                        value is string text && text.StartsWith(pair.Value, StringComparison.Ordinal)) &&
        rule.WhenOneOf.All(pair => request.Inputs.TryGetValue(pair.Key, out var value) &&
                                   pair.Value.Contains(Value(value), StringComparer.Ordinal));

    internal static string InputError(NameType type, NameRequest request)
    {
        foreach (var input in request.Inputs)
            if (!type.Fields.TryGetValue(input.Key, out var field) || !field.Accepts(input.Value))
                return "input:" + input.Key;
        foreach (var field in type.Fields.Values)
            if (field.Required && !request.Inputs.ContainsKey(field.Id))
                return "missing-input:" + field.Id;
        foreach (var part in request.FixedParts)
            if (!type.FixedParts.Contains(part.Key) || !Ids.Text(part.Value, 128))
                return "fixed-part:" + part.Key;
        return null;
    }

    internal static NameResult Generate(NameType type, NameRequest request, EffectiveNames effective,
        IReadOnlyDictionary<string, Func<NameRequest, IEnumerable<Word>>> providers, Func<string, bool> accept,
        string selectedRule = null)
    {
        var diagnostics = new List<string>();
        NameResult Fail(GenerationStatus code, string detail)
        {
            diagnostics.Add(detail);
            return new NameResult(code, null, null, diagnostics);
        }

        diagnostics.AddRange(effective.Diagnostics);
        var rules = (selectedRule == null ? effective.Rules : effective.AvailableRules)
            .Where(rule => (selectedRule == null || rule.Id == selectedRule) && Matches(rule, request)).ToArray();
        if (rules.Length == 0) return Fail(GenerationStatus.NoRule, "no-rule:" + type.Id);
        // A provider sees the same request once, even when a name needs multiple attempts.
        var customPools = new Dictionary<string, Word[]>(StringComparer.Ordinal);
        var random = new NamingRandom(request.Seed);
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            // Give the preferred rule most attempts while leaving budget for a fallback.
            int ruleIndex = Math.Min(attempt / 2, rules.Length - 1);
            if (attempt == Attempts - 1) ruleIndex = rules.Length - 1;
            var rule = rules[ruleIndex];
            var parts = new Dictionary<string, string>(StringComparer.Ordinal);
            var sources = new Dictionary<string, NamePartSource>(StringComparer.Ordinal);
            var writing = request.Writing ?? rule.Writing;
            string separator = Separator(rule, writing);
            bool failed = false;
            foreach (var part in rule.Parts)
            {
                bool fixedPart = request.FixedParts.TryGetValue(part.Id, out string value);
                NamePartSource source = fixedPart ? new NamePartSource("inherited") : null;
                if (!fixedPart && (part.Chance == 1 || random.Next() < part.Chance))
                    value = DrawPart(part, request, effective, writing, parts, customPools, providers, diagnostics, ref random, out source);
                if (value == null && !part.Optional)
                {
                    string detail = "part:" + rule.Id + "/" + part.Id;
                    if (!diagnostics.Contains(detail)) diagnostics.Add(detail);
                    failed = true;
                    break;
                }
                parts.Add(part.Id, value ?? "");
                if (source != null) sources.Add(part.Id, source);
            }
            // Dropping a persisted part would silently change a person's established surname.
            if (failed || request.FixedParts.Any(p => p.Value.Length != 0 && !parts.ContainsKey(p.Key))) continue;
            var order = rule.Parts.Select(part => part.Id).ToArray();
            string text = Join(order.Select(id => parts[id]), separator);
            if (!Ids.Text(text, type.MaxLength) || text.Trim().Length < type.MinLength ||
                (accept != null && !accept(text))) continue;
            return new NameResult(GenerationStatus.Success, text, parts, diagnostics, order, sources, separator);
        }
        return Fail(GenerationStatus.Exhausted, "attempts:" + Attempts);
    }

    internal static string Separator(Rule rule, NameWriting writing) =>
        (writing == NameWriting.Chinese ? rule.ChineseSeparator : rule.OriginalSeparator) ?? "";

    internal static string Join(IEnumerable<string> parts, string separator) =>
        string.Join(separator, parts.Where(value => !string.IsNullOrEmpty(value)));

    internal static string DrawPart(Part part, NameRequest request, EffectiveNames effective, NameWriting writing,
        Dictionary<string, string> parts, Dictionary<string, Word[]> customPools,
        IReadOnlyDictionary<string, Func<NameRequest, IEnumerable<Word>>> providers, List<string> diagnostics,
        ref NamingRandom random, out NamePartSource source)
    {
        if (part.Literal != null) { source = new NamePartSource("literal"); return part.Literal; }
        if (part.Choices != null)
        {
            source = new NamePartSource("culture");
            return part.Choices.Count == 0 ? null : part.Choices[(int)(random.Next() * part.Choices.Count)];
        }
        if (part.Input != null)
        {
            source = new NamePartSource("input", part.Input);
            string value = request.Inputs.TryGetValue(part.Input, out var input) ? Value(input) : null;
            if (!string.IsNullOrWhiteSpace(value) || part.FallbackPool == null) return value;
            return DrawPool(part.FallbackPool, null, effective, writing, ref random, out source);
        }
        if (part.Copy != null)
        {
            source = new NamePartSource("copy", part.Copy);
            return parts.TryGetValue(part.Copy, out var value) ? value : null;
        }
        if (part.Pool != null)
        {
            string pool = part.Pool;
            if (part.PoolInput != null)
                pool = request.Inputs.TryGetValue(part.PoolInput, out var input) && input is string suffix && Ids.Local(suffix)
                    ? PoolPrefix(pool) + suffix : null;
            return DrawPool(pool, part.FallbackPool, effective, writing, ref random, out source);
        }
        if (part.Provider != null && !customPools.TryGetValue(part.Provider, out _))
            customPools[part.Provider] = ReadProvider(part.Provider, providers, request, diagnostics);
        return RenderWord(part.Provider != null ? PickWord(customPools[part.Provider], ref random) : null,
            writing, "provider", part.Provider, out source);
    }

    internal static string PoolPrefix(string id) => id.EndsWith("-", StringComparison.Ordinal) ? id : id + ".";

    private static string DrawPool(string pool, string fallback, EffectiveNames effective, NameWriting writing,
        ref NamingRandom random, out NamePartSource source)
    {
        var words = pool == null ? Array.Empty<Word>() : effective.GetPool(pool);
        if (words.Length == 0 && fallback != null)
        {
            pool = fallback;
            words = effective.GetPool(pool);
        }
        return RenderWord(PickWord(words, ref random), writing, "pool", pool, out source);
    }

    internal static string RenderWord(Word word, NameWriting writing, string kind, string reference, out NamePartSource source)
    {
        bool translated = writing == NameWriting.Chinese;
        source = new NamePartSource(kind, reference, translated && word != null && word.Chinese == null &&
            word.Text.Any(char.IsLetter) && !word.Text.Any(c => c is >= '\u3400' and <= '\u9fff'));
        return word == null ? null : translated ? word.Chinese ?? word.Text : word.Text;
    }

    internal static Word PickWord(Word[] words, ref NamingRandom random)
    {
        if (words.Length == 0) return null;
        double roll = random.Next() * words.Sum(word => word.Weight);
        foreach (var word in words)
        {
            roll -= word.Weight;
            if (roll < 0) return word;
        }
        return words[words.Length - 1];
    }

    private static Word[] ReadProvider(string id, IReadOnlyDictionary<string, Func<NameRequest, IEnumerable<Word>>> providers,
        NameRequest request, List<string> diagnostics)
    {
        if (providers == null || !providers.TryGetValue(id, out var provider))
        {
            diagnostics?.Add("missing-provider:" + id);
            return Array.Empty<Word>();
        }
        try
        {
            return (provider(request) ?? Array.Empty<Word>()).Take(129)
                .Where(word => word != null && Ids.Qualified(word.Id) && Ids.Weight(word.Weight) && word.Weight > 0 &&
                               Ids.Text(word.Text, 128) && !string.IsNullOrWhiteSpace(word.Text))
                .Take(128).GroupBy(word => word.Id, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(word => word.Weight)
                    .ThenBy(word => word.Text, StringComparer.Ordinal).First())
                .OrderBy(word => word.Id, StringComparer.Ordinal).ToArray();
        }
        catch (Exception error)
        {
            diagnostics.Add("provider:" + id + ":" + error.GetType().Name);
            return Array.Empty<Word>();
        }
    }
}
