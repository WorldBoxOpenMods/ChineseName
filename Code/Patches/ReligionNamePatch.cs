using System.Collections.Generic;
using Chinese_Name.utils;
using HarmonyLib;

namespace Chinese_Name;

public class ReligionNamePatch : IPatch
{
    public void Initialize()
    {
        new Harmony(nameof(set_religion_name)).Patch(AccessTools.Method(typeof(Religion), nameof(Religion.generateName)),
            postfix: new HarmonyMethod(GetType(), nameof(set_religion_name)));
    }
    private static void set_religion_name(Religion __instance, Actor pActor)
    {
        var template_id = pActor.GetNameTemplate(MetaType.Religion);
        template_id = "human_religion";
        var generator = CN_NameGeneratorLibrary.Instance.get(template_id);
        if (generator == null) return;

        var para = new Dictionary<string, string>();

        ParameterGetters.GetReligionParameterGetter(generator.parameter_getter)(__instance, para);

        __instance.data.name = generator.GenerateName(para);
    }
}