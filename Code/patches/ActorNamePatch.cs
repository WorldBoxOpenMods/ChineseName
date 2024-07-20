using System.Collections.Generic;
using System.Reflection.Emit;
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
        harmony.Patch(AccessTools.Method(typeof(ActionLibrary), nameof(ActionLibrary.turnIntoZombie)),
            transpiler: new HarmonyMethod(AccessTools.Method(GetType(), nameof(undead_creature_name))));
        harmony.Patch(AccessTools.Method(typeof(ActionLibrary), nameof(ActionLibrary.turnIntoSkeleton)),
            transpiler: new HarmonyMethod(AccessTools.Method(GetType(), nameof(undead_creature_name))));
    }

    private static void set_actor_family_name(Clan __instance, Actor pActor)
    {
        string tmp = "";
        foreach (Actor unit in __instance.units.Values)
        {
            unit.data.get(DataS.family_name, out tmp, "");
            if (!string.IsNullOrWhiteSpace(tmp))
            {
                pActor.data.set(DataS.family_name, tmp);
                return;
            }
        }
    }

    private static bool set_actor_name(ActorBase __instance)
    {
        if (!string.IsNullOrWhiteSpace(__instance.data.name)) return true;
        var generator = CN_NameGeneratorLibrary.Instance.get(__instance.asset.nameTemplate);
        if (generator == null) return true;
        int max_try = 10;

        var para = new Dictionary<string, string>();
        ParameterGetters.GetActorParameterGetter(generator.param_getters)(__instance.a, para);

        __instance.data.get(DataS.family_name, out var family_name, "");
        para[DataS.family_name_in_template] = family_name;

        __instance.data.name = generator.GenerateName(para);

        para.TryGetValue(DataS.family_name_in_template, out family_name);
        __instance.data.set(DataS.family_name, family_name);

        return true;
    }

    private static IEnumerable<CodeInstruction> undead_creature_name(IEnumerable<CodeInstruction> pIntro)
    {
        var codes = new List<CodeInstruction>(pIntro);
        var idx = codes.FindIndex(x => x.opcode == OpCodes.Ldstr && (string)x.operand == "Un");
        if (idx != -1) codes[idx].operand = "亡-";
        return codes;
    }
}