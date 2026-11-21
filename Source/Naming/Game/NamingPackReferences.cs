using System;
using System.Collections.Generic;
using System.Linq;

namespace Chinese_Name.Naming.Game;

internal static class NamingPackReferences
{
    internal static IReadOnlyList<string> Inspect(NamingService service)
    {
        var catalog = service.PackageCatalog();
        var types = service.Types.Select(type => type.Id).ToHashSet();
        var pools = catalog.Values.SelectMany(pack => pack.Pools.Keys.Concat(pack.PoolPatches.Select(patch => patch.Pool))).ToHashSet();
        var aliases = catalog.Values.SelectMany(pack => pack.PoolAliases)
            .ToLookup(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var issues = new List<string>();
        var migrations = PackageMigrations.Pools(catalog.Values, pools, issues);
        foreach (var pack in catalog.Values.OrderBy(pack => pack.Id, StringComparer.Ordinal))
        {
            foreach (var entry in pack.Rules.Where(entry => types.Contains(entry.Key)))
                foreach (var rule in entry.Value)
                {
                    var scope = pack.Id + " / " + entry.Key + " / " + rule.Id;
                    foreach (var part in rule.Parts)
                    {
                        if (part.Pool != null && part.PoolInput == null) CheckPool(scope, part.Pool);
                        if (part.FallbackPool != null) CheckPool(scope, part.FallbackPool);
                    }
                    CheckCards(scope, entry.Key, rule.NativeCards);
                }
            foreach (var entry in pack.Arrangements.Where(entry => types.Contains(entry.Key)))
                foreach (var item in entry.Value)
                    CheckCards(pack.Id + " / " + entry.Key + " / " + item.Id, entry.Key, item.Cards);
            foreach (var entry in pack.CultureVariants.Where(entry => types.Contains(entry.Value.Type)))
                CheckCards(pack.Id + " / " + entry.Key, entry.Value.Type, entry.Value.Arrangement?.Cards);
        }
        var world = service.ActiveWorld;
        foreach (var culture in world.Cultures)
        {
            var settings = culture.Value;
            var targets = settings.ArrangementEdits.Keys.Concat(settings.Arrangements.Keys).Concat(settings.Cards.Keys)
                .Concat(settings.DefaultArrangementEdits.Keys.Select(Category)).Concat(settings.Rules.Keys.Select(Category))
                .Where(types.Contains).Distinct(StringComparer.Ordinal);
            foreach (string type in targets)
            {
                var names = service.Inspect(new NameRequest(type, 0, culture.Key), world);
                string scope = "文化 " + culture.Key + " / " + type;
                if (settings.Cards.TryGetValue(type, out var cards) && cards.Enabled && cards.Template != null)
                    CheckCards(scope, type, cards);
                foreach (var row in names.Arrangements)
                    CheckCards(scope + " / " + row.Id, type, row.Cards);
                foreach (string issue in names.Diagnostics)
                    if (issue.StartsWith("unmapped-arrangement-part:", StringComparison.Ordinal) ||
                        issue.StartsWith("missing-part:", StringComparison.Ordinal) || issue.StartsWith("rename-conflict:", StringComparison.Ordinal))
                        issues.Add(scope + " · 保留无法迁移的部件编辑 " + issue.Substring(issue.IndexOf(':') + 1));
                foreach (var rule in names.AvailableRules)
                    if (settings.DefaultArrangementEdits.TryGetValue(CultureNaming.DefaultArrangementKey(type, names.ArrangementBasis, rule.Id), out var edit))
                        NativeNamingArrangement.ApplyDefaultEdit(type, rule, names, edit,
                            part => issues.Add(scope + " / " + rule.Id + " · 保留无法迁移的部件编辑 " + part));
            }
        }
        return issues.Distinct(StringComparer.Ordinal).ToArray();

        string Category(string key) => key.Split('/')[0];
        bool Available(string pool) => pools.Contains(PackageMigrations.CurrentId(pool, migrations.Ids));

        void CheckPool(string scope, string pool)
        {
            if (!Available(pool)) issues.Add(scope + " · 缺少词库 " + pool);
        }

        void CheckCards(string scope, string type, CardNaming cards)
        {
            if (cards == null) return;
            foreach (string pool in cards.Pools.Values) CheckPool(scope, pool);
            using var data = NativeNamingArrangement.ReadPackaged(type, cards, out var failure);
            if (data == null)
            {
                string reason = failure.Reason == NameFailureReason.MissingGlyph ? "缺少字形 " : "排列无效 ";
                issues.Add(scope + " · " + reason + failure.Reference);
                return;
            }
            foreach (string token in NativeOnomastics.SelectorTokens(data))
            {
                string id = token.Replace('/', ':');
                if (Ids.Qualified(id)) CheckPool(scope, id);
                else if (!aliases[token].Any(Available)) issues.Add(scope + " · 未找到词库名称「" + token + "」");
            }
        }
    }
}
