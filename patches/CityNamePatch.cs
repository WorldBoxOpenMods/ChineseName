using System.Collections.Generic;
using HarmonyLib;
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
    private static void set_city_name(City __instance)
    {
        if (!string.IsNullOrWhiteSpace(__instance.data.name)) return;
        var generator = CN_NameGeneratorLibrary.Instance.get("city_name");
        if (generator == null) return;

        var para = new Dictionary<string, string>();

        ParameterGetters.GetCityParameterGetter(generator.parameter_getter)(__instance, para);

        __instance.data.name = generator.GenerateName(para);
    }
}