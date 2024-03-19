using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Chinese_Name.utils;

internal class GeneralUtils
{
    private static readonly JsonSerializerSettings private_members_visit_settings = new()
    {
        ContractResolver = new DefaultContractResolver
        {
            DefaultMembersSearchFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
        }
    };

    public static List<T> DeserializeAllFromResource<T>(string pPath)
    {
        TextAsset[] text_assets = LoadAllFrom(pPath);
        return text_assets.Select(text_asset => JsonConvert.DeserializeObject<T>(text_asset.text))
                          .Where(asset => asset != null).ToList();
    }

    public static TextAsset[] LoadAllFrom(string pPath)
    {
        if (!Directory.Exists(pPath))
        {
            return Array.Empty<TextAsset>();
        }

        List<TextAsset> text_assets = new List<TextAsset>();
        foreach (var file_path in Directory.GetFiles(pPath, "*", SearchOption.AllDirectories))
        {
            var text_asset = new TextAsset(File.ReadAllText(file_path));
            text_asset.name = Path.GetFileNameWithoutExtension(file_path);
            text_assets.Add(text_asset);
        }

        return text_assets.ToArray();
    }

    public static T DeserializeFromJson<T>(string pRawPackageStr)
    {
        return JsonConvert.DeserializeObject<T>(pRawPackageStr, private_members_visit_settings);
    }

    public static string SerializeToJson(CN_PackageAsset pCurrentPackage)
    {
        return JsonConvert.SerializeObject(pCurrentPackage, private_members_visit_settings);
    }
}