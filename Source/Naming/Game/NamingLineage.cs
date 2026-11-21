using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace Chinese_Name.Naming.Game;

internal static class NamingLineage
{
    private static Actor _languageFounder;
    private static Language _parentLanguage;

    internal static void ConnectLanguageSplit()
    {
        var plot = AssetManager.plots_library.get("language_divergence");
        var original = plot?.action;
        if (original == null) return;
        plot.action = actor =>
        {
            var previousFounder = _languageFounder;
            var previousParent = _parentLanguage;
            _languageFounder = actor;
            _parentLanguage = actor.language;
            try { return original(actor); }
            finally
            {
                _languageFounder = previousFounder;
                _parentLanguage = previousParent;
            }
        };
    }

    private static ulong Seed(long id) =>
        unchecked((ulong)(World.world?.map_stats?.life_dna ?? 0) ^ (ulong)id);

    [HarmonyPatch(typeof(Culture), nameof(Culture.createCulture))]
    internal static class HumanCultureFounding
    {
        private static void Postfix(Culture __instance, Actor pActor)
        {
            if (pActor.asset.id != "human" || pActor.hasCulture()) return;
            var world = WorldNamingStore.LineageWorld();
            if (world == null || world.Cultures.ContainsKey(__instance.id)) return;
            string pack = NamingApi.Current.PickHumanCulturePack(world, Seed(__instance.id));
            if (pack == null) return;
            var settings = new CultureNaming { Pack = pack };
            // 原版旧地图只保留已有文化的存量排列，新文化继续用抽中的预设。
            if (world.UseSavedVanillaCards)
                foreach (var meta in NativeOnomastics.Categories)
                    settings.Cards[NativeOnomastics.Type(meta)] = new CardNaming();
            WorldNamingStore.SetCulture(__instance.id, settings);
        }
    }

    [HarmonyPatch(typeof(Culture), nameof(Culture.cloneAndEvolveOnomastics))]
    internal static class CultureSplit
    {
        private static void Postfix(Culture __instance, Culture pFrom)
        {
            // 词库名称指向命名包，分化时按父文化原样继承。
            foreach (var entry in __instance._onomastics_data)
            {
                var parentCards = pFrom._onomastics_data[entry.Key];
                foreach (string group in NativeOnomastics.PoolSelectorGroups(entry.Value).Distinct())
                    if (parentCards.getGroupString(group) is { Length: > 0 } alias)
                        entry.Value.setGroup(group, alias);
            }
            var world = WorldNamingStore.LineageWorld();
            if (world == null || world.Cultures.TryGetValue(__instance.id, out var existing) && existing.Parent.HasValue) return;
            var child = world.Cultures.TryGetValue(pFrom.id, out var parent)
                ? NamingJson.Copy(parent) : new CultureNaming();
            child.Parent = pFrom.id;
            foreach (var meta in NativeOnomastics.Categories)
            {
                string type = NativeOnomastics.Type(meta);
                var choice = NativeOnomastics.Selection(world, pFrom, type);
                if (choice != null) child.Cards[type] = NamingJson.Copy(choice);
            }
            var founder = World.world.units.get(__instance.data.creator_id);
            NamingApi.Current.EvolveCulture(child, world, Seed(__instance.id),
                GeographicNaming.At(founder?.current_tile)?.Landform, __instance.data.original_actor_asset);
            WorldNamingStore.SetCulture(__instance.id, child);
        }
    }

    [HarmonyPatch(typeof(Language), nameof(Language.newLanguage))]
    internal static class NewLanguage
    {
        private static void Postfix(Language __instance, Actor pActor)
        {
            var parent = ReferenceEquals(pActor, _languageFounder) ? _parentLanguage : null;
            if (parent?.id == __instance.id) return;
            var world = WorldNamingStore.LineageWorld();
            if (world == null || world.Languages.ContainsKey(__instance.id)) return;
            var language = parent != null && world.Languages.TryGetValue(parent.id, out var previous)
                ? NamingJson.Copy(previous) : new LanguageNaming();
            var service = NamingApi.Current;
            var preferred = NativeOnomastics.CulturePools(service, pActor);
            bool diversified = service.EvolveLanguage(language, world, Seed(__instance.id), parent != null, preferred,
                pActor?.culture?.id, GeographicNaming.At(pActor?.current_tile)?.Landform);
            if (parent == null && !diversified) return;
            language.BiasOrigin ??= parent?.id;
            WorldNamingStore.SetLanguage(__instance.id, language, parent?.id);
        }
    }

    [HarmonyPatch(typeof(Subspecies), nameof(Subspecies.setSkeletonForm))]
    internal static class RenameSkeletonSubspecies
    {
        private static bool Prefix(Subspecies __instance, Subspecies pSkeletonForm)
        {
            string name = Derive(pSkeletonForm, "skeleton", __instance.name);
            if (name == null) return true;
            __instance.data.skeleton_form_id = pSkeletonForm.id;
            pSkeletonForm.setName(name, pTrack: false);
            return false;
        }
    }

    [HarmonyPatch(typeof(ActorManager), nameof(ActorManager.evolutionEvent))]
    internal static class UpliftName
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) =>
            IndependentNames.ReplaceCall(codes, AccessTools.Method(typeof(NanoObject), nameof(NanoObject.setName)),
                AccessTools.Method(typeof(NamingLineage), nameof(SetUpliftedName)));
    }

    private static void SetUpliftedName(Subspecies subspecies, string vanilla, bool track)
    {
        if (subspecies.data.custom_name) return;
        subspecies.setName(Derive(subspecies, "uplift", subspecies.name) ?? vanilla, track);
    }

    private static string Derive(Subspecies target, string kind, string origin)
    {
        if (target.data.custom_name) return target.name;
        var inputs = new Dictionary<string, object>
        {
            ["kind"] = kind,
            ["origin"] = origin ?? "",
            ["species"] = target.getActorAsset().id
        };
        var request = VanillaNames.Request("worldbox:subspecies", null, unchecked((ulong)target.id),
            inputs: inputs, subject: target, tile: IndependentNames.SubspeciesName.Origin(target));
        var result = NamingApi.Current.Generate(request, name => !World.world.subspecies.Any(other => other != target && other.name == name));
        return result.Success ? result.Text : null;
    }
}
