using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;

namespace Chinese_Name.Naming.Game;

internal static class PlaceNames
{
    internal const string SourceCity = "source-city";
    internal const string SourceFoundingName = "source-city-founding-name";
    internal const string Region = "region-name";
    internal const string Landform = "landform";
    internal const string RebellionCity = "rebellion-city";
    internal const string AuthorCity = "author-city";
    internal const string AuthorFoundingName = "author-city-founding-name";
    private const string CityNameKey = "chinesename.city-name.v1";
    private const string OriginKey = "chinesename.city-origin.v1";
    private const string RegionKey = "chinesename.city-region.v1";
    private const string RebellionKey = "chinesename.rebellion-city.v1";
    private const string AuthorKey = "chinesename.author-city.v1";
    private const string FoundingKey = "chinesename.city-founding.v1";
    private const string WarStartKey = "chinesename.war-parties.v1";

    private sealed class PlaceLabel
    {
        public long Id { get; set; }
        public string Name { get; set; }
    }

    private sealed class FoundingSnapshot
    {
        public long Id { get; set; }
        public string Kind { get; set; }
        public double At { get; set; }
        public PlaceLabel To { get; set; }
    }

    private sealed class WarParties
    {
        public PlaceLabel From { get; set; }
        public PlaceLabel To { get; set; }
    }

