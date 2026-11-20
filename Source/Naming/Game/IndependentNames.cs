using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Chinese_Name.Naming.Game;

internal static class IndependentNames
{
    internal static long Seed(long id) => unchecked((World.world?.map_stats?.life_dna ?? 0) + id * 543);

    internal static string FromKingdom(string type, Kingdom kingdom, long id, string kind = null,
        Dictionary<string, object> inputs = null, Func<string, bool> accept = null)
    {
        inputs ??= new Dictionary<string, object>();
        AddName(inputs, "origin", kingdom);
        var result = NamingApi.Current.Generate(VanillaNames.Request(type, kingdom?.king, unchecked((ulong)Seed(id)),
            kind: kind, culture: kingdom?.culture?.id, language: kingdom?.language?.id, inputs: inputs), accept);
        return result.Success ? result.Text : null;
    }

    private static void AddName(Dictionary<string, object> inputs, string key, Kingdom kingdom)
    {
        if (kingdom?.name is { Length: > 0 and <= 256 } name) inputs[key] = name;
    }

    private static readonly MethodInfo Template = AccessTools.Method(typeof(NameGenerator),
        nameof(NameGenerator.generateNameFromTemplate), new[] { typeof(string), typeof(Actor), typeof(Kingdom), typeof(bool) });

    internal static IEnumerable<CodeInstruction> ReplaceCall(IEnumerable<CodeInstruction> source,
        MethodInfo from, MethodInfo replacement, params int[] arguments)
    {
        var codes = source.ToList();
        int index = codes.FindIndex(code => code.Calls(from));
        if (index < 0 || codes.Count(code => code.Calls(from)) != 1)
            throw new InvalidOperationException("Expected a single name call to " + from.Name);
        var call = codes[index];
        if (arguments.Length > 0)
        {
            var loads = arguments.Select(arg => new CodeInstruction(OpCodes.Ldarg, arg)).ToArray();
            loads[0].labels.AddRange(call.labels);
            loads[0].blocks.AddRange(call.blocks);
            call.labels.Clear();
            call.blocks.Clear();
            codes.InsertRange(index, loads);
        }
        call.opcode = OpCodes.Call;
        call.operand = replacement;
        return codes;
    }

    private static IEnumerable<CodeInstruction> ReplaceCall(IEnumerable<CodeInstruction> source,
        MethodInfo from, string replacement, params int[] arguments) =>
        ReplaceCall(source, from, AccessTools.Method(typeof(IndependentNames), replacement), arguments);

    [HarmonyPatch(typeof(MapStats), nameof(MapStats.initNewWorld))]
    internal static class WorldNames
    {
        private static void Postfix(MapStats __instance) => AssignWorldNames(__instance);
    }

    internal static int AssignWorldNames(MapStats map)
    {
        string worldName = VanillaNames.Generate("worldbox:world", null, map.life_dna);
        string creator = VanillaNames.Generate("worldbox:player", null, map.life_dna);
        int changed = 0;
        if (worldName != null && map.name != worldName) { map.name = worldName; changed++; }
        if (creator != null && map.player_name != creator) { map.player_name = creator; changed++; }
        return changed;
    }

