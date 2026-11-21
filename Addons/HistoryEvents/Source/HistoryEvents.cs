using System;
using System.Collections.Generic;
using System.Linq;
using Chinese_Name.Naming;
using Chinese_Name.Naming.Game;
using HarmonyLib;

namespace ChineseName.HistoryEvents;

internal static class HistoryEvents
{
    internal const string EventName = "historical-event";
    internal const string EventKind = "historical-event-kind";
    internal const char EventCode = '\uE12A';
    internal const string WarKind = "war", RebellionKind = "rebellion", MigrationKind = "migration", FoundingKind = "founding";
    private const string MigrationKey = "chinesename.city-migrations.v2";
    private const string PriorMigrationKey = "chinesename.city-migration.v1";
    private const string ConflictKey = "chinesename.kingdom-conflicts.v2";
    private const string PriorConflictKey = "chinesename.kingdom-conflict.v1";
    private const string BookKey = "chinesename.book-event.v1";
    private const string ReadingKey = "chinesename.recent-book-events.v1";
    private const int EventsPerKind = 8;
    private const int RecentBooks = 24;
    private const double WorldTimePerYear = 60;

    private sealed class Place
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public Dictionary<long, string> Exonyms { get; set; }
    }

    private sealed class Occurrence
    {
        public long Id { get; set; }
        public string Kind { get; set; }
        public double At { get; set; }
        public Place From { get; set; }
        public Place To { get; set; }
        public Place RevoltCity { get; set; }
        public string Title { get; set; }
    }

    private sealed class Citation
    {
        public long Id { get; set; }
        public string Kind { get; set; }
        public double At { get; set; }
        public string Name { get; set; }
    }

    internal static string Example(string kind) => kind switch
    {
        WarKind => "临川国与松岭邦之战",
        RebellionKind => "澄江之乱",
        MigrationKind => "自澄江迁往新澄江",
        FoundingKind => "澄江建城",
        _ => null
    };

    internal static void PrepareExample(Dictionary<string, object> inputs)
    {
        if (inputs.TryGetValue(EventName, out var title) && Equals(title, Example(WarKind)) &&
            inputs.TryGetValue(EventKind, out var value) && value is string kind && Example(kind) is { } example)
            inputs[EventName] = example;
    }

    internal static string ConditionIcon(string field, string value) => (field, value) switch
    {
        (EventName or EventKind, null) => "ui/Icons/iconBooksWritten",
        (EventKind, WarKind) => AssetManager.war_types_library.get("normal")?.path_icon,
        (EventKind, RebellionKind) => AssetManager.war_types_library.get("rebellion")?.path_icon,
        (EventKind, MigrationKind) => "ui/Icons/iconStatisticsGoodLookingMigrants",
        (EventKind, FoundingKind) => "ui/Icons/iconCityList",
        _ => null
    };

    internal static NameResult GenerateBook(Book book, Actor author, NameRequest request, Func<string, bool> accept)
    {
        var service = NamingApi.Current;
        if (book.data.book_type != null || author == null) return service.Generate(request, accept);

        var culture = request.Culture.HasValue ? World.world.cultures.get(request.Culture.Value) : author.culture;
        var recent = Read<List<Citation>>(culture?.data, ReadingKey) ?? new();
        var arrangements = WorldNamingInputs.ConditionalArrangements(request).Where(arrangement => !arrangement.Disabled &&
            arrangement.IsConfigured && arrangement.Cards?.Enabled == true && arrangement.Cards.Template != null).ToArray();
        Citation[] candidates = null;
        for (int start = 0; start < arrangements.Length;)
        {
            bool usesEvent = UsesEvent(arrangements[start]);
            int end = start + 1;
            while (end < arrangements.Length && UsesEvent(arrangements[end]) == usesEvent) end++;
            var options = arrangements.Skip(start).Take(end - start);
            start = end;

            // 相邻的事件排列一起选史料。排在前面的普通排列照常优先。
            if (usesEvent)
            {
                candidates ??= Candidates(author, culture, recent);
                foreach (var source in candidates)
                {
                    var result = WorldNamingInputs.GenerateArrangements(WithEvent(request, source), options, accept);
                    if (!result.Success) continue;
                    Cite(book, culture, recent, source);
                    return result;
                }
                if (candidates.Length != 0) continue;
            }
            var standalone = WorldNamingInputs.GenerateArrangements(request, options, accept);
            if (standalone.Success) return standalone;
        }

        var latest = (candidates ?? Candidates(author, culture, recent)).FirstOrDefault();
        if (latest == null) return service.Generate(request, accept);
        var fallback = service.Generate(WithEvent(request, latest), accept);
        if (fallback.Success && fallback.Sources.Values.Any(source => source.Kind == "input" && source.Reference == EventName))
            Cite(book, culture, recent, latest);
        return fallback;
    }

    internal static NameRequest UseSavedEvent(Book book, NameRequest request) =>
        Read<Citation>(book.data, BookKey) is { } saved ? WithEvent(request, saved) : request;

    private static NameRequest WithEvent(NameRequest request, Citation source)
    {
        var inputs = new Dictionary<string, object>(request.Inputs)
        {
            [EventName] = source.Name, [EventKind] = source.Kind
        };
        return new NameRequest(request.Type, request.Seed, request.Culture, request.Language, inputs,
            request.FixedParts, request.Writing);
    }

    private static bool UsesEvent(ConditionalArrangement arrangement) =>
        ReadsEvent(arrangement.Expression) || arrangement.Cards?.Template?.Split('|')[0].Contains(EventCode) == true;

    private static bool ReadsEvent(ArrangementCondition term) =>
        term != null && (term.Field is EventName or EventKind || term.Children?.Any(ReadsEvent) == true);

    private static Citation[] Candidates(Actor author, Culture culture, List<Citation> recent)
    {
        var city = author.city;
        return Conflicts(author.kingdom).Concat(Migrations(city)).Append(Founded(city))
            .Where(item => item != null)
            .OrderByDescending(item => item.At - WorldTimePerYear * Math.Clamp(recent.Count(book =>
                (book.Kind, book.Id, book.At) == EventId(item)) - 1, 0, 3))
            .ThenByDescending(item => item.At)
            .ThenBy(item => item.Kind == FoundingKind ? 2 : item.Kind == MigrationKind ? 1 : 0)
            .ThenByDescending(item => item.Id)
            .Select(item => new Citation { Id = item.Id, Kind = item.Kind, At = item.At, Name = Describe(item, culture) })
            .Where(item => item.Name is { Length: > 0 and <= 256 }).ToArray();
    }

    private static Occurrence Founded(City city) => WorldNamingInputs.CityFounding(city) is { } founding
        ? new Occurrence { Id = founding.Id, Kind = FoundingKind, At = founding.At,
            To = new Place { Id = founding.Id, Name = founding.Name } }
        : null;

    private static void Cite(Book book, Culture culture, List<Citation> recent, Citation source)
    {
        book.data.set(BookKey, NamingJson.Write(source));
        if (culture == null) return;
        recent.Add(source);
        if (recent.Count > RecentBooks) recent.RemoveAt(0);
        culture.data.set(ReadingKey, NamingJson.Write(recent));
    }

    private static (string Kind, long Id, double At) EventId(Occurrence item) => (item.Kind, item.Id, item.At);

    private static string Describe(Occurrence item, Culture culture)
    {
        switch (item.Kind)
        {
            case WarKind:
                string first = KingdomName(item.From, culture), second = KingdomName(item.To, culture);
                return first != null && second != null ? first + "与" + second + "之战" : item.Title;
            case RebellionKind:
                return CityName(item.RevoltCity, culture) is { } city ? city + "之乱" : item.Title;
            case MigrationKind:
                string destination = CityName(item.To, culture);
                return item.From == null ? "迁入" + destination : "自" + CityName(item.From, culture) + "迁往" + destination;
            case FoundingKind:
                return item.To?.Name + "建城";
            default:
                return null;
        }
    }

    private static string CityName(Place place, Culture culture) =>
        culture != null && place?.Exonyms?.TryGetValue(culture.id, out string name) == true ? name : place?.Name;

    private static string KingdomName(Place place, Culture culture) => place == null ? null :
        WorldNamingInputs.AliasFor(culture, World.world.kingdoms.get(place.Id) ?? World.world.kingdoms.db_get(place.Id)) ?? place.Name;

    private static Place Capture(NanoObject item) =>
        string.IsNullOrWhiteSpace(item?.name) ? null : new Place { Id = item.id, Name = item.name };

    private static Place CaptureCity(City city)
    {
        var place = Capture(city);
        if (place != null) place.Exonyms = WorldNamingInputs.CaptureAliases(city);
        return place;
    }

    private static T Read<T>(BaseSystemData data, string key) where T : class
    {
        if (data == null) return null;
        data.get(key, out string saved);
        return saved == null ? null : NamingJson.Read<T>(saved);
    }

    private static List<Occurrence> Migrations(City city) => Read<List<Occurrence>>(city?.data, MigrationKey) ??
        (Read<Occurrence>(city?.data, PriorMigrationKey) is { } prior ? new() { prior } : new());

    private static List<Occurrence> Bounded(IEnumerable<Occurrence> items) => items
        .GroupBy(EventId).Select(group => group.First())
        .OrderByDescending(item => item.At).ThenByDescending(item => item.Id)
        .GroupBy(item => item.Kind).SelectMany(group => group.Take(EventsPerKind)).ToList();

    private static void Remember(BaseSystemData data, string key, List<Occurrence> history, Occurrence item)
    {
        if (history.Any(existing => EventId(existing) == EventId(item))) return;
        data.set(key, NamingJson.Write(Bounded(history.Append(item))));
    }

    internal static void RecordMigration(City origin, City destination)
    {
        if (destination == origin || CaptureCity(destination) is not { } target) return;
        var movement = new Occurrence
        {
            Kind = MigrationKind, Id = destination.id, At = World.world.getCurWorldTime(), From = CaptureCity(origin), To = target
        };
        Remember(destination.data, MigrationKey, Migrations(destination), movement);
        if (movement.From != null) Remember(origin.data, MigrationKey, Migrations(origin), movement);
    }

    private static Occurrence Completed(War conflict)
    {
        var history = new Occurrence
        {
            Kind = conflict.data.war_type == RebellionKind ? RebellionKind : WarKind,
            Id = conflict.id, At = conflict.data.died_time, Title = conflict.name,
            From = Party(conflict, true), To = Party(conflict, false)
        };
        if (history.Kind == RebellionKind && WorldNamingInputs.RebellionOrigin(conflict) is { } city)
            history.RevoltCity = new Place { Id = city.Id, Name = city.Name, Exonyms = city.Exonyms };
        return history;
    }

    private static Place Party(War conflict, bool attacker) => WorldNamingInputs.WarPartyAtStart(conflict, attacker) is { } party
        ? new Place { Id = party.Id, Name = party.Name }
        : Capture(attacker ? conflict.getMainAttacker() : conflict.getMainDefender());

    private static IEnumerable<long> Participants(War conflict)
    {
        var data = conflict.data;
        return new[] { data.main_attacker, data.main_defender }.Concat(data.list_attackers).Concat(data.list_defenders)
            .Concat(data.past_attackers).Concat(data.past_defenders).Concat(data.died_attackers).Concat(data.died_defenders);
    }

    private static List<Occurrence> Conflicts(Kingdom kingdom)
    {
        if (kingdom?.isCiv() != true) return new();
        if (Read<List<Occurrence>>(kingdom.data, ConflictKey) is { } saved) return saved;

        var items = World.world.wars.Where(war => war.data.died_time > 0 && war.data.winner != WarWinner.Merged &&
            war.data.died_time >= kingdom.data.created_time &&
            (Participants(war).Contains(kingdom.id) || war.getAsset()?.total_war == true)).Select(Completed);
        if (Read<Occurrence>(kingdom.data, PriorConflictKey) is { } prior) items = items.Prepend(prior);
        var history = Bounded(items);
        kingdom.data.set(ConflictKey, NamingJson.Write(history));
        return history;
    }

    [HarmonyPatch(typeof(ai.behaviours.BehCheckBuildCity), nameof(ai.behaviours.BehCheckBuildCity.execute))]
    internal static class MigrantFounding
    {
        private static void Prefix(Actor pActor, out City __state) => __state = pActor.city;

        private static void Postfix(Actor pActor, City __state, ai.behaviours.BehResult __result)
        {
            if (__result == ai.behaviours.BehResult.Continue && __state != null)
                RecordMigration(__state, pActor.city);
        }
    }

    [HarmonyPatch(typeof(BehKingCheckNewCityFoundation), nameof(BehKingCheckNewCityFoundation.moveSomeUnitsToNewCity))]
    internal static class ExpansionMigration
    {
        private static void Prefix(City pNewCity, out long __state) => __state = pNewCity.getTotalMoved();

        private static void Postfix(City pNewCity, City pFromCity, long __state)
        {
            if (pNewCity.getTotalMoved() > __state) RecordMigration(pFromCity, pNewCity);
        }
    }

    [HarmonyPatch(typeof(City), nameof(City.increaseMigrants))]
    internal static class Immigration
    {
        private static void Postfix(City __instance)
        {
            if (__instance.isAlive()) RecordMigration(null, __instance);
        }
    }

    [HarmonyPatch(typeof(WarManager), nameof(WarManager.endWar))]
    internal static class ConflictEnded
    {
        private static void Prefix(War pWar, WarWinner pWinner, out bool __state) =>
            __state = pWinner != WarWinner.Merged && pWar.isAlive() && !pWar.hasEnded();

        private static void Postfix(War pWar, bool __state)
        {
            if (!__state || !pWar.hasEnded()) return;
            var world = World.world;
            var outcome = Completed(pWar);
            var parties = pWar.getAsset()?.total_war == true ? world.kingdoms :
                Participants(pWar).Where(id => id >= 0).Distinct().Select(id => world.kingdoms.get(id));
            foreach (var kingdom in parties)
                if (kingdom?.isCiv() == true) Remember(kingdom.data, ConflictKey, Conflicts(kingdom), outcome);
        }
    }
}
