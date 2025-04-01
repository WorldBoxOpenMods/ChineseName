using System.Collections.Generic;
using System.Reflection.Emit;
using Chinese_Name.constants;
using Chinese_Name.utils;
using HarmonyLib;
using NeoModLoader.api.attributes;

namespace Chinese_Name;

public class ActorNamePatch : IPatch
{
    public void Initialize()
    {
        Harmony harmony = new Harmony(nameof(set_actor_name));
        harmony.Patch(AccessTools.Method(typeof(Actor), nameof(Actor.getName)),
            prefix: new HarmonyMethod(AccessTools.Method(GetType(), nameof(set_actor_name))));
        harmony.Patch(AccessTools.Method(typeof(ActionLibrary), nameof(ActionLibrary.turnIntoZombie)),
            transpiler: new HarmonyMethod(AccessTools.Method(GetType(), nameof(undead_creature_name))));
        harmony.Patch(AccessTools.Method(typeof(ActionLibrary), nameof(ActionLibrary.turnIntoSkeleton)),
            transpiler: new HarmonyMethod(AccessTools.Method(GetType(), nameof(undead_creature_name))));
    }
    [Hotfixable]
    private static bool set_actor_name(Actor __instance)
    {
        if (!string.IsNullOrWhiteSpace(__instance.data.name)) return true;
        var template_id = __instance.GetNameTemplate(MetaType.Unit);
        if (__instance.asset.civ)
        {
            template_id = "human_name";
        }
        else
        {
            template_id = "default_name";
        }
        var generator = CN_NameGeneratorLibrary.Instance.get(template_id);
        if (generator == null) return true;
        int max_try = 10;

        var para = new Dictionary<string, string>();
        ParameterGetters.GetActorParameterGetter(generator.parameter_getter)(__instance.a, para);

        __instance.data.get(DataS.family_name, out var family_name, "");
        if (string.IsNullOrEmpty(family_name))
        {
            foreach (var parent in __instance.getParents())
            {
                parent.data.get(DataS.family_name, out family_name, "");
                if (!string.IsNullOrEmpty(family_name)) break;
            }
        }
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