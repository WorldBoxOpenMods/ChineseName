using HarmonyLib;
using NeoModLoader.General.Event.Handlers;
using NeoModLoader.General.Event.Listeners;

namespace Chinese_Name;

public class ClanNamePatch : IPatch
{
    class RenameClan : ClanCreateHandler
    {
        public override void Handle(Clan pClan, Actor pFounder)
        {
            if (!string.IsNullOrEmpty(pClan.data.name)) return;
            if (pFounder == null) return;

            var asset = CN_NameGeneratorLibrary.Instance.get(pFounder.race.name_template_clan);
            if (asset == null) return;
            var template = asset.GetRandomTemplate();
            var para = template.GetParametersToFill();
            pClan.data.name = template.GenerateName(para);
        }
    }
    public void Initialize()
    {
        ClanCreateListener.RegisterHandler(new RenameClan());
        new Harmony(nameof(set_clan_motto)).Patch(AccessTools.Method(typeof(Clan), nameof(Clan.getMotto)),
            prefix: new HarmonyMethod(AccessTools.Method(GetType(), nameof(set_clan_motto))));
    }

    private static bool set_clan_motto(Clan __instance)
    {
        if (!string.IsNullOrEmpty(__instance.data.motto)) return true;
        var generator = CN_NameGeneratorLibrary.Instance.get("clan_mottos");
        if (generator == null) return true;
        var template = generator.GetRandomTemplate();
        var para = template.GetParametersToFill();
        __instance.data.motto = template.GenerateName(para);
        return true;
    }
}