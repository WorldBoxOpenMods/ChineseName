using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace Chinese_Name.Naming;

public enum InputKind { Text, Integer, Flag }
public enum GenerationStatus { Success, UnknownType, InvalidInput, NoRule, Exhausted, ContextError }
public enum NameWriting { Original, Chinese }
public enum NameFailureReason
{
    MissingGlyph, MissingInput, MissingVocabulary, AmbiguousVocabulary, EmptyVocabulary, UnavailableRule,
    InvalidArrangement, InvalidResult, Rejected, InapplicableInput
}

public sealed class InputField
{
    public InputField(string id, InputKind kind, bool required = false, string label = null, object example = null)
    {
        Id = id;
        Kind = kind;
        Required = required;
        Label = label ?? id;
        if (example != null && !Accepts(example)) throw new ArgumentException("Example does not match the input kind.", nameof(example));
        Example = example;
    }

    public string Id { get; }
    public InputKind Kind { get; }
    public bool Required { get; }
    public string Label { get; }
    public object Example { get; }

    internal bool Accepts(object value) => Kind switch
    {
        InputKind.Text => value is string s && s.Length <= 256,
        InputKind.Integer => value is int,
        InputKind.Flag => value is bool,
        _ => false
    };
}

public sealed class NameType
{
    public NameType(string id, string label, IEnumerable<InputField> fields = null,
        int minLength = 1, int maxLength = 32, IEnumerable<string> fixedParts = null)
    {
        if (!Ids.Qualified(id)) throw new ArgumentException("Use a namespace:type ID.", nameof(id));
        if (minLength < 1 || maxLength < minLength || maxLength > 128)
            throw new ArgumentOutOfRangeException(nameof(maxLength));
        Id = id;
        Label = label ?? id;
        var inputs = (fields ?? Array.Empty<InputField>()).ToArray();
        if (inputs.Any(f => f == null || !Ids.Local(f.Id) || !Enum.IsDefined(typeof(InputKind), f.Kind)))
            throw new ArgumentException("Invalid input field.", nameof(fields));
        Fields = new ReadOnlyDictionary<string, InputField>(inputs.ToDictionary(f => f.Id, StringComparer.Ordinal));
        FixedParts = Array.AsReadOnly((fixedParts ?? Array.Empty<string>()).ToArray());
        if (FixedParts.Any(idPart => !Ids.Local(idPart)) || FixedParts.Distinct().Count() != FixedParts.Count)
            throw new ArgumentException("Invalid fixed part IDs.", nameof(fixedParts));
        MinLength = minLength;
        MaxLength = maxLength;
    }

    public string Id { get; }
    public string Label { get; }
    public IReadOnlyDictionary<string, InputField> Fields { get; }
    public IReadOnlyList<string> FixedParts { get; }
    public int MinLength { get; }
    public int MaxLength { get; }
}

public sealed class NameRequest
{
    internal const string SurnamePart = "surname";

    public NameRequest(string type, ulong seed, long? culture = null, long? language = null,
        IReadOnlyDictionary<string, object> inputs = null, IReadOnlyDictionary<string, string> fixedParts = null,
        NameWriting? writing = null)
    {
        Type = type;
        Seed = seed;
        Culture = culture;
        Language = language;
        Inputs = Copy(inputs);
        FixedParts = Copy(fixedParts);
        Writing = writing;
    }

    public string Type { get; }
    public ulong Seed { get; }
    public long? Culture { get; }
    public long? Language { get; }
    public IReadOnlyDictionary<string, object> Inputs { get; }
    // An empty string is an explicit decision to omit the part.
    public IReadOnlyDictionary<string, string> FixedParts { get; }
    public NameWriting? Writing { get; }
    internal string FatherSurname { get; private set; }
    internal string MotherSurname { get; private set; }
    internal NamingService ContextOwner { get; private set; }
    internal Func<string, int> SurnameUsage { get; private set; }