    [HarmonyPatch(typeof(WarManager), nameof(WarManager.newWar))]
    internal static class WarName
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) =>
            ReplaceCall(codes, Template, nameof(NameWar), 1, 2, 3);
    }

    private static string NameWar(string template, Actor actor, Kingdom kingdom, bool legacy,
        Kingdom attacker, Kingdom defender, WarTypeAsset war) =>
        GenerateWar(attacker, defender, war, unchecked((long)NamingRandom.Hash(template) + World.world.map_stats.warsStarted))
        ?? NameGenerator.generateNameFromTemplate(template, actor, kingdom, legacy);

    internal static string GenerateWar(Kingdom attacker, Kingdom defender, WarTypeAsset war, long seed,
        Func<string, bool> accept = null)
    {
        if (war == null) return null;
        var inputs = new Dictionary<string, object>();
        AddName(inputs, "attacker", attacker);
        AddName(inputs, "defender", defender);
        var kingdom = war.kingdom_for_name_attacker ? attacker : defender;
        return FromKingdom(VanillaNames.War, kingdom ?? attacker ?? defender, seed, war.id, inputs, accept);
    }

    [HarmonyPatch(typeof(Book), nameof(Book.newBook))]
    internal static class BookName
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) =>
            ReplaceCall(codes, Template, nameof(NameBook), 0);
    }

    private static string NameBook(string template, Actor author, Kingdom kingdom, bool legacy, Book book) =>
        GenerateBook(template, book, author) ?? NameGenerator.generateNameFromTemplate(template, author, kingdom, legacy);

    internal static string GenerateBook(string template, Book book, Actor author, long? culture = null,
        long? language = null, Func<string, bool> accept = null)
    {
        const string prefix = "book_name_";
        var inputs = new Dictionary<string, object>();
        if (template?.StartsWith(prefix, StringComparison.Ordinal) == true)
            inputs["topic"] = template.Substring(prefix.Length).Replace('_', '-');
        return VanillaNames.Generate("worldbox:book", author, Seed(book.id), kind: template, inputs: inputs,
            culture: culture, language: language, accept: accept);
    }

    [HarmonyPatch(typeof(Plot), nameof(Plot.newPlot))]
    internal static class PlotName
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) =>
            ReplaceCall(codes, AccessTools.Method(typeof(NanoObject), nameof(NanoObject.setName)),
                nameof(SetPlotName), 1, 2);
    }

    private static void SetPlotName(Plot plot, string vanilla, bool track, Actor author, PlotAsset asset)
    {
        plot.data.name_culture_id = author?.culture?.id ?? -1L;
        if (author?.language is { } language) plot.data.set("chinesename.plot-language.v1", language.id);
        plot.setName(GeneratePlot(plot, author, asset) ?? vanilla, track);
    }

    internal static string GeneratePlot(Plot plot, Actor author, PlotAsset asset, Func<string, bool> accept = null)
    {
        if (asset == null) return null;
        var inputs = new Dictionary<string, object> { ["plot"] = asset.id.ToLowerInvariant().Replace('_', '-') };
        plot.data.get("chinesename.plot-language.v1", out long language, -1L);
        return VanillaNames.Generate(VanillaNames.Plot, author, Seed(plot.id), kind: asset.id, inputs: inputs,
            culture: author?.culture?.id ?? (plot.data.name_culture_id >= 0 ? plot.data.name_culture_id : null),
            language: author?.language?.id ?? (language >= 0 ? language : null), accept: accept);
    }

    [HarmonyPatch(typeof(Army), nameof(Army.generateName))]
    internal static class ArmyName
    {
        private const string Source = "chinesename.army-kingdom.v1";

        private static bool Prefix(Army __instance, Kingdom pKingdom)
        {
            var army = __instance;
            if (army.data.custom_name && !string.IsNullOrWhiteSpace(army.data.name)) return false;
            var kingdom = pKingdom ?? army.getKingdom();
            army.data.get(Source, out long previous, -1);
            if (kingdom != null && !string.IsNullOrEmpty(army.data.name) && (previous == kingdom.id || pKingdom == null)) return false;
            return !TryAssign(army, kingdom);
        }

        internal static bool TryAssign(Army army, Kingdom kingdom, Func<string, bool> accept = null)
        {
            if (kingdom == null)
            {
                if (army.name != "无归属军队") army.setName("无归属军队");
                army.data.set(Source, -1L);
                return true;
            }
            var city = army.getCity();
            var inputs = new Dictionary<string, object>();
            if (city?.name is { Length: > 0 and <= 256 } station) inputs["station"] = station;
            AddName(inputs, "origin", kingdom);
            var taken = new HashSet<string>(World.world.armies.Where(other => other != army &&
                other.getKingdom() == kingdom).Select(other => other.name), StringComparer.Ordinal);
            string name = VanillaNames.Generate("worldbox:army", null, Seed(army.id ^ kingdom.id),
                culture: city?.culture?.id ?? kingdom.culture?.id, language: city?.language?.id ?? kingdom.language?.id,
                accept: candidate => !taken.Contains(candidate) && (accept == null || accept(candidate)), inputs: inputs);
            if (name == null) return false;
            if (army.name != name) army.setName(name);
            army.data.set(Source, kingdom.id);
            return true;
        }
    }

    [HarmonyPatch(typeof(Subspecies), nameof(Subspecies.generateName))]
    internal static class SubspeciesName
    {
        private static bool Prefix(Subspecies __instance, ActorAsset pAsset, WorldTile pTile)
            => !TryAssign(__instance, pAsset, pTile?.Type?.biome_asset?.id);

        internal static bool TryAssign(Subspecies subspecies, ActorAsset asset, string biome, Func<string, bool> accept = null)
        {
            if (asset == null) return false;
            var used = new HashSet<string>(World.world.subspecies.Where(other => other != subspecies)
                .Select(other => other.name), StringComparer.OrdinalIgnoreCase);
            var inputs = new Dictionary<string, object> { ["species"] = asset.id };
            if (biome != null) inputs["kind"] = biome;
            var result = NamingApi.Current.Generate(new NameRequest("worldbox:subspecies", unchecked((ulong)Seed(subspecies.id)),
                inputs: inputs), name => !used.Contains(name) && (accept == null || accept(name)));
            if (!result.Success) return false;
            if (subspecies.name != result.Text) subspecies.setName(result.Text);
            return true;
        }
    }

    [HarmonyPatch(typeof(ItemManager), nameof(ItemManager.generateItem))]
    internal static class ItemOrigin
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) =>
            ReplaceCall(codes, AccessTools.Method(typeof(ItemManager), nameof(ItemManager.generateModsFor)),
                nameof(GenerateItemMods), 2);
    }

    private static void GenerateItemMods(ItemManager manager, Item item, int tries, Actor maker, bool addName, Kingdom origin)
    {
        // Vanilla records the kingdom after rolling the legendary name.
        if (origin != null) item.data.creator_kingdom_id = origin.id;
        manager.generateModsFor(item, tries, maker, addName);
    }

    [HarmonyPatch(typeof(ItemManager), nameof(ItemManager.checkModName))]
    internal static class LegendaryName
    {
        private static bool Prefix(ItemManager __instance, Item pItem, ItemModAsset pModAsset,
            EquipmentAsset pItemAsset, Actor pActor, ref string pName, ref bool __result)
        {
            if (pModAsset.quality != Rarity.R3_Legendary) return true;
            string name = EquipmentName(pItem, pItemAsset, pActor, Seed(pItem.id), "legendary",
                accept: candidate => !__instance.unique_legendary_names.Contains(candidate));
            if (name == null) return true;
            pName = name;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Item), nameof(Item.reforge))]
    internal static class ReforgeName
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var noIntermediate = ReplaceCall(codes, AccessTools.Method(typeof(ItemManager), nameof(ItemManager.generateModsFor)),
                nameof(ReforgeMods));
            return ReplaceCall(noIntermediate, AccessTools.Method(typeof(NanoObject), nameof(NanoObject.setName)), nameof(SetReforgedName));
        }
    }

    private static void ReforgeMods(ItemManager manager, Item item, int tries, Actor actor, bool addName) =>
        manager.generateModsFor(item, tries, actor, pAddName: false);

    private static void SetReforgedName(Item item, string vanilla, bool track)
    {
        if (item.data.custom_name && !string.IsNullOrWhiteSpace(item.data.name)) return;
        const string reforges = "chinesename.reforges.v1";
        item.data.get(reforges, out int count);
        item.data.set(reforges, count + 1);
        var maker = World.world.units.get(item.data.creator_id);
        item.setName(EquipmentName(item, item.asset, maker, Seed(item.id + count + 1), "reforge") ?? vanilla, track);
    }

    private static string EquipmentName(Item item, EquipmentAsset asset, Actor maker, long seed, string stage,
        Func<string, bool> accept = null)
    {
        var origin = World.world.kingdoms.get(item.data.creator_kingdom_id);
        var inputs = new Dictionary<string, object> { ["stage"] = stage };
        if (asset?.group_id is { Length: > 0 } equipment) inputs["equipment"] = equipment.ToLowerInvariant().Replace('_', '-');
        string material = string.IsNullOrEmpty(item.data.material) ? asset?.material : item.data.material;
        if (!string.IsNullOrEmpty(material)) inputs["material"] = material.ToLowerInvariant().Replace('_', '-');
        return VanillaNames.Generate("worldbox:item", maker, seed, kind: stage == "reforge" ? stage : asset?.group_id,
            accept: accept, inputs: inputs,
            culture: maker?.culture?.id ?? origin?.culture?.id, language: maker?.language?.id ?? origin?.language?.id);
    }

    internal static string GenerateExistingItem(Item item, Func<string, bool> accept = null)
    {
        var maker = World.world.units.get(item.data.creator_id);
        item.data.get("chinesename.reforges.v1", out int reforges);
        if (reforges <= 0)
            return EquipmentName(item, item.asset, maker, Seed(item.id), "legendary", accept);
        return EquipmentName(item, item.asset, maker, Seed(item.id + reforges), "reforge", accept);
    }

    [HarmonyPatch]
    internal static class UndeadNames
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ActionLibrary), nameof(ActionLibrary.turnIntoZombie));
            yield return AccessTools.Method(typeof(ActionLibrary), nameof(ActionLibrary.turnIntoSkeleton));
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) =>
            ReplaceCall(codes, AccessTools.Method(typeof(NanoObject), nameof(NanoObject.setName)), nameof(KeepUndeadName));
    }

    private static void KeepUndeadName(Actor actor, string _, bool track) { }

    [HarmonyPatch(typeof(Kingdom), nameof(Kingdom.getMotto))]
    internal static class KingdomMotto
    {
        private static void Prefix(Kingdom __instance)
        {
            if (string.IsNullOrEmpty(__instance.data.motto))
                __instance.data.motto = FromKingdom("worldbox:motto", __instance, __instance.id, "kingdom");
        }
    }

    [HarmonyPatch(typeof(Clan), nameof(Clan.getMotto))]
    internal static class ClanMotto
    {
        private static void Prefix(Clan __instance)
        {
            if (!string.IsNullOrEmpty(__instance.data.motto)) return;
            var data = __instance.data;
            var source = __instance.getChief() ?? World.world.units.get(data.founder_actor_id);
            var city = World.world.cities.get(data.founder_city_id);
            var kingdom = World.world.kingdoms.get(data.founder_kingdom_id);
            data.motto = VanillaNames.Generate("worldbox:motto", source, Seed(__instance.id), kind: "clan",
                culture: source?.culture?.id ?? (data.name_culture_id >= 0 ? data.name_culture_id : city?.culture?.id ?? kingdom?.culture?.id),
                language: source?.language?.id ?? city?.language?.id ?? kingdom?.language?.id);
        }
    }

    [HarmonyPatch(typeof(Alliance), nameof(Alliance.getMotto))]
    internal static class AllianceMotto
    {
        private static void Prefix(Alliance __instance)
        {
            if (string.IsNullOrEmpty(__instance.data.motto))
                __instance.data.motto = FromKingdom("worldbox:motto",
                    World.world.kingdoms.get(__instance.data.founder_kingdom_id), __instance.id, "alliance");
        }
    }
}
