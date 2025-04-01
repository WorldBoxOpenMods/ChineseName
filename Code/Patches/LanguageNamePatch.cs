using System.Collections.Generic;
using Chinese_Name.utils;
using HarmonyLib;

namespace Chinese_Name;

public class LanguageNamePatch : IPatch
{
    public void Initialize()
    {
        new Harmony(nameof(set_lang_name)).Patch(AccessTools.Method(typeof(Language), nameof(Language.generateName)),
            postfix: new HarmonyMethod(GetType(), nameof(set_lang_name)));
    }
    private static void set_lang_name(Language __instance, Actor pActor)
    {
        var template_id = pActor.GetNameTemplate(MetaType.Language);
        template_id = "human_lang";
        var generator = CN_NameGeneratorLibrary.Instance.get(template_id);
        if (generator == null) return;

        var para = new Dictionary<string, string>();

        ParameterGetters.GetLanguageParameterGetter(generator.parameter_getter)(__instance, para);

        __instance.data.name = generator.GenerateName(para);
    }
}