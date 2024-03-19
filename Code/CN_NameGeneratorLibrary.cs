using System;
using System.Collections.Generic;
using System.IO;
using Chinese_Name.utils;

namespace Chinese_Name;

public class CN_NameGeneratorLibrary : AssetLibrary<CN_NameGeneratorAsset>
{
    private static  HashSet<string>         submitted_dir = new HashSet<string>();
    internal static CN_NameGeneratorLibrary Instance => CN_PackageLibrary.CurrentPackage.generators;

    public override void init()
    {
        base.init();
        id = "default";
        SubmitDirectoryToLoad(Path.Combine(ModClass.Instance.GetDeclaration().FolderPath, "name_generators/default"));
    }

    public static CN_NameGeneratorAsset Get(string pId)
    {
        return Instance.get(pId);
    }

    internal void Reload()
    {
        HashSet<string> reload_dir = new HashSet<string>(submitted_dir);
        submitted_dir.Clear();
        foreach (var dir in reload_dir)
        {
            SubmitDirectoryToLoad(dir);
        }
    }

    public override CN_NameGeneratorAsset get(string pID)
    {
        if (string.IsNullOrEmpty(pID)) return null;
        return dict.TryGetValue(pID, out CN_NameGeneratorAsset asset)
            ? asset
            : CN_PackageLibrary.DefaultPackage.generators.get(pID);
    }

    public static void SubmitDirectoryToLoad(string pDirectory, string pTargetPackage = "default")
    {
        if (submitted_dir.Contains(pDirectory)) return;
        List<List<CN_NameGeneratorAsset>> name_generator_assets =
            GeneralUtils.DeserializeAllFromResource<List<CN_NameGeneratorAsset>>(pDirectory);
        List<CN_NameGeneratorAsset> name_generator_assets_flatten = new List<CN_NameGeneratorAsset>();
        foreach (List<CN_NameGeneratorAsset> name_generator_asset in name_generator_assets)
        {
            name_generator_assets_flatten.AddRange(name_generator_asset);
        }

        foreach (CN_NameGeneratorAsset asset in name_generator_assets_flatten)
        {
            asset.default_template?.ReParse();
            Submit(asset, pTargetPackage);
        }

        submitted_dir.Add(pDirectory);
    }

    public static void Submit(CN_NameGeneratorAsset pAsset, string pTargetPackage = "default")
    {
        for (int i = 0; i < pAsset.templates.Count; i++)
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

        if (pAsset.templates.Count == 0)
        {
            ModClass.LogWarning($"No valid name template in {pAsset.id}");
            ModClass.LogInfo($"Just skip it now.");

            return;
        }

        CN_PackageLibrary.Instance.get(pTargetPackage).generators.add(pAsset);

        if (pTargetPackage == Instance.id)
        {
            NameGeneratorReplaceUtils.ReplaceNameGeneratorEmpty(pAsset.id);
        }
    }
}