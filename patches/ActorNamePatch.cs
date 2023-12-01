using Chinese_Name.constants;
using HarmonyLib;

namespace Chinese_Name;

public class ActorNamePatch : IPatch
{
    public void Initialize()
    {
        Harmony harmony = new Harmony(nameof(set_actor_name));
        harmony.Patch(AccessTools.Method(typeof(ActorBase), nameof(ActorBase.getName)),
            prefix: new HarmonyMethod(AccessTools.Method(GetType(), nameof(set_actor_name))));
        harmony.Patch(AccessTools.Method(typeof(Clan), nameof(Clan.addUnit)),
            postfix: new HarmonyMethod(AccessTools.Method(GetType(), nameof(set_actor_family_name))));
    }

    private static void set_actor_family_name(Clan __instance, Actor pActor)
    {
        string tmp = "";
        foreach (Actor unit in __instance.units.Values)
        {
            unit.data.get(DataS.family_name, out tmp, "");
            if (!string.IsNullOrEmpty(tmp))
            {
                pActor.data.set(DataS.family_name, tmp);
                return;
            }
        }
    }
    private static bool set_actor_name(ActorBase __instance)
    {
        if (!string.IsNullOrEmpty(__instance.data.name)) return true;
        var generator = CN_NameGeneratorLibrary.Instance.get(__instance.asset.nameTemplate);
        if (generator == null) return true;
        var template = generator.GetRandomTemplate();
        var para = template.GetParametersToFill();
        
        __instance.data.get(DataS.family_name, out var family_name, "");
        para[DataS.family_name] = family_name;
        
        __instance.data.name = template.GenerateName(para);
        
        para.TryGetValue(DataS.family_name, out family_name);
        __instance.data.set(DataS.family_name, family_name);
        
        return true;
    }
}