    /// <summary>Provide known parental surnames for candidates that use surname inheritance.</summary>
    public NameRequest WithParentSurnames(string father, string mother)
    {
        if (father == FatherSurname && mother == MotherSurname) return this;
        if (father != null && !Ids.Text(father, 128)) throw new ArgumentException("Invalid surname.", nameof(father));
        if (mother != null && !Ids.Text(mother, 128)) throw new ArgumentException("Invalid surname.", nameof(mother));
        return WithSeed(Seed, father, mother);
    }

    internal NameRequest WithSeed(ulong seed) => WithSeed(seed, FatherSurname, MotherSurname);

    internal NameRequest ForSurnameFounding(Func<string, int> usage) =>
        new NameRequest(this, Seed) { SurnameUsage = usage };

    private NameRequest WithSeed(ulong seed, string father, string mother) =>
        new NameRequest(this, seed)
        {
            FatherSurname = father,
            MotherSurname = mother
        };

    private NameRequest(NameRequest source, ulong seed)
    {
        Type = source.Type;
        Seed = seed;
        Culture = source.Culture;
        Language = source.Language;
        Inputs = source.Inputs;
        FixedParts = source.FixedParts;
        Writing = source.Writing;
        ContextOwner = source.ContextOwner;
        SurnameUsage = source.SurnameUsage;
    }

    internal NameRequest WithPreparedInputs(NamingService service, IReadOnlyDictionary<string, object> inputs) =>
        new NameRequest(Type, Seed, Culture, Language, inputs, FixedParts, Writing)
        {
            FatherSurname = FatherSurname,
            MotherSurname = MotherSurname,
            ContextOwner = service,
            SurnameUsage = SurnameUsage
        };

    internal string ResolveSurname(SurnameOrder order)
    {
        if (FixedParts.TryGetValue(SurnamePart, out string value)) return value;
        if (order == SurnameOrder.New) return null;
        bool motherFirst = order == SurnameOrder.Mother || order == SurnameOrder.Random && new NamingRandom(Seed).Next() < 0.5;
        var first = motherFirst ? MotherSurname : FatherSurname;
        var second = motherFirst ? FatherSurname : MotherSurname;
        return !string.IsNullOrEmpty(first) ? first : string.IsNullOrEmpty(second) ? null : second;
    }

    private static IReadOnlyDictionary<string, T> Copy<T>(IReadOnlyDictionary<string, T> source) =>
        new ReadOnlyDictionary<string, T>((source ?? new Dictionary<string, T>())
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
}

internal sealed class CultureNameGlyph
{
    internal readonly string Type, Id, Field, Label, Icon, PoolPrefix, Description, FallbackPool;
    internal readonly char ShortCode;
    internal readonly bool PublishedLocalId;

    internal CultureNameGlyph(string type, string id, char shortCode, string field, string label, string icon,
        string poolPrefix, string description, string fallbackPool = null, bool publishedLocalId = false)
    {
        Type = type;
        Id = id;
        ShortCode = shortCode;
        Field = field;
        Label = label;
        Icon = icon;
        PoolPrefix = poolPrefix;
        Description = description;
        FallbackPool = fallbackPool;
        PublishedLocalId = publishedLocalId;
    }
}

public sealed class NameResult
{
    internal NameResult(GenerationStatus status, string text, Dictionary<string, string> parts,
        IEnumerable<string> diagnostics, IEnumerable<string> partOrder = null,
        Dictionary<string, NamePartSource> sources = null, string separator = null, IReadOnlyList<string> cardBranch = null,
        string ruleId = null, string arrangementId = null)
    {
        Status = status;
        Text = text;
        Parts = new ReadOnlyDictionary<string, string>(parts ?? new Dictionary<string, string>());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        PartOrder = Array.AsReadOnly((partOrder ?? Array.Empty<string>()).ToArray());
        Sources = new ReadOnlyDictionary<string, NamePartSource>(sources ?? new Dictionary<string, NamePartSource>());
        Separator = separator;
        CardBranch = cardBranch;
        RuleId = ruleId;
        ArrangementId = arrangementId;
    }

