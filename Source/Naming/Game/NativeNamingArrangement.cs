using System;
using System.Collections.Generic;
using System.Linq;

namespace Chinese_Name.Naming.Game;

internal static class NativeNamingArrangement
{
    internal static bool Visible(Part part) => part.Chance != 0 && part.Literal != "";

    // 部件必须能无损对应到游戏实际执行的字形。
    internal static string[] Default(string type, Rule rule) => Default(type, rule, out _);

    internal static string[] Default(string type, Rule rule, out Dictionary<string, string> roles) =>
        Default(type, rule, out roles, out _, null);

    internal static OnomasticsData ReadPackaged(string type, CardNaming cards)
    {
        if (cards == null) return null;
        var data = new OnomasticsData();
        try
        {
            data.loadFromShortTemplate(cards.Template);
            NativeOnomastics.Validate(data, type, cards);
            return data;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            data.Dispose();
            return null;
        }
    }

    private static string[] Default(string type, Rule rule, out Dictionary<string, string> roles,
        out Dictionary<string, string> terms, EffectiveNames effective)
    {
        roles = new Dictionary<string, string>();
        terms = new Dictionary<string, string>();
        if (rule == null || !NativeOnomastics.Supports(type)) return null;
        using var packaged = ReadPackaged(type, rule.NativeCards);
        if (packaged != null)
        {
            roles = new Dictionary<string, string>(rule.NativeCards.Roles);
            return packaged.getFullTemplateData().ToArray();
        }
        return FromParts(type, rule, roles, terms, effective);
    }

    private static string[] FromParts(string type, Rule rule, Dictionary<string, string> roles,
        Dictionary<string, string> terms, EffectiveNames effective)
    {
        bool person = type == VanillaNames.Person;
        bool separated = !string.IsNullOrEmpty(rule.ChineseSeparator) || !string.IsNullOrEmpty(rule.OriginalSeparator);
        if (separated && !AssetManager.onomastics_library.dict.ContainsKey(NativeNamingGlyphs.RuleSeparator)) return null;
        var ids = new List<string>();
        var parts = rule.Parts.Where(Visible).ToArray();
        if (parts.Length > 10) return null;
        var reserved = new HashSet<string>(parts.Select(part => GroupForPart(person, part.Id)), StringComparer.Ordinal);
        foreach (var part in parts)
        {
            bool materialize = rule.UseNativeWordGroups && !part.Optional && part.Chance == 1;
            string id = (materialize ? NativeNamingGlyphs.ForInput(type, part)?.Id : null) ?? GroupForPart(person, part.Id);
            if (id == null && !person)
                id = Enumerable.Range(1, 10).Select(number => "group_" + number).FirstOrDefault(reserved.Add);
            if (id == null || roles.ContainsKey(id) || !AssetManager.onomastics_library.dict.ContainsKey(id) ||
                part.Copy != null || part.Provider != null)
                return null;
            roles.Add(id, part.Id);
            if (separated && (part.Optional || part.Chance != 1)) return null;
            if (separated && ids.Count != 0) ids.Add(NativeNamingGlyphs.RuleSeparator);
            ids.Add(id);
            if (materialize)
            {
                if (part.Pool != null && part.PoolInput == null)
                {
                    if (!AssetManager.onomastics_library.dict.ContainsKey(NativeNamingGlyphs.AsPool)) return null;
                    ids.Add(NativeNamingGlyphs.AsPool);
                    string token = part.Pool.Replace(':', '/');
                    if (effective != null && effective.PoolLabels.TryGetValue(part.Pool, out string label) &&
                        effective.ResolvePoolToken(label, out _) == part.Pool) token = label;
                    terms.Add(id, token);
                }
                else if (part.Literal != null && part.Literal.All(c => char.IsLetter(c) || c == '—')) terms.Add(id, part.Literal);
            }
            if (person && part.Id == VanillaNames.Surname)
            {
                if (!AssetManager.onomastics_library.dict.ContainsKey(NativeNamingGlyphs.KeepSurname)) return null;
                ids.Add(NativeNamingGlyphs.KeepSurname);
            }
        }
        return ids.Count is > 0 and <= OnomasticsData.MAX_NAME_LENGTH ? ids.ToArray() : null;
    }

    internal static bool FillDefault(OnomasticsData data, string type, Rule rule, EffectiveNames effective, out bool changed)
    {
        changed = false;
        if (rule == null || !NativeOnomastics.Supports(type)) return false;
        using var packaged = ReadPackaged(type, rule.NativeCards);
        if (packaged != null)
        {
            string template = packaged.getShortTemplate();
            changed = data.getShortTemplate() != template;
            if (changed) data.loadFromShortTemplate(template);
            return true;
        }
        var terms = new Dictionary<string, string>();
        var order = FromParts(type, rule, new Dictionary<string, string>(), terms, effective);
        if (order == null) return false;
        if (data.getFullTemplateData().SequenceEqual(order) && (!rule.UseNativeWordGroups ||
            data.groups.Count == terms.Count && terms.All(pair => data.getGroupString(pair.Key) == pair.Value))) return true;
        data.setTemplateData(order);
        data.groups.Clear();
        foreach (var term in terms) data.setGroup(term.Key, term.Value);
        changed = true;
        return true;
    }

    private static string GroupForPart(bool person, string part) => (person, part) switch
    {
        (false, "name") or (true, "surname") => "group_1",
        (false, "suffix") or (true, "first") => "group_2",
        (true, "given") => "group_3",
        (true, "middle") => "group_4",
        _ => null
    };

