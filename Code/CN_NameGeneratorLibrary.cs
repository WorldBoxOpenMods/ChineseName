using System;
using System.Collections.Generic;
using System.IO;
using Chinese_Name.utils;
using UnityEngine.Assertions;

namespace Chinese_Name;

public class CN_NameGeneratorLibrary : AssetLibrary<CN_NameGeneratorAsset>
{
    private static  HashSet<string>         submitted_dir = new HashSet<string>();
    internal static CN_NameGeneratorLibrary Instance;

    public override void init()
    {
        base.init();
        id = "CN_NameGenerator";
        //SubmitDirectoryToLoad(GeneralUtils.CombinePath(ModClass.Instance.GetDeclaration().FolderPath, "name_generators/default"));
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
    internal void LoadNonRepeatFolder(string folder_path, Func<string, bool> filter = null)
    {
        List<List<CN_NameGeneratorAsset>> name_generator_assets =
            GeneralUtils.DeserializeAllFromFolder<List<CN_NameGeneratorAsset>>(folder_path, filter);
        List<CN_NameGeneratorAsset> name_generator_assets_flatten = new List<CN_NameGeneratorAsset>();
        foreach (List<CN_NameGeneratorAsset> name_generator_asset in name_generator_assets)
        {
            name_generator_assets_flatten.AddRange(name_generator_asset);
        }

        foreach(var asset in name_generator_assets_flatten)
        {
            bool self_check = asset.SelfCheck();
            if (!self_check)
            {
                continue;
            }

            NameGeneratorReplaceUtils.ReplaceNameGeneratorEmpty(asset.id);
            if (get(asset.id) == null) add(asset); 
            else get(asset.id).MergeWith(asset);
        }
    }
    internal void UnLoadAll()
    {
        NameGeneratorReplaceUtils.RestoreNameGenerators();
        dict.Clear();
        list.Clear();
    }
    public override CN_NameGeneratorAsset get(string pID)
    {
        if (string.IsNullOrEmpty(pID)) return null;
        return dict.TryGetValue(pID, out CN_NameGeneratorAsset asset)
            ? asset
            : null;
    }

    public static void SubmitDirectoryToLoad(string pDirectory)
    {
        if (submitted_dir.Contains(pDirectory)) return;
        List<List<CN_NameGeneratorAsset>> name_generator_assets =
            GeneralUtils.DeserializeAllFromFolder<List<CN_NameGeneratorAsset>>(pDirectory);
        List<CN_NameGeneratorAsset> name_generator_assets_flatten = new List<CN_NameGeneratorAsset>();
        foreach (List<CN_NameGeneratorAsset> name_generator_asset in name_generator_assets)
        {
            name_generator_assets_flatten.AddRange(name_generator_asset);
        }

        foreach (CN_NameGeneratorAsset asset in name_generator_assets_flatten)
        {
            asset.default_template?.ReParse();
            Submit(asset);
        }

        submitted_dir.Add(pDirectory);
    }

    public static void Submit(CN_NameGeneratorAsset pAsset)
    {
        Instance.add(pAsset);
        if (pAsset.SelfCheck())
            NameGeneratorReplaceUtils.ReplaceNameGeneratorEmpty(pAsset.id);
    }
}