    private sealed class CityReference
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Stem { get; set; }
        public string Exonym { get; set; }
        public string FoundedAs { get; set; }
        public Dictionary<long, string> Exonyms { get; set; }
        public string Display => string.IsNullOrWhiteSpace(Stem) ? Name : Stem;
    }

    [ThreadStatic] private static City _expansionSource;
    [ThreadStatic] private static CityReference _rebellionSource;

    internal static void FoundingContext(City city)
    {
        if (Capture(_expansionSource, includeFounding: true) is { } origin)
            city.data.set(OriginKey, NamingJson.Write(origin));
        if (GeographicNaming.At(city.getTile()) is { } region)
            city.data.set(RegionKey, NamingJson.Write(region));
    }

    internal static void CityInputs(City city, Dictionary<string, object> inputs)
    {
        var origin = Read<CityReference>(city.data, OriginKey);
        Add(inputs, SourceCity, origin?.Display);
        Add(inputs, SourceFoundingName, origin?.FoundedAs);
        var region = Read<NamingRegion>(city.data, RegionKey) ?? GeographicNaming.At(city.getTile());
        Add(inputs, Region, region?.Name);
        Add(inputs, Landform, region?.Landform);
    }

    internal static void SaveCityName(City city, NameResult result)
    {
        string stem = null;
        if (!result.Parts.TryGetValue("name", out stem))
        {
            var source = result.Sources.FirstOrDefault(pair => pair.Value.Reference is SourceCity or Region);
            if (source.Key != null) result.Parts.TryGetValue(source.Key, out stem);
        }
        city.data.set(CityNameKey, NamingJson.Write(new CityReference { Id = city.id, Name = result.Text, Stem = stem }));
    }

    internal static void WarInputs(War war, WarTypeAsset type, Kingdom observer, Kingdom opponent,
        Dictionary<string, object> inputs)
    {
        if (type.id == "rebellion")
            Add(inputs, RebellionCity, (war != null ? Read<CityReference>(war.data, RebellionKey) : _rebellionSource)?.Display);
        WorldNamingInputs.AddWarInputs(war, observer, opponent, inputs);
    }

    internal static void BookInputs(Book book, Actor author, Dictionary<string, object> inputs)
    {
        var reference = Read<CityReference>(book.data, AuthorKey);
        if (reference == null && book.data.book_type == null && Capture(author?.city, includeFounding: true) is { } created)
        {
            reference = created;
            reference.Exonym = WorldNamingInputs.AliasFor(author.culture, author.city);
            book.data.set(AuthorKey, NamingJson.Write(created));
        }
        Add(inputs, AuthorCity, reference?.Exonym ?? reference?.Display ?? book.data.author_city_name);
        Add(inputs, AuthorFoundingName, reference?.FoundedAs);
    }

    internal static string CityStem(City city) => Capture(city)?.Display;

    internal static (long Id, string Name, Dictionary<long, string> Exonyms)? RebellionOrigin(War war) =>
        Read<CityReference>(war.data, RebellionKey) is { } city ? (city.Id, city.Display, city.Exonyms) : null;

    internal static (long Id, double At, string Name)? CityFounding(City city) =>
        Read<FoundingSnapshot>(city?.data, FoundingKey) is { To: { } place } saved ? (saved.Id, saved.At, place.Name) : null;

    internal static (long Id, string Name)? WarPartyAtStart(War war, bool attacker)
    {
        var parties = Read<WarParties>(war.data, WarStartKey);
        var place = attacker ? parties?.From : parties?.To;
        return place == null ? null : (place.Id, place.Name);
    }

    private static PlaceLabel Label(NanoObject place) =>
        string.IsNullOrWhiteSpace(place?.name) ? null : new PlaceLabel { Id = place.id, Name = place.name };

    private static CityReference Capture(City city, bool includeFounding = false)
    {
        if (city?.name is not { Length: > 0 } name) return null;
        var stored = city.data.custom_name ? null : Read<CityReference>(city.data, CityNameKey);
        var reference = stored?.Name == name ? stored : new CityReference { Id = city.id, Name = name };
        if (includeFounding) reference.FoundedAs = CityFounding(city)?.Name ?? city.data.past_names?.FirstOrDefault().name;
        return reference;
    }

    private static T Read<T>(BaseSystemData data, string key) where T : class
    {
        if (data == null) return null;
        data.get(key, out string serialized);
        return serialized == null ? null : NamingJson.Read<T>(serialized);
    }

    private static void Add(Dictionary<string, object> inputs, string field, string value)
    {
        if (value is { Length: > 0 and <= 256 }) inputs[field] = value;
    }

    [HarmonyPatch(typeof(City), nameof(City.newCityEvent))]
    internal static class CityFounded
    {
        private static void Postfix(City __instance)
        {
            if (Label(__instance) is not { } place || Read<FoundingSnapshot>(__instance.data, FoundingKey) != null) return;
            var snapshot = new FoundingSnapshot
            {
                Id = place.Id, Kind = "founding", At = World.world.getCurWorldTime(), To = place
            };
            __instance.data.set(FoundingKey, NamingJson.Write(snapshot));
        }
    }

    [HarmonyPatch(typeof(BehKingCheckNewCityFoundation), nameof(BehKingCheckNewCityFoundation.execute))]
    internal static class Expansion
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
        {
            var find = AccessTools.Method(typeof(BehKingCheckNewCityFoundation),
                nameof(BehKingCheckNewCityFoundation.findZoneForExpansion));
            var build = AccessTools.Method(typeof(CityManager), nameof(CityManager.buildNewCity));
            var codes = source.ToList();
            int selection = codes.FindIndex(code => code.Calls(find));
            int creation = codes.FindIndex(code => code.Calls(build));
            if (selection < 1 || creation <= selection || codes.Count(code => code.Calls(build)) != 1 ||
                codes[selection - 1].opcode != OpCodes.Ldloca && codes[selection - 1].opcode != OpCodes.Ldloca_S)
                throw new InvalidOperationException("Cannot locate the expansion's source city.");
            var call = codes[creation];
            var load = new CodeInstruction(OpCodes.Ldloc, codes[selection - 1].operand);
            load.labels.AddRange(call.labels);
            load.blocks.AddRange(call.blocks);
            call.labels.Clear();
            call.blocks.Clear();
            codes.Insert(creation, load);
            call.opcode = OpCodes.Call;
            call.operand = AccessTools.Method(typeof(PlaceNames), nameof(BuildFromSource));
            return codes;
        }
    }

    [HarmonyPatch(typeof(ai.behaviours.BehCheckBuildCity), nameof(ai.behaviours.BehCheckBuildCity.execute))]
    internal static class MigrantFounding
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source) =>
            IndependentNames.ReplaceCall(source, AccessTools.Method(typeof(CityManager), nameof(CityManager.buildNewCity)),
                AccessTools.Method(typeof(MigrantFounding), nameof(BuildFromMigrant)));

        private static City BuildFromMigrant(CityManager manager, Actor founder, TileZone zone) =>
            BuildFromSource(manager, founder, zone, founder.city);
    }

    private static City BuildFromSource(CityManager manager, Actor founder, TileZone zone, City origin)
    {
        var previous = _expansionSource;
        _expansionSource = origin;
        try { return manager.buildNewCity(founder, zone); }
        finally { _expansionSource = previous; }
    }

    [HarmonyPatch(typeof(DiplomacyHelpersRebellion), nameof(DiplomacyHelpersRebellion.startRebellion))]
    internal static class Rebellion
    {
        private static void Prefix(Actor pActor, out CityReference __state)
        {
            __state = _rebellionSource;
            _rebellionSource = Capture(pActor.city);
            if (_rebellionSource != null) _rebellionSource.Exonyms = WorldNamingInputs.CaptureAliases(pActor.city);
        }

        private static Exception Finalizer(Exception __exception, CityReference __state)
        {
            _rebellionSource = __state;
            return __exception;
        }
    }

    internal static void SaveWarSource(War war, WarTypeAsset type, Kingdom attacker, Kingdom defender)
    {
        if (type.id == "rebellion" && _rebellionSource != null)
            war.data.set(RebellionKey, NamingJson.Write(_rebellionSource));
        war.data.set(WarStartKey, NamingJson.Write(new WarParties { From = Label(attacker), To = Label(defender) }));
    }
}
