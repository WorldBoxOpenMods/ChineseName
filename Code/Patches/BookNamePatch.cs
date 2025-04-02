using System.Collections.Generic;
using Chinese_Name.utils;
using HarmonyLib;

namespace Chinese_Name;

public class BookNamePatch : IPatch
{
    public void Initialize()
    {
        new Harmony(nameof(set_book_name)).Patch(AccessTools.Method(typeof(Book), nameof(Book.newBook)),
            postfix: new HarmonyMethod(GetType(), nameof(set_book_name)));
    }
    private static void set_book_name(Book __instance, BookTypeAsset pBookType)
    {
        var template_id = pBookType.name_template;
        var generator = CN_NameGeneratorLibrary.Instance.get(template_id);
        if (generator == null) return;

        var para = new Dictionary<string, string>();

        ParameterGetters.GetBookParameterGetter(generator.parameter_getter)(__instance, para);

        __instance.data.name = generator.GenerateName(para);
    }
}