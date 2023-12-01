using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Chinese_Name.utils;

internal class GeneralUtils
{
    public static List<T> DeserializeAllFromResource<T>(string pPath)
    {
        TextAsset[] text_assets = Resources.LoadAll<TextAsset>(pPath);
        List<T> assets = new List<T>();
        foreach (TextAsset text_asset in text_assets)
        {
            ModClass.LogInfo($"To parse the following into {typeof(T).Name}:\n {text_asset.text}");
            T asset = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(text_asset.text);
            if (asset != null)
            {
                assets.Add(asset);
            }
        }
        return assets;
    }
}