    // 旧存档只在能无损放入普通词组时转换。已有词组、绑定和词条都不能被占用。
    internal static bool TryUpgrade(string type, OnomasticsData data, Rule rule, CardNaming legacy, out CardNaming choice)
    {
        choice = legacy;
        if (legacy?.Enabled != true || rule == null || legacy.SourceRule != rule.Id) return false;
        var oldOrder = data.getFullTemplateData();
        if (!oldOrder.Any(id => NativeNamingGlyphs.Find(id) != null) ||
            oldOrder.Contains(NativeNamingGlyphs.KeepSurname) || legacy.SurnameGroup != null) return false;
        var occupied = oldOrder.Concat(data.groups.Keys).Concat(legacy.Roles.Keys).Concat(legacy.Pools.Keys).ToHashSet();
        var replacements = new Dictionary<string, string>();
        var bindings = new Dictionary<string, string>(legacy.Roles);
        foreach (string id in oldOrder.Distinct())
        {
            var glyph = NativeNamingGlyphs.Find(id);
            if (glyph == null) continue;
            if (glyph.Target != NativeOnomastics.Meta(type) || !rule.Parts.Any(part => part.Id == glyph.Part)) return false;
            string slot = Enumerable.Range(1, 10).Select(index => "group_" + index).FirstOrDefault(group => !occupied.Contains(group));
            if (slot == null) return false;
            occupied.Add(slot);
            replacements.Add(id, slot);
            bindings.Add(slot, glyph.Part);
        }

        var order = new List<string>(oldOrder.Count);
        using var old = new OnomasticsData();
        old.setTemplateData(oldOrder);
        bool explicitSeparators = legacy.ExplicitSeparators;
        if (ShowLegacySeparators(old, legacy, rule)) explicitSeparators = true;
        else if (!explicitSeparators && oldOrder.Contains(NativeNamingGlyphs.RuleSeparator)) explicitSeparators = true;
        foreach (string id in old.getFullTemplateData())
        {
            order.Add(replacements.TryGetValue(id, out var slot) ? slot : id);
            if (NativeNamingGlyphs.Find(id)?.Part == VanillaNames.Surname) order.Add(NativeNamingGlyphs.KeepSurname);
        }
        if (order.Count > OnomasticsData.MAX_NAME_LENGTH) return false;
        if (!explicitSeparators && (!string.IsNullOrEmpty(rule.ChineseSeparator) || !string.IsNullOrEmpty(rule.OriginalSeparator)) &&
            !oldOrder.Any(id => id != "divider" && NativeNamingGlyphs.Find(id) == null && !bindings.ContainsKey(id) &&
                !AssetManager.onomastics_library.get(id).isGroupType())) return false;
        var migrated = NamingJson.Copy(legacy);
        migrated.Roles = bindings;
        migrated.ExplicitSeparators = explicitSeparators;
        old.setTemplateData(order);
        try { NativeOnomastics.Validate(old, type, migrated); }
        catch (InvalidOperationException) { return false; }
        data.setTemplateData(order);
        choice = migrated;
        return true;
    }

    // 旧的纯部件排列把分隔符藏在规则里。将它补进同一个盘面，保留原文与译名的写法。
    internal static bool ShowLegacySeparators(OnomasticsData data, CardNaming choice, Rule rule)
    {
        if (choice?.Enabled != true || choice.ExplicitSeparators || rule == null ||
            string.IsNullOrEmpty(rule.ChineseSeparator) && string.IsNullOrEmpty(rule.OriginalSeparator)) return false;
        if (!AssetManager.onomastics_library.dict.ContainsKey(NativeNamingGlyphs.RuleSeparator)) return false;
        var layout = data.getFullTemplateData();
        if (layout.Count < 2 || layout.Count * 2 - 1 > OnomasticsData.MAX_NAME_LENGTH) return false;
        var used = new HashSet<string>();
        foreach (string id in layout)
        {
            if (id == NativeNamingGlyphs.KeepSurname) return false;
            string role = NativeNamingGlyphs.Find(id)?.Part;
            if (role == null) choice.Roles.TryGetValue(id, out role);
            var part = rule.Parts.FirstOrDefault(item => item.Id == role);
            if (part == null || part.Optional || part.Chance != 1 || !used.Add(role)) return false;
        }
        var expanded = layout.SelectMany((id, index) =>
            index == 0 ? new[] { id } : new[] { NativeNamingGlyphs.RuleSeparator, id }).ToArray();
        data.setTemplateData(expanded);
        return true;
    }

    internal static CardNaming ForRule(string type, Rule rule)
    {
        using var packaged = ReadPackaged(type, rule.NativeCards);
        if (packaged != null)
        {
            var choice = NamingJson.Copy(rule.NativeCards);
            choice.SourceRule = rule.Id;
            choice.Inheritance = rule.Inheritance;
            return choice;
        }
        var roles = new Dictionary<string, string>();
        FromParts(type, rule, roles, new Dictionary<string, string>(), null);
        return new CardNaming
        {
            Enabled = true,
            SourceRule = NativeOnomastics.Meta(type) != null || rule.UseNativeWordGroups ? rule.Id : null,
            Inheritance = rule.Inheritance,
            ExplicitSeparators = true,
            Roles = roles
        };
    }
}
