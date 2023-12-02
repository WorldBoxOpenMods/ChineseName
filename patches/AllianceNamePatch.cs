using System.Collections.Generic;
using HarmonyLib;
using NeoModLoader.General.Event.Handlers;
using NeoModLoader.General.Event.Listeners;

namespace Chinese_Name;

public class AllianceNamePatch : IPatch
{
    class RenameAlliance : AllianceCreateHandler
    {
        public override void Handle(Alliance pAlliance, Kingdom pKingdom, Kingdom pKingdom2)
        {
            if (!string.IsNullOrWhiteSpace(pAlliance.data.name)) return;
            var generator = CN_NameGeneratorLibrary.Instance.get("alliance_name");
            if (generator == null) return;
            
            var para = new Dictionary<string, string>();
            
            ParameterGetters.GetAllianceParameterGetter(generator.parameter_getter)(pAlliance, para);
            
            int max_try = 10;
            while (string.IsNullOrWhiteSpace(pAlliance.data.name) && max_try-- > 0)
            {
                var template = generator.GetRandomTemplate();
                pAlliance.data.name = template.GenerateName(para);
            }
        }
    }

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
            
        ParameterGetters.GetAllianceParameterGetter(generator.parameter_getter)(__instance, para);
        
        int max_try = 10;
        while (string.IsNullOrWhiteSpace(__instance.data.motto) && max_try-- > 0)
        {
            var template = generator.GetRandomTemplate();
            __instance.data.motto = template.GenerateName(para);
        }

        return true;
    }
}