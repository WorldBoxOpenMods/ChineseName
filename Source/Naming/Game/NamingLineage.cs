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
            WorldNamingStore.Update(draft =>
            {
                if (draft.Cultures.TryGetValue(__instance.id, out var existing) && existing.Parent.HasValue) return false;
                var child = draft.Cultures.TryGetValue(pFrom.id, out var parent)
                    ? NamingJson.Copy(parent) : new CultureNaming();
                child.Parent = pFrom.id;
                foreach (var meta in NativeOnomastics.Categories)
                {
                    string type = NativeOnomastics.Type(meta);
                    var choice = NativeOnomastics.Selection(draft, pFrom, type);
                    if (choice != null) child.Cards[type] = NamingJson.Copy(choice);
                }
                NamingApi.Current.EvolveCulture(child, draft, Seed(__instance.id));
                draft.Cultures[__instance.id] = child;
                return true;
            });
        }
    }

    [HarmonyPatch(typeof(Language), nameof(Language.newLanguage))]
    internal static class NewLanguage
    {
        private static void Postfix(Language __instance, Actor pActor)
        {
            var parent = ReferenceEquals(pActor, _languageFounder) ? _parentLanguage : null;
            if (parent?.id == __instance.id) return;
            WorldNamingStore.Update(draft =>
            {
                if (draft.Languages.ContainsKey(__instance.id)) return false;
                var language = parent != null && draft.Languages.TryGetValue(parent.id, out var previous)
                    ? NamingJson.Copy(previous) : new LanguageNaming();
                var service = NamingApi.Current;
                var preferred = NativeOnomastics.CulturePools(service, pActor);
                bool diversified = service.EvolveLanguage(language, draft, Seed(__instance.id), parent != null, preferred, pActor?.culture?.id);
                if (parent == null && !diversified) return false;
                if (parent != null)
                {
                    draft.LanguageParents[__instance.id] = parent.id;
                    language.BiasOrigin ??= parent.id;
                }
                draft.Languages[__instance.id] = language;
                return true;
            });
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
        var result = NamingApi.Current.Generate(new NameRequest("worldbox:subspecies", unchecked((ulong)target.id),
            inputs: inputs), name => !World.world.subspecies.Any(other => other != target && other.name == name));
        return result.Success ? result.Text : null;
    }
}
