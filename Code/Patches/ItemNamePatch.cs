using System.Collections.Generic;
using HarmonyLib;
using NeoModLoader.api.attributes;

namespace Chinese_Name;

public class ItemNamePatch : IPatch
{
    public void Initialize()
    {/*
        new Harmony(nameof(set_item_name)).Patch(
                                                 AccessTools.Method(typeof(ItemGenerator),
                                                                    nameof(ItemGenerator.generateItem)),
                                                 postfix: new
                                                     HarmonyMethod(AccessTools.Method(GetType(),
                                                                       nameof(set_item_name))));*/
    }
/*
    [Hotfixable]
    private static void set_item_name(ref ItemData __result, ItemAsset pItemAsset,
                                      ActorBase    pActor)
    {
        if (__result == null) return;
        if (!string.IsNullOrWhiteSpace(__result.name)) return;
        var max_quality = ItemQuality.Normal;
        foreach (var mod in __result.modifiers)
        {
            var mod_asset = AssetManager.items_modifiers.get(mod);
            if (mod_asset.quality > max_quality) max_quality = mod_asset.quality;
        }

        if (max_quality < ItemQuality.Legendary) return;

        string name = null;
        int num = 0;
        int no_found = 0;

        var para = new Dictionary<string, string>();
        while (string.IsNullOrWhiteSpace(name) || ItemGenerator.unique_legendary_names.Contains(name))
        {
            string random_name_template = pItemAsset.getRandomNameTemplate(pActor);
            var generator = CN_NameGeneratorLibrary.Instance.get(random_name_template);

            if (generator == null)
            {
                no_found++;
                if (no_found > 3) return;

                continue;
            }

            ParameterGetters.GetItemParameterGetter(generator.parameter_getter)(__result, pItemAsset, pActor.a, para);
            generator.ClearTemplateGetter();
            var template = generator.GetTemplate(para);
            name = template.GenerateName(para);

            if (++num > 10)
            {
                ItemGenerator.unique_legendary_names.Clear();
            }

            if (num > 12)
            {
                name = generator.default_template.GenerateName(para);
                break;
            }
        }

        __result.name = name;
        if (num <= 10) ItemGenerator.unique_legendary_names.Add(name);
    }*/
}