using System;
using System.Collections.Generic;
using System.Linq;

namespace Chinese_Name.Naming;

internal sealed class ArrangementLayout(IReadOnlyList<string> glyphs, Dictionary<string, string> groups)
{
    internal IReadOnlyList<string> Glyphs { get; } = glyphs;
    internal Dictionary<string, string> Groups { get; } = groups;

    internal string WordGroup(string id) => Groups.TryGetValue(id, out var text) ? text : "";

    internal ArrangementLayout Apply(DefaultArrangementEdit edit, IReadOnlyDictionary<string, string> roles = null,
        IReadOnlyDictionary<string, string> renames = null, Action<string> unresolved = null)
    {
        if (edit == null) return this;
        var updated = new Dictionary<string, string>(Groups, StringComparer.Ordinal);
        foreach (var group in edit.Groups) SetWordGroup(group.Key, group.Value);
        var bySlot = new Dictionary<string, List<KeyValuePair<string, string>>>(StringComparer.Ordinal);
        foreach (var part in edit.PartGroups)
        {
            string slot = SlotForPart(part.Key, roles, renames);
            if (slot == null) { unresolved?.Invoke(part.Key); continue; }
            if (!bySlot.TryGetValue(slot, out var choices)) bySlot.Add(slot, choices = new());
            choices.Add(part);
        }
        foreach (var group in bySlot)
        {
            if (group.Value.Count == 1) SetWordGroup(group.Key, group.Value[0].Value);
            else if (edit.PartGroups.TryGetValue(roles[group.Key], out string exact)) SetWordGroup(group.Key, exact);
            else foreach (var part in group.Value) unresolved?.Invoke(part.Key);
        }
        IReadOnlyList<string> order = (IReadOnlyList<string>)edit.Glyphs ?? Glyphs;
        if (edit.Glyphs != null && edit.GlyphParts.Count != 0)
        {
            var replaced = new List<string>();
            foreach (string glyph in edit.Glyphs)
            {
                string next = glyph;
                if (edit.GlyphParts.TryGetValue(glyph, out string part))
                {
                    next = SlotForPart(part, roles, renames);
                    if (next == null) unresolved?.Invoke(part);
                }
                replaced.Add(next);
            }
            if (replaced.All(slot => slot != null)) order = replaced;
            else order = Glyphs;
        }
        return new ArrangementLayout(order, updated);

        void SetWordGroup(string slot, string text)
        {
            if (string.IsNullOrEmpty(text)) updated.Remove(slot);
            else updated[slot] = text;
        }
    }

    internal DefaultArrangementEdit Changes(ArrangementLayout edited, DefaultArrangementEdit previous,
        IReadOnlyDictionary<string, string> roles = null, IReadOnlyDictionary<string, string> renames = null)
    {
        var current = Apply(previous, roles, renames);
        var change = previous == null ? new DefaultArrangementEdit() : NamingJson.Copy(previous);
        if (!current.Glyphs.SequenceEqual(edited.Glyphs))
        {
            change.Glyphs = Glyphs.SequenceEqual(edited.Glyphs) ? null : edited.Glyphs.ToList();
            change.GlyphParts.Clear();
            if (change.Glyphs != null && roles != null)
                foreach (string glyph in change.Glyphs.Distinct(StringComparer.Ordinal))
                    if (PartForSlot(glyph, roles) is string role) change.GlyphParts[glyph] = role;
        }
        foreach (string group in current.Groups.Keys.Concat(edited.Groups.Keys).Distinct(StringComparer.Ordinal))
        {
            string text = edited.WordGroup(group);
            if (text == current.WordGroup(group)) continue;
            change.Groups.Remove(group);
            string role = PartForSlot(group, roles);
            if (role != null)
                foreach (string prior in change.PartGroups.Keys.Where(part => SlotForPart(part, roles, renames) == group).ToArray())
                    change.PartGroups.Remove(prior);
            if (text == WordGroup(group)) continue;
            if (role == null) change.Groups[group] = text;
            else change.PartGroups[role] = text;
        }
        return change.IsEmpty ? null : change;
    }

    private static string PartForSlot(string slot, IReadOnlyDictionary<string, string> roles) =>
        roles != null && roles.TryGetValue(slot, out string role) && roles.Values.Count(part => part == role) == 1 ? role : null;

    private static string SlotForPart(string part, IReadOnlyDictionary<string, string> roles, IReadOnlyDictionary<string, string> renames)
    {
        if (roles == null) return null;
        string current = PackageMigrations.Resolve(part, renames, roles.Values.Contains);
        var slots = roles.Where(entry => entry.Value == current).Take(2).ToArray();
        return slots.Length == 1 ? slots[0].Key : null;
    }
}

internal sealed class ArrangementCodec(Func<string, string> glyphId, Func<string, string> shortCode)
{
    internal static readonly ArrangementCodec ShortCodes = new(code => code, id => id.Length == 1 ? id : null);

    internal ArrangementLayout Read(string template)
    {
        var fields = template?.Trim('\n', '\r', ' ', '"', '`').Split('|');
        if (fields?.Length != 2) return null;
        var groups = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string wordGroup in fields[1].Split(';'))
        {
            var parts = wordGroup.Split(':');
            if (parts.Length == 2 && parts[0].Length == 1 && parts[1].Length > 0)
                groups[Id(parts[0])] = parts[1];
        }
        return new ArrangementLayout(fields[0].Select(code => Id(code.ToString())).ToArray(), groups);
    }

    private string Id(string code) => glyphId(code) ?? code;

    internal string Write(ArrangementLayout layout)
    {
        var glyphs = layout.Glyphs.Select(Code).ToArray();
        var groups = layout.Groups.Select(group => (Code: Code(group.Key), group.Value)).ToArray();
        if (glyphs.Contains(null) || groups.Any(group => group.Code == null)) return null;
        return string.Concat(glyphs) + "|" + string.Join(";", groups.OrderBy(group => group.Code, StringComparer.Ordinal)
            .Select(group => group.Code + ":" + group.Value));
    }

    private string Code(string id) => shortCode(id) ?? (id.Length == 1 ? id : null);
}
