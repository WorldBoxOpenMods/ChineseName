using System.Collections.Generic;
using HarmonyLib;
using NeoModLoader.General.Event.Handlers;
using NeoModLoader.General.Event.Listeners;

namespace Chinese_Name;

public class KingdomNamePatch : IPatch
{
    class RenameKingdom : KingdomSetupHandler
    {
        public override void Handle(Kingdom pKingdom, bool pCiv)
        {
            if (!pCiv) return;
            if (!string.IsNullOrWhiteSpace(pKingdom.data.name)) return;

            string name_generator_id = "human_kingdom";
            if (pKingdom.race == null)
            {
                ModClass.LogWarning($"No found race for kingdom {pKingdom.id} at {pKingdom.location}, use default name generator(human).");
            }
            else
            {
                name_generator_id = pKingdom.race.name_template_kingdom;
            }

            var asset = CN_NameGeneratorLibrary.Instance.get(name_generator_id);
            if (asset == null) return;

            var para = new Dictionary<string, string>();
            
            ParameterGetters.GetKingdomParameterGetter(asset.parameter_getter)(pKingdom, para);
            
            int max_try = 10;
            while (!string.IsNullOrWhiteSpace(pKingdom.data.name) && max_try-- > 0)
            {
                var template = asset.GetRandomTemplate();
                pKingdom.data.name = template.GenerateName(para);
            }
        }
    }
    public void Initialize()
    {
        KingdomSetupListener.RegisterHandler(new RenameKingdom());
        new Harmony(nameof(set_kingdom_motto)).Patch(AccessTools.Method(typeof(Kingdom), nameof(Kingdom.getMotto)),
            prefix: new HarmonyMethod(AccessTools.Method(GetType(), nameof(set_kingdom_motto))));
    }

    private static bool set_kingdom_motto(Kingdom __instance)
    {
        if (!string.IsNullOrWhiteSpace(__instance.data.motto)) return true;
        var generator = CN_NameGeneratorLibrary.Instance.get("kingdom_mottos");
        if (generator == null) return true;

        var para = new Dictionary<string, string>();
            
        ParameterGetters.GetKingdomParameterGetter(generator.parameter_getter)(__instance, para);

        int max_try = 10;
        while (!string.IsNullOrWhiteSpace(__instance.data.name) && max_try-- > 0)
        {
            var template = generator.GetRandomTemplate();
            __instance.data.motto = template.GenerateName(para);
        }
        return true;
    }
}