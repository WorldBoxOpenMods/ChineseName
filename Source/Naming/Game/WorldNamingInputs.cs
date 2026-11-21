using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Chinese_Name.Naming.Game;

/// <summary>Facts available when an original game entry or the naming editor builds a request.</summary>
public sealed class WorldNamingContext
{
    public string Type { get; }
    /// <summary>The object being named, when the game has already created it.</summary>
    public NanoObject Subject { get; }
    /// <summary>The person being named, or the founder or author supplying context.</summary>
    public Actor Actor { get; }
    public Culture Culture { get; }
    public Language Language { get; }
    public WorldTile Tile { get; }
    public bool IsPreview { get; }
    public IReadOnlyDictionary<string, object> Inputs { get; }

    internal WorldNamingContext(string type, NanoObject subject, Actor actor, Culture culture, Language language,
        WorldTile tile, bool preview, Dictionary<string, object> inputs)
    {
        Type = type;
        Subject = subject;
        Actor = actor;
        Culture = culture;
        Language = language;
        Tile = tile;
        IsPreview = preview;
        Inputs = new ReadOnlyDictionary<string, object>(inputs);
    }
}

/// <summary>Optional sources of facts used by the original WorldBox naming entries.</summary>
public static class WorldNamingInputs
{
    private static readonly Dictionary<string, Dictionary<string, (InputField Field, Func<WorldNamingContext, object> Read)>>
        Inputs = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Func<War, Kingdom, Kingdom, string>> WarInputs = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, (Func<Book, Actor, NameRequest, Func<string, bool>, NameResult> Generate,
        Func<Book, NameRequest, NameRequest> Restore)> BookNaming = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, (Func<Actor, bool> Applies, Action<Actor> Founded)> SurnameFounding =
        new(StringComparer.Ordinal);
    private static Func<NanoObject, IReadOnlyDictionary<long, string>> _placeAliases;

    /// <summary>Fill a declared field from the current game. A scoped field is unavailable when its provider returns null.</summary>
    /// <remarks>Read only. The editor supplies a culture and language without a live subject or founder.</remarks>
    public static void RegisterInput(string type, string field, Func<WorldNamingContext, object> read, bool scoped = false)
    {
        if (read == null) throw new ArgumentNullException(nameof(read));
        var service = NamingApi.Current;
        var category = service.Types.FirstOrDefault(candidate => candidate.Id == type);
        if (field == null || category == null || !category.Fields.TryGetValue(field, out var input) || scoped && input.Required)
            throw new ArgumentException("Declare a field first. Scoped fields must be optional.", nameof(field));
        if (!Inputs.TryGetValue(type, out var fields)) Inputs[type] = fields = new(StringComparer.Ordinal);
        fields.Add(field, (input, read));
        if (scoped) service.ScopeInput(type, field);
    }

    internal static void AddInputs(string type, NanoObject subject, Actor actor, long? culture, long? language,
        WorldTile tile, Dictionary<string, object> inputs, bool preview = false)
    {
        if (!Inputs.TryGetValue(type, out var fields)) return;
        var world = World.world;
        var owner = culture == null || actor?.culture?.id == culture ? actor?.culture : world?.cultures.get(culture.Value);
        var speech = language == null || actor?.language?.id == language ? actor?.language : world?.languages.get(language.Value);
        var context = new WorldNamingContext(type, subject, actor, owner, speech, tile ?? actor?.current_tile, preview, inputs);
        foreach (var provider in fields.Values)
        {
            if (inputs.ContainsKey(provider.Field.Id)) continue;
            object value;
            try { value = provider.Read(context); }
            catch (Exception error)
            {
                UnityEngine.Debug.LogWarning($"Naming input {type}/{provider.Field.Id} failed: {error.GetType().Name}");
                continue;
            }
            if (value != null && provider.Field.Accepts(value) && value is not string { Length: 0 })
                inputs.Add(provider.Field.Id, value);
        }
    }

    /// <summary>Supply a declared war field. A null war means it is being named for the first time.</summary>
    public static void RegisterWarInput(string field, Func<War, Kingdom, Kingdom, string> read)
    {
        if (read == null) throw new ArgumentNullException(nameof(read));
        var type = NamingApi.Current.Types.Single(category => category.Id == VanillaNames.War);
        if (field == null || !type.Fields.TryGetValue(field, out var input) || input.Kind != InputKind.Text)
            throw new ArgumentException("Declare a text field on the war category first.", nameof(field));
        WarInputs.Add(field, read);
    }

