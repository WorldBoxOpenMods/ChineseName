using System.Collections.Generic;
using HarmonyLib;
using NeoModLoader.General.Event.Handlers;
using NeoModLoader.General.Event.Listeners;

namespace Chinese_Name;

public class WarNamePatch : IPatch
{
    public void Initialize()
    {
        new Harmony(nameof(set_war_name)).Patch(AccessTools.Method(typeof(WarManager), nameof(WarManager.newWar)),
            postfix: new HarmonyMethod(typeof(WarNamePatch), nameof(set_war_name)));
        
        WarStartListener.RegisterHandler(new RenameWar());
    }

    private static void set_war_name(War __result, Kingdom pAttacker, Kingdom pDefender, WarTypeAsset pType)
    {
        if (pDefender != null && pDefender.getAge() <= 1)
        {
            pType = WarTypeLibrary.rebellion;
        }

        var generator = CN_NameGeneratorLibrary.Instance.get(pType.name_template);
        if (generator == null) return;

        var para = new Dictionary<string, string>();
        ParameterGetters.GetWarParameterGetter(generator.parameter_getter)(__result, para);

        __result.data.name = generator.GenerateName(para);
    }
    class RenameWar : WarStartHandler
    {
        public override void Handle(War pWar, Kingdom pAttacker, Kingdom pDefender, WarTypeAsset pWarType)
        {
            if (!string.IsNullOrWhiteSpace(pWar.data.name)) return;
            if (pDefender != null && pDefender.getAge() <= 1)
            {
                pWarType = WarTypeLibrary.rebellion;
            }

            var generator = CN_NameGeneratorLibrary.Instance.get(pWarType.name_template);
            if (generator == null) return;

            var para = new Dictionary<string, string>();
            ParameterGetters.GetWarParameterGetter(generator.parameter_getter)(pWar, para);

            pWar.data.name = generator.GenerateName(para);
        }
    }
}