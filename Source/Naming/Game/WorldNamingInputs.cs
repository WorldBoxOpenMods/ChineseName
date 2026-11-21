using System;
using System.Collections.Generic;
using System.Linq;

namespace Chinese_Name.Naming.Game;

/// <summary>Optional sources of facts used by the original WorldBox naming entries.</summary>
public static class WorldNamingInputs
{
    private static readonly Dictionary<string, Func<War, Kingdom, Kingdom, string>> WarInputs = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, (Func<Book, Actor, NameRequest, Func<string, bool>, NameResult> Generate,
        Func<Book, NameRequest, NameRequest> Restore)> BookNaming = new(StringComparer.Ordinal);
    private static Func<NanoObject, IReadOnlyDictionary<long, string>> _placeAliases;

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
    public static string KingdomShortName(Kingdom kingdom) => KingdomNames.Short(kingdom);
    public static (long Id, double At, string Name)? CityFounding(City city) => PlaceNames.CityFounding(city);
    public static (long Id, string Name, Dictionary<long, string> Exonyms)? RebellionOrigin(War war) =>
        PlaceNames.RebellionOrigin(war);
    public static (long Id, string Name)? WarPartyAtStart(War war, bool attacker) => PlaceNames.WarPartyAtStart(war, attacker);

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
