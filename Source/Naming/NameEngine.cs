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
        if ((request.FatherSurname != null || request.MotherSurname != null) && !type.FixedParts.Contains(NameRequest.SurnamePart))
            return "fixed-part:" + NameRequest.SurnamePart;
        return null;
    }

    internal static NamingCandidate RuleCandidate(Rule rule, int index, int count) =>
        new(rule, index == count - 1 ? Math.Max(2, Attempts - 2 * index) : 2);

    internal static NameResult Generate(NameType type, NameRequest request, EffectiveNames effective,
        IReadOnlyDictionary<string, Func<NameRequest, IEnumerable<Word>>> providers, Func<string, bool> accept,
        NamingTrace trace, IEnumerable<NamingCandidate> candidates)
    {
        var diagnostics = new List<string>(effective.Diagnostics);
        var result = TryCandidates(type, request, effective, candidates, accept, trace, diagnostics, out int tried, providers);
        if (result != null) return result;
        diagnostics.Add(tried == 0 ? "no-rule:" + type.Id : "rules-exhausted:" + tried);
        return trace.Finish(new NameResult(tried == 0 ? GenerationStatus.NoRule : GenerationStatus.Exhausted, null, null, diagnostics));
    }

    internal static NameResult TryCandidates(NameType type, NameRequest request, EffectiveNames effective,
        IEnumerable<NamingCandidate> candidates, Func<string, bool> accept, NamingTrace trace, List<string> diagnostics,
        out int tried, IReadOnlyDictionary<string, Func<NameRequest, IEnumerable<Word>>> providers = null)
    {
        // 同一次取名只向候选提供者读取一次词表。
        var customPools = new Dictionary<string, Word[]>(StringComparer.Ordinal);
        var random = new NamingRandom(request.Seed);
        tried = 0;
        foreach (var candidate in candidates)
        {
            tried++;
            string surname = request.ResolveSurname(candidate.Inheritance);
            using var draw = candidate.OpenLayout == null ? DrawRule(candidate.Rule, surname) : candidate.OpenLayout(surname, diagnostics);
            if (draw == null) continue;
            var failure = draw.PreparationFailure;
            if (draw.Attempt != null)
                for (int attempt = 0; attempt < candidate.Attempts; attempt++)
                {
                    var trial = draw.Attempt(attempt);
                    if (trial.Name == null)
                    {
                        failure ??= trial.Failure;
                        continue;
                    }
                    var name = trial.Name;
                    string missing = request.FixedParts.FirstOrDefault(part =>
                        name.Parts.TryGetValue(part.Key, out string actual) ? actual != part.Value : part.Value.Length != 0).Key;
                    if (missing != null)
                    {
                        failure = (NameFailureReason.UnavailableRule, missing);
                        continue;
                    }
                    if (ResultFailure(type, name.Text, accept) is { } reason)
                    {
                        if (failure == null || reason == NameFailureReason.Rejected) failure = (reason, null);
                        continue;
                    }
                    return trace.Finish(name);
                }
            if (failure is { } actual)
                trace.Add(candidate.RuleId, candidate.ArrangementId, actual.Reason, actual.Reference, native: candidate.OpenLayout != null);
        }
        return null;

        CandidateDraw DrawRule(Rule rule, string surname)
        {
            var writing = request.Writing ?? rule.Writing;
            string separator = Separator(rule, writing);
            var order = rule.Parts.Select(part => part.Id).ToArray();
            if (request.FixedParts.FirstOrDefault(part => part.Value.Length != 0 && !order.Contains(part.Key)).Key is string lost)
                return new CandidateDraw(NameFailureReason.UnavailableRule, lost);

            return new CandidateDraw(_ =>
            {
                var parts = new Dictionary<string, string>(StringComparer.Ordinal);
                var sources = new Dictionary<string, NamePartSource>(StringComparer.Ordinal);
                foreach (var part in rule.Parts)
                {
                    bool fixedPart = request.FixedParts.TryGetValue(part.Id, out string value);
                    if (!fixedPart && part.Id == NameRequest.SurnamePart && surname != null && part.Chance > 0 && part.Literal != "")
                    {
                        value = surname;
                        fixedPart = true;
                    }
                    NamePartSource source = fixedPart ? new NamePartSource("inherited") : null;
                    if (!fixedPart && (part.Chance == 1 || random.Next() < part.Chance))
                        value = DrawPart(part, request, effective, writing, parts, customPools, providers, diagnostics,
                            ref random, out source, part.Id == NameRequest.SurnamePart ? request.SurnameUsage : null);
                    if (value == null && !part.Optional)
                    {
                        string detail = "part:" + rule.Id + "/" + part.Id;
                        if (!diagnostics.Contains(detail)) diagnostics.Add(detail);
                        return (null, PartFailure(part, source, effective));
                    }
                    parts.Add(part.Id, value ?? "");
                    if (source != null) sources.Add(part.Id, source);
                }
                string text = Join(order.Select(id => parts[id]), separator);
                return (new NameResult(GenerationStatus.Success, text, parts, diagnostics, order, sources, separator, ruleId: rule.Id), null);
            });
        }
    }

    internal static NameFailureReason? ResultFailure(NameType type, string text, Func<string, bool> accept)
    {
        if (!Ids.Text(text, type.MaxLength) || text.Trim().Length < type.MinLength) return NameFailureReason.InvalidResult;
        return accept != null && !accept(text) ? NameFailureReason.Rejected : null;
    }

    internal static NameFailureReason VocabularyFailure(EffectiveNames effective, string pool) =>
        effective.HasPool(pool) ? NameFailureReason.EmptyVocabulary : NameFailureReason.MissingVocabulary;

    internal static (NameFailureReason Reason, string Reference) PartFailure(Part part, NamePartSource source,
        EffectiveNames effective) => source?.Kind switch
    {
        "pool" when source.Reference != null => (VocabularyFailure(effective, source.Reference), source.Reference),
        "input" => (NameFailureReason.MissingInput, source.Reference),
        "provider" => (NameFailureReason.EmptyVocabulary, source.Reference),
        _ when part.PoolInput != null => (NameFailureReason.MissingInput, part.PoolInput),
        _ => (NameFailureReason.InvalidResult, part.Id)
    };

    internal static string Separator(Rule rule, NameWriting writing) =>
        (writing == NameWriting.Chinese ? rule.ChineseSeparator : rule.OriginalSeparator) ?? "";

    internal static string Join(IEnumerable<string> parts, string separator) =>
        string.Join(separator, parts.Where(value => !string.IsNullOrEmpty(value)));

    internal static string DrawPart(Part part, NameRequest request, EffectiveNames effective, NameWriting writing,
        Dictionary<string, string> parts, Dictionary<string, Word[]> customPools,
        IReadOnlyDictionary<string, Func<NameRequest, IEnumerable<Word>>> providers, List<string> diagnostics,
        ref NamingRandom random, out NamePartSource source, Func<string, int> surnameUsage = null)
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
            return DrawPool(part.FallbackPool, null, effective, writing, ref random, out source, surnameUsage);
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
            return DrawPool(pool, part.FallbackPool, effective, writing, ref random, out source, surnameUsage);
        }
        if (part.Provider != null && !customPools.TryGetValue(part.Provider, out _))
            customPools[part.Provider] = ReadProvider(part.Provider, providers, request, diagnostics);
        return RenderWord(part.Provider != null ? PickWord(customPools[part.Provider], ref random) : null,
            writing, "provider", part.Provider, out source);
    }

    internal static string PoolPrefix(string id) => id.EndsWith("-", StringComparison.Ordinal) ? id : id + ".";

    internal static string DrawPool(string pool, string fallback, EffectiveNames effective, NameWriting writing,
        ref NamingRandom random, out NamePartSource source, Func<string, int> surnameUsage = null)
    {
        pool = effective.PoolId(pool);
        var words = pool == null ? Array.Empty<Word>() : effective.GetPool(pool);
        if (words.Length == 0 && fallback != null)
        {
            pool = effective.PoolId(fallback);
            words = effective.GetPool(pool);
        }
        return RenderWord(PickPoolWord(words, writing, surnameUsage, ref random), writing, "pool", pool, out source);
    }

    internal static int SurnameUse(Word word, NameWriting writing, Func<string, int> usage) =>
        usage(writing == NameWriting.Chinese ? word.Chinese ?? word.Text : word.Text);

    internal static Word PickPoolWord(Word[] words, NameWriting writing, Func<string, int> usage, ref NamingRandom random)
    {
        if (usage == null) return PickWord(words, ref random);
        int lowest = int.MaxValue;
        var preferred = new List<Word>();
        foreach (var word in words)
        {
            int count = SurnameUse(word, writing, usage);
            if (count == int.MaxValue || count > lowest) continue;
            if (count < lowest) preferred.Clear();
            lowest = count;
            preferred.Add(word);
        }
        return PickWord(preferred.ToArray(), ref random);
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
