using System;
using System.Collections.Generic;
using System.Linq;

namespace Chinese_Name.Naming;

internal static class ArrangementExamples
{
    internal static bool TryApply(NamingService service, NameType type, ConditionalArrangement arrangement,
        Dictionary<string, object> sample)
    {
        if (!service.CanMatch(arrangement, type)) return false;
        var choices = arrangement.Expression.Leaves().GroupBy(term => term.Field)
            .Select(terms => (Field: terms.Key, Candidates: Candidates(service, type, terms, sample).ToArray())).ToArray();
        var assigned = new Dictionary<string, object>(StringComparer.Ordinal);
        int remaining = 4096;
        if (!Search(0)) return false;
        foreach (var entry in assigned)
        {
            if (entry.Value == null) sample.Remove(entry.Key);
            else sample[entry.Key] = entry.Value;
        }
        return true;

        bool Search(int index)
        {
            if (remaining-- <= 0) return false;
            var outcome = arrangement.Expression.Evaluate(term => assigned.TryGetValue(term.Field, out var value)
                ? service.MatchesCondition(type.Id, term.Field, term, NameEngine.Value(value)) : (bool?)null);
            if (outcome.HasValue) return outcome.Value;
            if (index >= choices.Length) return false;
            var (field, candidates) = choices[index];
            foreach (object candidate in candidates)
            {
                assigned[field] = candidate;
                if (Search(index + 1)) return true;
            }
            assigned.Remove(field);
            return false;
        }
    }

    internal static object Input(InputField field, string value) => field.Kind switch
    {
        InputKind.Integer => NamingService.Number(value),
        InputKind.Flag => bool.TryParse(value, out bool flag) ? flag : null,
        _ => value
    };

    private static IEnumerable<object> Candidates(NamingService service, NameType type,
        IGrouping<string, ArrangementCondition> terms, Dictionary<string, object> sample)
    {
        var field = type.Fields[terms.Key];
        sample.TryGetValue(terms.Key, out var current);
        bool registered = service.Conditions(type.Id).TryGetValue(terms.Key, out var options);
        var operands = terms
            .Where(term => term.Test != ArrangementTest.Registered || !registered || !options.Presence)
            .SelectMany(term => term.Values).Distinct(StringComparer.Ordinal).ToArray();
        var values = new List<object> { current };
        if (field.Kind == InputKind.Text)
        {
            if (registered && !options.Presence) values.AddRange(options.Values.Keys);
            values.AddRange(operands);
            var starts = terms.Where(term => term.Test == ArrangementTest.Prefix).SelectMany(term => term.Values).Distinct();
            var ends = terms.Where(term => term.Test == ArrangementTest.Suffix).SelectMany(term => term.Values).Distinct();
            var middle = string.Concat(terms.Where(term => term.Test == ArrangementTest.Contains && !term.Negated)
                .SelectMany(term => term.Values).Distinct().Take(24));
            foreach (string prefix in starts.Prepend("").Take(24))
                foreach (string suffix in ends.Prepend("").Take(24))
                    if (prefix.Length + middle.Length + suffix.Length <= 256) values.Add(prefix + middle + suffix);
            values.Add("示例");
        }
        else if (field.Kind == InputKind.Integer)
        {
            values.Add(0);
            foreach (int bound in operands.Select(NamingService.Number).Where(value => value.HasValue).Select(value => value.Value))
            {
                values.Add(bound);
                if (bound < int.MaxValue) values.Add(bound + 1);
                if (bound > int.MinValue) values.Add(bound - 1);
            }
        }
        else
        {
            values.Add(true);
            values.Add(false);
        }
        if (!field.Required) values.Add(null);
        return values.Where(value => value == null ? !field.Required :
            field.Accepts(value) && (value is not string text || !string.IsNullOrWhiteSpace(text))).Distinct();
    }
}
