using System;
using System.Collections.Generic;
using ai.behaviours;
using HarmonyLib;
using Chinese_Name.Naming;
using Chinese_Name.Naming.Game;

namespace ChineseName.CultureExonyms;

internal static class ForeignNames
{
    internal const string Type = "chinesename:exonym";
    internal const string Opponent = "opponent-exonym";
    private const string Contacts = "chinesename.exonyms.v1";
    private const string OpponentKey = "chinesename.opponent-exonym.v1";

    internal static NameType Definition => new(Type, "异地称呼", new[]
    {
        new InputField("kind", InputKind.Text, required: true),
        new InputField("name", InputKind.Text, required: true)
    });

    private sealed class Contact
    {
        public double? FirstTrade { get; set; }
        public string Name { get; set; }
    }

    private static Dictionary<long, Contact> Read(BaseSystemData target)
    {
        target.get(Contacts, out string saved);
        return saved == null ? null : NamingJson.Read<Dictionary<long, Contact>>(saved);
    }

    internal static IReadOnlyDictionary<long, string> ReadAliases(NanoObject place)
    {
        var (native, data) = place switch
        {
            City city => (city.culture?.id, (BaseSystemData)city.data),
            Kingdom kingdom => (kingdom.culture?.id ?? kingdom.data?.id_culture, kingdom.data),
            _ => ((long?)null, null)
        };
        if (native is not >= 0 || data == null || Read(data) is not { } contacts) return null;
        var names = new Dictionary<long, string>();
        foreach (var (observer, contact) in contacts)
            if (observer != native.Value && contact.Name != null) names.Add(observer, contact.Name);
        return names.Count == 0 ? null : names;
    }

    internal static string OpponentName(War war, Kingdom observer, Kingdom opponent)
    {
        if (war != null)
        {
            war.data.get(OpponentKey, out string saved);
            return saved;
        }
        long? native = opponent?.culture?.id ?? opponent?.data?.id_culture;
        return observer?.culture is { } culture && native is >= 0 && culture.id != native &&
            Read(opponent.data)?.TryGetValue(culture.id, out var contact) == true ? contact.Name : null;
    }

    [HarmonyPatch(typeof(WarManager), nameof(WarManager.newWar))]
    internal static class SaveOpponent
    {
        private static void Postfix(War __result, WarTypeAsset pType, Kingdom pAttacker, Kingdom pDefender)
        {
            if (__result == null) return;
            var alias = pType.kingdom_for_name_attacker ? OpponentName(null, pAttacker, pDefender) :
                OpponentName(null, pDefender, pAttacker);
            if (alias != null) __result.data.set(OpponentKey, alias);
        }
    }

    internal static void LearnFromResidence(Actor resident, City city) =>
        Learn(resident.culture, resident.language, city, includeKingdom: true, established: true);

    private static void Learn(Culture culture, Language language, City foreign, bool includeKingdom, bool established)
    {
        Observe(culture, language, foreign.culture, foreign.data, "city", foreign.name, WorldNamingInputs.CityStem(foreign), established);
        var kingdom = foreign.kingdom;
        if (includeKingdom && kingdom?.isCiv() == true)
            Observe(culture, language, kingdom.culture, kingdom.data, "kingdom", kingdom.name, WorldNamingInputs.KingdomShortName(kingdom), established);
    }

    private static void Observe(Culture observer, Language language, Culture native, BaseSystemData target,
        string kind, string formal, string stem, bool established)
    {
        if (observer == null || native == null || observer.id == native.id ||
            string.IsNullOrWhiteSpace(stem) || stem.Length > 256) return;
        var contacts = Read(target) ?? new Dictionary<long, Contact>();
        if (!contacts.TryGetValue(observer.id, out var contact)) contacts[observer.id] = contact = new Contact();
        if (contact.Name != null) return;
        if (!established && !contact.FirstTrade.HasValue)
        {
            contact.FirstTrade = World.world.getCurWorldTime();
            target.set(Contacts, NamingJson.Write(contacts));
            return;
        }
        if (!established && Date.getYearsSince(contact.FirstTrade.Value) < 1) return;
        // Preserve the name seed used for contacts formed by the previous built-in implementation.
        ulong kindSalt = kind == "city" ? 0xF9DAB5910F08A8A2UL : 0xFE5F93C586F6C202UL;
        ulong seed = unchecked((ulong)(World.world.map_stats.life_dna + target.id * 543)) ^ (ulong)observer.id ^ kindSalt;
        var result = NamingApi.Current.Generate(new NameRequest(Type, seed, observer.id, language?.id,
            new Dictionary<string, object> { ["kind"] = kind, ["name"] = stem }),
            name => name != formal && name.IndexOf(stem, StringComparison.Ordinal) >= 0);
        if (!result.Success) return;
        contact.Name = result.Text;
        target.set(Contacts, NamingJson.Write(contacts));
    }

    [HarmonyPatch(typeof(BehBoatMakeTrade), nameof(BehBoatMakeTrade.execute))]
    internal static class Trade
    {
        private static void Postfix(Actor pActor, BehResult __result)
        {
            var dock = pActor.inside_building;
            if (__result != BehResult.Continue || dock?.component_docks == null || dock != pActor.beh_building_target ||
                pActor.city is not { } home || dock.getCity() is not { } foreign || home == foreign ||
                home.culture == foreign.culture) return;
            bool crossBorder = home.kingdom != foreign.kingdom;
            Learn(home.culture, home.language, foreign, crossBorder, established: false);
            Learn(foreign.culture, foreign.language, home, crossBorder, established: false);
        }
    }
}
