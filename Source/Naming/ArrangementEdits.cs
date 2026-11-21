using System;
using System.Collections.Generic;
using System.Linq;

namespace Chinese_Name.Naming;

public sealed class ArrangementEdit
{
    public bool Removed { get; set; }
    public bool? Disabled { get; set; }
    public ArrangementCondition Expression { get; set; }
    // Full-template edits from earlier versions retain their original meaning.
    public string Template { get; set; }
    public DefaultArrangementEdit Layout { get; set; }

    internal bool IsEmpty => !Removed && Disabled == null && Expression == null && Template == null && Layout == null;
}

public sealed class ArrangementEdits
{
    public Dictionary<string, ArrangementEdit> Changes { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, ConditionalArrangement> Added { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, List<string>> Orders { get; set; } = new(StringComparer.Ordinal);

    internal bool IsEmpty => Changes.Count == 0 && Added.Count == 0 && Orders.Count == 0;

    internal IReadOnlyList<ConditionalArrangement> Apply(string basis, IReadOnlyList<ConditionalArrangement> defaults,
        IReadOnlyDictionary<string, (string Key, string LegacyKey)> owners, ArrangementCodec codec,
        Func<string, IReadOnlyDictionary<string, string>> renames = null, List<string> diagnostics = null)
    {
        var items = new List<ConditionalArrangement>();
        foreach (var source in defaults)
        {
            if (Added.ContainsKey(source.Id)) continue;
            var owner = owners[source.Id];
            if (!Changes.TryGetValue(owner.Key, out var change) &&
                (owner.LegacyKey == null || !Changes.TryGetValue(owner.LegacyKey, out change)))
            {
                items.Add(source);
                continue;
            }
            if (change.Removed) continue;
            var item = NamingJson.Copy(source);
            if (change.Disabled.HasValue) item.Disabled = change.Disabled.Value;
            if (change.Expression != null) item.Expression = change.Expression;
            if (change.Template != null) item.Cards.Template = change.Template;
            else if (change.Layout != null && codec.Read(source.Cards.Template) is { } layout)
                item.Cards.Template = codec.Write(layout.Apply(change.Layout, source.Cards.Roles, renames?.Invoke(owner.Key),
                    part => diagnostics?.Add("unmapped-arrangement-part:" + owner.Key + "/" + part))) ?? source.Cards.Template;
            items.Add(item);
        }
        items.AddRange(Added.Values);
        if (!Orders.TryGetValue(basis, out var order)) return items;

        var byId = items.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var idsByOwner = items.Where(item => owners.ContainsKey(item.Id) && !Added.ContainsKey(item.Id))
            .ToDictionary(item => owners[item.Id].Key, item => item.Id, StringComparer.Ordinal);
        var ordered = order.Select(id => idsByOwner.TryGetValue(id, out var current) ? current : id)
            .Where(byId.ContainsKey).Distinct(StringComparer.Ordinal).ToArray();
        if (ordered.Length == 0) return items;
        var pinned = new HashSet<string>(ordered, StringComparer.Ordinal);
        var runs = new Dictionary<string, List<ConditionalArrangement>>(StringComparer.Ordinal);
        var unplaced = new List<ConditionalArrangement>();
        // 包更新加进来的排列跟随基础顺序，插到下一个仍可见的锚点之前。
        foreach (var item in items)
        {
            if (pinned.Contains(item.Id))
            {
                runs[item.Id] = unplaced;
                unplaced = new();
            }
            else unplaced.Add(item);
        }
        return ordered.SelectMany(id => runs[id].Append(byId[id])).Concat(unplaced).ToArray();
    }

    internal void Update(EffectiveNames before, IReadOnlyList<ConditionalArrangement> after, bool reorder)
    {
        string basis = before.ArrangementBasis;
        var remaining = after.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var defaults = before.BaseArrangements.ToDictionary(item => item.Id, StringComparer.Ordinal);
        foreach (var previous in before.Arrangements)
        {
            string id = previous.Id;
            before.ArrangementOwners.TryGetValue(id, out var owner);
            string key = owner.Key ?? id;
            if (!remaining.Remove(id, out var next))
            {
                if (!Added.Remove(id))
                {
                    if (owner.LegacyKey != null) Changes.Remove(owner.LegacyKey);
                    Changes[key] = new ArrangementEdit { Removed = true };
                }
                continue;
            }
            if (Added.ContainsKey(id))
            {
                Added[id] = NamingJson.Copy(next);
                continue;
            }
            if (!defaults.TryGetValue(id, out var source)) continue;
            Changes.TryGetValue(key, out var change);
            if (owner.LegacyKey != null && Changes.Remove(owner.LegacyKey, out var legacy))
                change ??= legacy;
            change ??= new ArrangementEdit();
            if (next.Disabled != previous.Disabled)
                change.Disabled = next.Disabled == source.Disabled ? null : next.Disabled;
            if (next.Cards.Template != previous.Cards.Template)
            {
                var codec = before.ArrangementCodec;
                if (change.Template == null && codec.Read(source.Cards.Template) is { } baseline &&
                    codec.Read(next.Cards.Template) is { } edited)
                    change.Layout = baseline.Changes(edited, change.Layout, source.Cards.Roles, before.ArrangementPartRenames(key));
                else
                    change.Template = next.Cards.Template == source.Cards.Template ? null : next.Cards.Template;
            }
            string expression = NamingJson.Write(next.Expression);
            if (expression != NamingJson.Write(previous.Expression))
                change.Expression = expression == NamingJson.Write(source.Expression) ? null : NamingJson.Copy(next.Expression);
            if (change.IsEmpty) Changes.Remove(key);
            else Changes[key] = change;
        }
        foreach (var item in remaining.Values) Added[item.Id] = NamingJson.Copy(item);
        if (reorder)
        {
            var order = after.Select(item => !Added.ContainsKey(item.Id) && before.ArrangementOwners.TryGetValue(item.Id, out var owner)
                ? owner.Key : item.Id).ToList();
            Orders.Remove(basis);
            if (!Apply(basis, before.BaseArrangements, before.ArrangementOwners, before.ArrangementCodec, before.ArrangementPartRenames).Select(item => item.Id)
                .SequenceEqual(after.Select(item => item.Id))) Orders[basis] = order;
        }
    }
}
