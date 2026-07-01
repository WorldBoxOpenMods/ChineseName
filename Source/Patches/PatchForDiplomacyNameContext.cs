using Chinese_Name.Abstract;
using HarmonyLib;

namespace Chinese_Name.Patches;

internal class PatchForDiplomacyNameContext : IPatch
{
    [HarmonyPrefix, HarmonyPatch(typeof(WarManager), nameof(WarManager.newWar))]
    private static void newWar_prefix(Kingdom pAttacker, Kingdom pDefender, WarTypeAsset pType)
    {
        var context = NameGenerationContextScope.Current?.Fork() ?? new NameGenerationContext();
        context.Source = "war";
        context.PrimaryKingdom = pAttacker;
        context.SecondaryKingdom = pDefender;
        context.Kingdom = pType != null && !pType.kingdom_for_name_attacker ? pDefender : pAttacker;
        NameGenerationContextScope.Push(context);
    }

    [HarmonyFinalizer, HarmonyPatch(typeof(WarManager), nameof(WarManager.newWar))]
    private static void newWar_finalizer()
    {
        NameGenerationContextScope.PopCurrent();
    }

    [HarmonyPrefix, HarmonyPatch(typeof(AllianceManager), nameof(AllianceManager.newAlliance))]
    private static void newAlliance_prefix(Kingdom pKingdom, Kingdom pKingdom2)
    {
        var context = NameGenerationContextScope.Current?.Fork() ?? new NameGenerationContext();
        context.Source = "alliance";
        context.PrimaryKingdom = pKingdom;
        context.SecondaryKingdom = pKingdom2;
        context.Kingdom = pKingdom;
        NameGenerationContextScope.Push(context);
    }

    [HarmonyFinalizer, HarmonyPatch(typeof(AllianceManager), nameof(AllianceManager.newAlliance))]
    private static void newAlliance_finalizer()
    {
        NameGenerationContextScope.PopCurrent();
    }
}
