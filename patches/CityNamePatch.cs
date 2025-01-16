using System.Collections.Generic;
using HarmonyLib;
using NeoModLoader.api.attributes;
using NeoModLoader.General.Event.Handlers;
using NeoModLoader.General.Event.Listeners;

namespace Chinese_Name;

public class CityNamePatch : IPatch
{
    public void Initialize()
    {
        new Harmony(nameof(set_city_name)).Patch(AccessTools.Method(typeof(WorldLog), nameof(WorldLog.logNewCity)),
            prefix: new HarmonyMethod(typeof(CityNamePatch), nameof(set_city_name)));
    }
    [Hotfixable]
    private static void set_city_name(City pCity)
    {
        if (!string.IsNullOrWhiteSpace(pCity.data.name)) return;
        var generator = CN_NameGeneratorLibrary.Instance.get(pCity.race.name_template_city);
        if (generator == null) return;

        var para = new Dictionary<string, string>();

        ParameterGetters.GetCityParameterGetter(generator.parameter_getter)(pCity, para);

        pCity.data.name = generator.GenerateName(para);
    }
}