using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Chinese_Name.utils;

internal static class GeneralUtils
{
    public static string GetNameTemplate(this Actor actor, MetaType type)
    {
        string tNameTemplate = null;
        if (actor.hasCulture())
        {
            tNameTemplate = actor.culture.getNameTemplate(type);
        }
        else
        {
            foreach (Actor t_parent in actor.getParents())
            {
                if (t_parent.hasCulture())
                {
                    tNameTemplate = t_parent.culture.getNameTemplate(type);
                    break;
                }
            }
        }
        if (string.IsNullOrEmpty(tNameTemplate))
        {
            tNameTemplate = actor.asset.getNameTemplate(type);
        }

        return tNameTemplate;
    }
    public static List<T> DeserializeAllFromResource<T>(string pPath)
    {
        TextAsset[] text_assets = LoadAllFrom(pPath);
        return text_assets.Select(text_asset => Newtonsoft.Json.JsonConvert.DeserializeObject<T>(text_asset.text)).Where(asset => asset != null).ToList();
    }

    public static TextAsset[] LoadAllFrom(string pPath)
    {
        if (!Directory.Exists(pPath))
        {
            return Array.Empty<TextAsset>();
        }
        List<TextAsset> text_assets = new List<TextAsset>();
        foreach(var file_path in Directory.GetFiles(pPath, "*", SearchOption.AllDirectories))
        {
            var text_asset = new TextAsset(File.ReadAllText(file_path));
            text_asset.name = Path.GetFileNameWithoutExtension(file_path);
            text_assets.Add(text_asset);
        }
        return text_assets.ToArray();
    }
}