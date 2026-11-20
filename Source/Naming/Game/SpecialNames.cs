using HarmonyLib;

namespace Chinese_Name.Naming.Game;

internal static class SpecialNames
{
    internal const string VoidElves = "虚空精灵";
    internal const string VanillaVoidElves = "Elfus Voidus";

    internal static string AngleMarker(string name) => name switch
    {
        "DAB" => "丁甲乙",
        "ABC" => "甲乙丙",
        "CDA" => "丙丁甲",
        "BCD" => "乙丙丁",
        "丁甲乙" or "甲乙丙" or "丙丁甲" or "乙丙丁" => name,
        _ => null
    };

    [HarmonyPatch(typeof(CursedSacrifice), nameof(CursedSacrifice.getVoidElvesSubspecies))]
    internal static class VoidEvent
    {
        private static void Postfix(Subspecies __result)
        {
            if (__result != null && !__result.data.custom_name && __result.name == VanillaVoidElves)
                __result.setName(VoidElves, pTrack: false);
        }
    }

    [HarmonyPatch(typeof(NanoObject), nameof(NanoObject.setName))]
    internal static class StartupAngles
    {
        private static void Prefix(NanoObject __instance, ref string pName, bool pTrack)
        {
            if (pTrack || __instance is not Actor { asset: { id: "angle" }, current_tile: { } tile } actor ||
                actor.data.custom_name || World.world == null) return;
            // The four labels identify corners in the original startup sequence.
            if ((tile.x == 0 || tile.x == MapBox.width - 1) && (tile.y == 0 || tile.y == MapBox.height - 1))
                pName = AngleMarker(pName) ?? pName;
        }
    }
}
