using System.Collections.Generic;
using Chinese_Name.utils;
using HarmonyLib;

namespace Chinese_Name;

public class SubspeciesNamePatch : IPatch
{
    public void Initialize()
    {
        new Harmony(nameof(set_subspecies_name)).Patch(AccessTools.Method(typeof(Subspecies), nameof(Subspecies.generateName)),
            postfix: new HarmonyMethod(GetType(), nameof(set_subspecies_name)));
    }
    private static void set_subspecies_name(Subspecies __instance)
    {
        var template_id = "default_species";
        var generator = CN_NameGeneratorLibrary.Instance.get(template_id);
        if (generator == null) return;

        var para = new Dictionary<string, string>();

        ParameterGetters.GetSubspeciesParameterGetter(generator.parameter_getter)(__instance, para);

        __instance.data.name = generator.GenerateName(para);
    }
}