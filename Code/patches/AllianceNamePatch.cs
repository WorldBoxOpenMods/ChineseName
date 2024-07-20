using System.Collections.Generic;
using HarmonyLib;
using NeoModLoader.General.Event.Handlers;
using NeoModLoader.General.Event.Listeners;

namespace Chinese_Name;

public class AllianceNamePatch : IPatch
{
    public void Initialize()
    {
        AllianceCreateListener.RegisterHandler(new RenameAlliance());
        new Harmony(nameof(set_alliance_motto)).Patch(AccessTools.Method(typeof(Alliance), nameof(Alliance.getMotto)),
            prefix: new HarmonyMethod(AccessTools.Method(GetType(), nameof(set_alliance_motto))));
    }

    private static bool set_alliance_motto(Alliance __instance)
    {
        if (!string.IsNullOrWhiteSpace(__instance.data.motto)) return true;
        var generator = CN_NameGeneratorLibrary.Instance.get("alliance_mottos");
        if (generator == null) return true;

        var para = new Dictionary<string, string>();

        ParameterGetters.GetAllianceParameterGetter(generator.param_getters)(__instance, para);

        __instance.data.motto = generator.GenerateName(para);

        return true;
    }

    class RenameAlliance : AllianceCreateHandler
    {
        public override void Handle(Alliance pAlliance, Kingdom pKingdom, Kingdom pKingdom2)
        {
            if (!string.IsNullOrWhiteSpace(pAlliance.data.name)) return;
            var generator = CN_NameGeneratorLibrary.Instance.get("alliance_name");
            if (generator == null) return;

            var para = new Dictionary<string, string>();

            ParameterGetters.GetAllianceParameterGetter(generator.param_getters)(pAlliance, para);

            pAlliance.data.name = generator.GenerateName(para);
        }
    }
}