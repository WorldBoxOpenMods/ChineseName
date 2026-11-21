using System;
using System.Collections.Generic;
using System.Linq;

namespace Chinese_Name.Naming;

/// <summary>Add inputs and editor tools to a naming category.</summary>
public class NamingCategoryExtension(string type)
{
    public string TypeId { get; } = type ?? throw new ArgumentNullException(nameof(type));
    /// <summary>Adjust related inputs when the editor constructs a condition preview.</summary>
    public Action<Dictionary<string, object>> PrepareExample { get; set; }
    /// <summary>Provide a sprite path for a condition field or value. The value is null for the field itself.</summary>
    public Func<string, string, string> ConditionIcon { get; set; }
    internal List<InputField> Inputs { get; } = new();
    internal List<NamingService.CultureCondition> Conditions { get; } = new();
    internal List<CultureNameGlyph> Glyphs { get; } = new();

    /// <summary>Declare an input that callers or the game integration can supply.</summary>
    public void Input(InputField field) => Inputs.Add(field ?? throw new ArgumentNullException(nameof(field)));

    /// <summary>List the stable values a player can choose in the condition palette.</summary>
    public void Values(string field, string label, IReadOnlyDictionary<string, string> values) =>
        Conditions.Add(ValueCondition(field, label, values));

    /// <summary>Expose a test for whether the request supplies a field.</summary>
    public void Presence(string field, string label, string present, string absent) =>
        Conditions.Add(PresenceCondition(field, label, present, absent));

    /// <summary>Add a card that reads a field directly or selects a pool from its value.</summary>
    public void InputGlyph(string id, char shortCode, string field, string label, string icon,
        string poolPrefix = null, string description = null, string fallbackPool = null) =>
        Glyphs.Add(new CultureNameGlyph(TypeId, id, shortCode, field, label, icon, poolPrefix,
            description ?? "取当前对象的" + label + "。", fallbackPool));

    internal void PublishedInputGlyph(string id, char code, string field, string label, string icon, string description) =>
        Glyphs.Add(new CultureNameGlyph(TypeId, id, code, field, label, icon, null, description, publishedLocalId: true));

    internal static NamingService.CultureCondition ValueCondition(string field, string label,
        IReadOnlyDictionary<string, string> values)
    {
        if (values == null || values.Count == 0 || values.Any(value => string.IsNullOrWhiteSpace(value.Key) ||
                string.IsNullOrWhiteSpace(value.Value)))
            throw new ArgumentException("Supply stable values and their labels.", nameof(values));
        return new NamingService.CultureCondition(field, label,
            values.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal), false);
    }

    internal static NamingService.CultureCondition PresenceCondition(string field, string label, string present, string absent) =>
        new(field, label, new Dictionary<string, string> { ["present"] = present, ["absent"] = absent }, true);
}

/// <summary>Declares a new category, its optional default content, and its tools in culture onomastics.</summary>
public sealed class NamingCategory(NameType type) : NamingCategoryExtension(type?.Id ?? throw new ArgumentNullException(nameof(type)))
{
    public NameType Type { get; } = type ?? throw new ArgumentNullException(nameof(type));
    public NamingPack Defaults { get; set; }
    public string CultureIcon { get; set; }
    public string Description { get; set; }
    public Func<Dictionary<string, object>, bool> PrepareContext { get; set; }
    internal string TabId { get; set; }
    internal string TabLabel { get; set; }
    internal int TabOrder { get; set; } = int.MaxValue;
    internal string ExampleKind { get; set; }
    internal bool WorldScoped { get; set; }
    internal bool ShareDefaultLayout { get; set; }
}
