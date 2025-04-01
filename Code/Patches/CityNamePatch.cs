using System.Collections.Generic;
using Chinese_Name.utils;
using HarmonyLib;
using NeoModLoader.api.attributes;
using NeoModLoader.General.Event.Handlers;
using NeoModLoader.General.Event.Listeners;

namespace Chinese_Name;

public class CityNamePatch : IPatch
{
    public void Initialize()
    {
        new Harmony(nameof(set_city_name)).Patch(AccessTools.Method(typeof(City), nameof(City.generateName)),
            postfix: new HarmonyMethod(typeof(CityNamePatch), nameof(set_city_name)));
    }
    [Hotfixable]
    private static void set_city_name(City __instance, Actor pActor)
    {
        var template_id = pActor.GetNameTemplate(MetaType.City);
        template_id = "human_city";
        var generator = CN_NameGeneratorLibrary.Instance.get(template_id);
        if (generator == null) return;

        var para = new Dictionary<string, string>();

        ParameterGetters.GetCityParameterGetter(generator.parameter_getter)(__instance, para);

        __instance.data.name = generator.GenerateName(para);
    }
}