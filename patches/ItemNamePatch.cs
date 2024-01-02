using System.Collections.Generic;
using HarmonyLib;
namespace Chinese_Name;

public class ItemNamePatch : IPatch
{
    public void Initialize()
    {
        new Harmony(nameof(set_item_name)).Patch(
            AccessTools.Method(typeof(ItemGenerator), nameof(ItemGenerator.checkModName)),
            prefix: new HarmonyMethod(AccessTools.Method(GetType(), nameof(set_item_name))));
    }

    private static bool set_item_name(ref ItemData pItemData, ItemAsset pModAsset, ItemAsset pItemAsset,
        ActorBase pActor)
    {
        if (!string.IsNullOrWhiteSpace(pItemData.name)) return false;
        if (pModAsset.quality != ItemQuality.Legendary) return false;

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
                if (no_found > 3) return true;

                continue;
            }

            ParameterGetters.GetItemParameterGetter(generator.parameter_getter)(pItemData, pItemAsset, pActor.a, para);
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

        pItemData.name = name;

        return false;
    }
}