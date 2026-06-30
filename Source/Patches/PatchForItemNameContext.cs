using System;
using Chinese_Name.Abstract;
using HarmonyLib;

namespace Chinese_Name.Patches;

internal class PatchForItemNameContext : IPatch
{
    [HarmonyPrefix, HarmonyPatch(typeof(ItemManager), "checkModName")]
    private static void checkModName_prefix(Item pItem, ItemModAsset pModAsset, EquipmentAsset pItemAsset, Actor pActor,
        out IDisposable __state)
    {
        var context = NameGenerationContextScope.Current?.Fork() ?? new NameGenerationContext();
        context.Source = "item";
        context.Actor = pActor;
        context.Kingdom = pActor?.kingdom;
        context.Item = pItem;
        context.EquipmentAsset = pItemAsset ?? pItem?.getAsset();
        context.ItemModAsset = pModAsset;
        __state = NameGenerationContextScope.Push(context);
    }

    [HarmonyFinalizer, HarmonyPatch(typeof(ItemManager), "checkModName")]
    private static Exception checkModName_finalizer(Exception __exception, IDisposable __state)
    {
        __state?.Dispose();
        return __exception;
    }
}
