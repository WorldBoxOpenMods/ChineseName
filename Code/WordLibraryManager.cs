using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Chinese_Name.utils;
using UnityEngine;

namespace Chinese_Name;

public class WordLibraryManager : AssetLibrary<WordLibraryAsset>
{
    private static  HashSet<string>    submitted_dir = new HashSet<string>();
    internal static WordLibraryManager Instance;

    public override void init()
    {
        base.init();
        id = "WordLibraryManager";
        //SubmitDirectoryToLoad(GeneralUtils.CombinePath(ModClass.Instance.GetDeclaration().FolderPath, "word_libraries/default"));
    }
    internal void MergeWithFolder(string folder_path)
    {
        TextAsset[] text_assets = GeneralUtils.LoadAllFrom(folder_path, x => x.EndsWith(".txt"));
        foreach (TextAsset text_asset in text_assets)
        {
            SubmitForPatch(text_asset.name, text_asset.text.Replace("\r", "").Split('\n').ToList());
        }
    }
    internal void UnloadAll()
    {
        dict.Clear();
        list.Clear();
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

    public override WordLibraryAsset get(string pID)
    {
        return dict.TryGetValue(pID, out WordLibraryAsset asset)
            ? asset
            : null;
    }

    /// <summary>
    /// 从指定的词库中随机获取一个词
    /// </summary>
    /// <param name="pId">指定词库的id, 为对应文件文件名去除后缀</param>
    /// <returns>指定词库中随机一个词, 如果词库不存在则返回空串</returns>
    public static string GetRandomWord(string pId)
    {
        WordLibraryAsset asset = Instance.get(pId);
        if (asset != null && asset.words.Count > 0)
        {
            return asset.GetRandom();
        }

        return "";
    }

    public static void SubmitDirectoryToLoad(string pDirectory)
    {
        if (submitted_dir.Contains(pDirectory)) return;
        TextAsset[] text_assets = GeneralUtils.LoadAllFrom(pDirectory);
        foreach (TextAsset text_asset in text_assets)
        {
            Submit(text_asset.name, text_asset.text.Replace("\r", "").Split('\n').ToList());
        }

        submitted_dir.Add(pDirectory);
    }

    public static void Submit(string pId, List<string> pWords)
    {
        Instance.add(new WordLibraryAsset(pId, pWords));
    }

    public static void SubmitForPatch(string pId, List<string> pWords)
    {
        if (Instance.dict.ContainsKey(pId))
        {
            Instance.dict[pId].words.AddRange(pWords);
        }
        else
        {
            Instance.add(new WordLibraryAsset(pId, pWords));
        }
    }
}