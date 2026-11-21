using System;
using System.Collections.Generic;
using System.Linq;

namespace Chinese_Name.Naming;

internal static class PackageMigrations
{
    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>(StringComparer.Ordinal);

    internal static IReadOnlyDictionary<string, string> Parts(NamingPack pack, string type) =>
        pack?.PartRenames.TryGetValue(type, out var renames) == true ? renames : Empty;

    internal static IReadOnlyDictionary<string, string> Collect(IEnumerable<KeyValuePair<string, string>> declarations,
        string kind, List<string> diagnostics)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in declarations.GroupBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var targets = entry.Select(pair => pair.Value).Distinct(StringComparer.Ordinal).ToArray();
            if (targets.Length == 1) result[entry.Key] = targets[0];
            else diagnostics.Add("rename-conflict:" + kind + "/" + entry.Key);
        }
        return result;
    }

    internal static string Resolve(string id, IReadOnlyDictionary<string, string> renames, Func<string, bool> exists)
    {
        if (exists(id)) return id;
        if (renames == null || !renames.ContainsKey(id)) return null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (seen.Add(id) && renames.TryGetValue(id, out string next))
        {
            if (exists(next)) return next;
            id = next;
        }
        return null;
    }

    internal static Bundle Pools(IEnumerable<NamingPack> packs, HashSet<string> available, List<string> diagnostics)
    {
        var declared = Collect(packs.SelectMany(pack => pack.PoolRenames), "pool", diagnostics);
        var effective = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var prior in declared.Keys)
        {
            if (available.Contains(prior)) continue;
            string target = Resolve(prior, declared, available.Contains);
            if (target != null) effective.Add(prior, target);
            else diagnostics.Add("missing-rename-target:pool/" + prior);
        }
        return new Bundle(effective);
    }

    internal static string CurrentId(string id, IReadOnlyDictionary<string, string> renames) =>
        id != null && renames != null && renames.TryGetValue(id, out var current) ? current : id;

    internal static bool Valid(IReadOnlyDictionary<string, string> renames, Func<string, bool> validId)
    {
        if (renames == null || renames.Any(pair => !validId(pair.Key) || !validId(pair.Value))) return false;
        return renames.Keys.All(key => Resolve(key, renames, id => !renames.ContainsKey(id)) != null);
    }

    internal sealed class Bundle(IReadOnlyDictionary<string, string> ids)
    {
        internal IReadOnlyDictionary<string, string> Ids { get; } = ids;

        internal LanguageNaming Language(LanguageNaming original, List<string> diagnostics)
        {
            if (original == null || Ids.Count == 0) return original;
            return new LanguageNaming
            {
                Pack = original.Pack, BiasOrigin = original.BiasOrigin,
                Pools = View(original.Pools, "words", diagnostics),
                Groups = View(original.Groups, "preferred-group", diagnostics),
                GroupPreferences = View(original.GroupPreferences, "group-weights", diagnostics)
            };
        }

        internal void AdoptPool(LanguageNaming profile, string target)
        {
            Adopt(profile.Pools, target);
            Adopt(profile.Groups, target);
            Adopt(profile.GroupPreferences, target);
        }

        private void Adopt<T>(Dictionary<string, T> entries, string target)
        {
            var previous = entries.Where(pair => pair.Key != target && CurrentId(pair.Key, Ids) == target).ToArray();
            if (previous.Length != 1) return;
            if (!entries.ContainsKey(target)) entries.Add(target, previous[0].Value);
            foreach (var old in previous) entries.Remove(old.Key);
        }

        private Dictionary<string, T> View<T>(Dictionary<string, T> entries, string kind, List<string> diagnostics)
        {
            if (!entries.Keys.Any(Ids.ContainsKey)) return entries;
            var mapped = entries.Where(pair => !Ids.ContainsKey(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            foreach (var group in entries.Where(pair => Ids.ContainsKey(pair.Key)).GroupBy(pair => Ids[pair.Key], StringComparer.Ordinal))
            {
                if (mapped.ContainsKey(group.Key)) continue;
                var choices = group.Select(pair => pair.Value).ToArray();
                if (choices.Length == 1) mapped.Add(group.Key, choices[0]);
                else diagnostics.Add("rename-conflict:" + kind + "/" + group.Key);
            }
            return mapped;
        }
    }
}
