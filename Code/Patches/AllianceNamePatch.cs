using System.Collections.Generic;
using HarmonyLib;
using NeoModLoader.General.Event.Handlers;
using NeoModLoader.General.Event.Listeners;

namespace Chinese_Name;

public class AllianceNamePatch : IPatch
{
    public void Initialize()
    {
        new Harmony(nameof(set_alliance_name)).Patch(
            AccessTools.Method(typeof(WorldLog), nameof(WorldLog.logAllianceCreated)),
            prefix: new HarmonyMethod(typeof(AllianceNamePatch), nameof(set_alliance_name)));
        new Harmony(nameof(set_alliance_motto)).Patch(AccessTools.Method(typeof(Alliance), nameof(Alliance.getMotto)),
            prefix: new HarmonyMethod(AccessTools.Method(GetType(), nameof(set_alliance_motto))));
    }

    private static bool set_alliance_motto(Alliance __instance)
    {
        if (!string.IsNullOrWhiteSpace(__instance.data.motto)) return true;
        var generator = CN_NameGeneratorLibrary.Instance.get("alliance_mottos");
        if (generator == null) return true;

        var para = new Dictionary<string, string>();

        ParameterGetters.GetAllianceParameterGetter(generator.parameter_getter)(__instance, para);

        __instance.data.motto = generator.GenerateName(para);

        return true;
    }
    private static void set_alliance_name(Alliance pAlliance)
    {
        var generator = CN_NameGeneratorLibrary.Instance.get("alliance_name");
        if (generator == null) return;

        var para = new Dictionary<string, string>();

        ParameterGetters.GetAllianceParameterGetter(generator.parameter_getter)(pAlliance, para);

        pAlliance.data.name = generator.GenerateName(para);
    }
}