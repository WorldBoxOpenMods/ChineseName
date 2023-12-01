using System;
using System.Collections.Generic;
using Chinese_Name.utils;

namespace Chinese_Name;

public class CN_NameGeneratorLibrary : AssetLibrary<CN_NameGeneratorAsset>  
{
    internal static CN_NameGeneratorLibrary Instance = new();
    public override void init()
    {
        base.init();
        id = "CN_NameGeneratorLibrary";
        SubmitDirectoryToLoad("chinese_name/name_generators/default");
    }

    public override CN_NameGeneratorAsset get(string pID)
    {
        if (string.IsNullOrEmpty(pID)) return null;
        return dict.TryGetValue(pID, out CN_NameGeneratorAsset asset) ? asset : null;
    }

    public static void SubmitDirectoryToLoad(string pDirectory)
    {
        List<List<CN_NameGeneratorAsset>> name_generator_assets =
            GeneralUtils.DeserializeAllFromResource<List<CN_NameGeneratorAsset>>(pDirectory);
        List<CN_NameGeneratorAsset> name_generator_assets_flatten = new List<CN_NameGeneratorAsset>();
        foreach (List<CN_NameGeneratorAsset> name_generator_asset in name_generator_assets)
        {
            name_generator_assets_flatten.AddRange(name_generator_asset);
        }
        foreach (CN_NameGeneratorAsset asset in name_generator_assets_flatten)
        {
            Submit(asset);
        }
    }
    public static void Submit(CN_NameGeneratorAsset pAsset)
    {
        for(int i = 0; i < pAsset.templates.Count; i++)
        {
            var name_template = pAsset.templates[i];
            try
            {
                name_template.Parse();
            }
            catch (Exception e)
            {
                ModClass.LogWarning($"Failed to parse name template '{name_template.raw_format}' in {pAsset.id}");
                ModClass.LogWarning(e.Message);
                ModClass.LogInfo($"Just skip it now.");
                    
                pAsset.templates.RemoveAt(i);
                i--;
            }
        }
            
        if(pAsset.templates.Count == 0)
        {
            ModClass.LogWarning($"No valid name template in {pAsset.id}");
            ModClass.LogInfo($"Just skip it now.");

            return;
        }
        Instance.add(pAsset);
        // TODO: Patch to vanilla name generator
    }
}