using System;
using System.Collections.Generic;
using System.IO;
using NeoModLoader.api;
using Newtonsoft.Json;

namespace Chinese_Name;

public class ChineseNameGeneratorLibrary : AssetLibrary<ChineseNameGeneratorAsset>
{
    public static ChineseNameGeneratorLibrary Instance = new();
    public void LoadFromMod(IMod mod)
    {
        foreach (var path in Directory.GetFiles(Path.Combine(mod.GetDeclaration().FolderPath, "ChineseNamePackages"),
                     "*.json"))
        {
            try
            {
                LoadFromFile(path);
            }
            catch (Exception e)
            {
                ModClass.LogAllException(e);
            }
        }
    }

    public void LoadFromFile(string path)
    {
        var asset_list = JsonConvert.DeserializeObject<List<ChineseNameGeneratorAsset>>(File.ReadAllText(path));
        foreach (var asset in asset_list)
        {
            asset.default_template?.ReParse();
            for (int i = 0; i < asset.templates.Count; i++)
            {
                try
                {
                    asset.templates[i].ParseNT();
                }
                catch (Exception e)
                {
                    ModClass.LogAllException(e);
                    asset.templates.RemoveAt(i);
                    i--;
                }
            }

            if (asset.templates.Count == 0)
            {
                ModClass.LogWarning($"No valid template found in {path}/{asset.id}");
                return;
            }

            if (has(asset.id))
            {
                get(asset.id).templates.AddRange(asset.templates);
            }
            else
            {
                add(asset);
            }
        }
    }
}