using Chinese_Name.Abstract;
using HarmonyLib;

namespace Chinese_Name.Patches;

internal class PatchForItemNameContext : IPatch
{
    [HarmonyPrefix, HarmonyPatch(typeof(ItemManager), nameof(ItemManager.generateModsFor))]
    private static void generateModsFor_prefix(Item pItem, Actor pActor)
    {
        var context = NameGenerationContextScope.Current?.Fork() ?? new NameGenerationContext();
        context.Source = "item";
        context.Actor = pActor;
        context.Kingdom = pActor?.kingdom;
        context.Item = pItem;
        context.EquipmentAsset = pItem?.getAsset();
        context.ItemModAsset = null;
        NameGenerationContextScope.Push(context);
    }

    [HarmonyFinalizer, HarmonyPatch(typeof(ItemManager), nameof(ItemManager.generateModsFor))]
    private static void generateModsFor_finalizer()
    {
        NameGenerationContextScope.PopCurrent();
    }
}