    /// <summary>Supply known place names indexed by the observing culture's ID.</summary>
    public static void RegisterPlaceAliases(Func<NanoObject, IReadOnlyDictionary<long, string>> read)
    {
        if (_placeAliases != null) throw new InvalidOperationException("A place alias source is already registered.");
        _placeAliases = read ?? throw new ArgumentNullException(nameof(read));
    }

    public static string CityStem(City city) => PlaceNames.CityStem(city);
    public static long? CityOrigin(City city) => PlaceNames.CityOrigin(city);
    public static string KingdomShortName(Kingdom kingdom) => KingdomNames.Short(kingdom);
    public static (long Id, double At, string Name)? CityFounding(City city) => PlaceNames.CityFounding(city);
    public static (long Id, string Name, Dictionary<long, string> Exonyms)? RebellionOrigin(War war) =>
        PlaceNames.RebellionOrigin(war);
    public static (long Id, string Name)? WarPartyAtStart(War war, bool attacker) => PlaceNames.WarPartyAtStart(war, attacker);

    /// <summary>Offer a new surname before inheritance or first naming. Consume the opportunity only when a surname was saved.</summary>
    public static void RegisterSurnameFounding(string id, Func<Actor, bool> applies, Action<Actor> founded)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Supply a surname founding source ID.", nameof(id));
        SurnameFounding.Add(id, (applies ?? throw new ArgumentNullException(nameof(applies)),
            founded ?? throw new ArgumentNullException(nameof(founded))));
    }

    internal static Action<Actor> SurnameFounder(Actor actor)
    {
        foreach (var source in SurnameFounding.Values)
            if (source.Applies(actor)) return source.Founded;
        return null;
    }

    /// <summary>Supply naming and optional saved facts for a vanilla book template.</summary>
    public static void RegisterBookNaming(string template, Func<Book, Actor, NameRequest, Func<string, bool>, NameResult> generate,
        Func<Book, NameRequest, NameRequest> restore = null)
    {
        if (string.IsNullOrWhiteSpace(template)) throw new ArgumentException("Supply a book template ID.", nameof(template));
        BookNaming.Add(template, (generate ?? throw new ArgumentNullException(nameof(generate)), restore));
    }

    internal static NameResult NameBook(string template, Book book, Actor author, NameRequest request, Func<string, bool> accept) =>
        (BookNaming.TryGetValue(template, out var naming) ? naming.Generate(book, author, request, accept) : null) ??
        NamingApi.Current.Generate(request, accept);

    internal static NameRequest RestoreBookInputs(string template, Book book, NameRequest request) =>
        BookNaming.TryGetValue(template, out var naming) ? naming.Restore?.Invoke(book, request) ?? request : request;

    /// <summary>Read the ordered conditional arrangements for the request's culture.</summary>
    public static IReadOnlyList<ConditionalArrangement> ConditionalArrangements(NameRequest request) =>
        NamingJson.Copy(NamingApi.Current.Inspect(request, null).Arrangements.ToArray());

    /// <summary>Try only the supplied arrangements using the normal naming engine.</summary>
    public static NameResult GenerateArrangements(NameRequest request, IEnumerable<ConditionalArrangement> arrangements,
        Func<string, bool> accept = null) =>
        NativeOnomastics.GenerateConditional(request, NamingApi.Current.Inspect(request, null), accept, arrangements, null);

    internal static void AddWarInputs(War war, Kingdom observer, Kingdom opponent, Dictionary<string, object> inputs)
    {
        foreach (var provider in WarInputs)
            if (!inputs.ContainsKey(provider.Key) && provider.Value(war, observer, opponent) is { Length: > 0 and <= 256 } value)
                inputs.Add(provider.Key, value);
    }

    public static string AliasFor(Culture observer, NanoObject place) => observer != null && place != null &&
        _placeAliases?.Invoke(place)?.TryGetValue(observer.id, out string name) == true ? name : null;

    public static Dictionary<long, string> CaptureAliases(NanoObject place)
    {
        if (place == null || _placeAliases?.Invoke(place) is not { Count: > 0 } aliases) return null;
        return new Dictionary<long, string>(aliases);
    }
}