    public GenerationStatus Status { get; }
    public bool Success => Status == GenerationStatus.Success;
    public string Text { get; }
    public IReadOnlyDictionary<string, string> Parts { get; }
    public IReadOnlyList<string> PartOrder { get; }
    public IReadOnlyList<string> Diagnostics { get; private set; }
    public IReadOnlyDictionary<string, NamePartSource> Sources { get; }
    public string RuleId { get; }
    public string ArrangementId { get; }
    public IReadOnlyList<NameFailure> Failures { get; private set; } = Array.Empty<NameFailure>();
    internal string Separator { get; }
    internal IReadOnlyList<string> CardBranch { get; }

    internal void SetFailures(IEnumerable<NameFailure> failures, bool nativeFallback)
    {
        Failures = Array.AsReadOnly(failures.ToArray());
        if (nativeFallback) Diagnostics = Array.AsReadOnly(Diagnostics.Append("中文规则回退").Distinct().ToArray());
    }
}

public sealed class NameFailure
{
    internal NameFailure(string ruleId, string arrangementId, NameFailureReason reason, string reference)
    {
        RuleId = ruleId;
        ArrangementId = arrangementId;
        Reason = reason;
        Reference = reference;
    }

    public string RuleId { get; }
    public string ArrangementId { get; }
    public NameFailureReason Reason { get; }
    public string Reference { get; }
}

public sealed class NamePartSource
{
    internal NamePartSource(string kind, string reference = null, bool missingTranslation = false, string group = null,
        int cardIndex = -1)
    {
        Kind = kind;
        Reference = reference;
        MissingTranslation = missingTranslation;
        Group = group;
        CardIndex = cardIndex;
    }

    public string Kind { get; }
    public string Reference { get; }
    public bool MissingTranslation { get; }
    public string Group { get; }
    internal int CardIndex { get; }
}

public sealed class Word
{
    public string Id { get; set; }
    public string Text { get; set; }
    public string Chinese { get; set; }
    public double Weight { get; set; } = 1;
}

public sealed class Part
{
    public string Id { get; set; }
    public string Pool { get; set; }
    public string PoolInput { get; set; }
    public string FallbackPool { get; set; }
    public string Literal { get; set; }
    public string Input { get; set; }
    public string Copy { get; set; }
    public string Provider { get; set; }
    public List<string> Choices { get; set; }
    public bool Optional { get; set; }
    public double Chance { get; set; } = 1;
}

public enum SurnameOrder { Father, Mother, Random, New }

public sealed class Rule
{
    public string Id { get; set; }
    public int Priority { get; set; }
    public bool PresetOnly { get; set; }
    public bool UseNativeWordGroups { get; set; }
    public CardNaming NativeCards { get; set; }
    public Dictionary<string, string> When { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> WhenStartsWith { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, List<string>> WhenOneOf { get; set; } = new(StringComparer.Ordinal);
    public List<Part> Parts { get; set; } = new();
    public SurnameOrder Inheritance { get; set; }
    public NameWriting Writing { get; set; }
    public string ChineseSeparator { get; set; }
    public string OriginalSeparator { get; set; }
}

internal static class Ids
{
    private static readonly Regex LocalId = new(@"\A[a-z][a-z0-9_.-]{0,63}\z", RegexOptions.CultureInvariant);
    internal static bool Local(string id) => id != null && LocalId.IsMatch(id);
    internal static bool Qualified(string id) => id != null && id.Split(':') is { Length: 2 } parts && parts.All(Local);
    internal static bool Text(string text, int limit) => text != null && text.Length <= limit &&
        !text.Any(char.IsControl);
    internal static bool Weight(double value) => value >= 0 && value <= 10000;
}
