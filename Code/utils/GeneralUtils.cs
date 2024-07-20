using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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

    public static List<T> DeserializeAllFromFolder<T>(string pPath, Func<string, bool> filter = null)
    {
        string[] text_assets = LoadAllRawFrom(pPath, filter==null ? (x=>x.EndsWith(".json")) : (x=>filter(x) && x.EndsWith(".json")));
        List<T> result = new List<T>();
        foreach (var item in text_assets)
        {
            try
            {
                result.Add(JsonConvert.DeserializeObject<T>(item));
            }
            catch(Exception e)
            {
                ModClass.LogError($"Error when deserialize text {item}");
                continue;
            }
        }
        return result;
    }

    public static string[] LoadAllRawFrom(string pPath, Func<string, bool> filter = null)
    {
        if (!Directory.Exists(pPath))
        {
            return Array.Empty<string>();
        }

        List<string> text_assets = new List<string>();
        foreach (var file_path in Directory.GetFiles(pPath, "*", SearchOption.AllDirectories))
        {
            if (filter != null && !filter(file_path))
            {
                continue;
            }
            text_assets.Add(File.ReadAllText(file_path));
        }

        return text_assets.ToArray();
    }
    public static TextAsset[] LoadAllFrom(string pPath, Func<string, bool> filter = null)
    {
        if (!Directory.Exists(pPath))
        {
            return Array.Empty<TextAsset>();
        }

        List<TextAsset> text_assets = new List<TextAsset>();
        foreach (var file_path in Directory.GetFiles(pPath, "*", SearchOption.AllDirectories))
        {
            if (filter!=null && !filter(file_path))
            {
                continue;
            }
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
    public static string CombinePath(params string[] paths)
    {
        return Path.Combine(paths).Replace("\\", "/");
    }
    public static string GetDirectoryName(string path)
    {
        return Path.GetDirectoryName(path).Replace("\\", "/");
    }
}