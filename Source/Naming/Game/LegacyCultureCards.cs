using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Chinese_Name.Naming.Game;

internal static class LegacyCultureCards
{
    private static readonly ConditionalWeakTable<CultureData, Dictionary<MetaType, string>> Unavailable = new();

    internal static string Preserved(CultureData data, MetaType meta) =>
        Unavailable.TryGetValue(data, out var templates) && templates.TryGetValue(meta, out string template) ? template : null;

    [HarmonyPatch(typeof(Culture), nameof(Culture.loadData))]
    internal static class Load
    {
        private static void Prefix(CultureData pData, out Dictionary<MetaType, string> __state)
        {
            __state = null;
            Unavailable.Remove(pData);
            if (pData.onomastics == null) return;
            foreach (var slot in pData.onomastics)
            {
                using var cards = NativeNamingArrangement.ReadTemplate(slot.Value);
                if (cards != null) continue;
                __state ??= new Dictionary<MetaType, string>();
                __state.Add(slot.Key, slot.Value);
            }
            if (__state == null) return;
            Unavailable.Add(pData, __state);
            foreach (var slot in __state) pData.onomastics.Remove(slot.Key);
        }

        private static Exception Finalizer(CultureData pData, Dictionary<MetaType, string> __state, Exception __exception)
        {
            if (__state != null) Restore(pData, __state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Culture), nameof(Culture.save))]
    internal static class Save
    {
        private static void Postfix(Culture __instance)
        {
            if (Unavailable.TryGetValue(__instance.data, out var templates)) Restore(__instance.data, templates);
        }
    }

    private static void Restore(CultureData data, Dictionary<MetaType, string> templates)
    {
        data.onomastics ??= new Dictionary<MetaType, string>();
        foreach (var slot in templates) data.onomastics[slot.Key] = slot.Value;
    }
}
