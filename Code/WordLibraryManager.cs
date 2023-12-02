using System.Collections.Generic;
using System.IO;
using System.Linq;
using Chinese_Name.utils;
using UnityEngine;

namespace Chinese_Name;

public class WordLibraryManager : AssetLibrary<WordLibraryAsset>
{
    internal static readonly WordLibraryManager Instance = new();
    private static HashSet<string> submitted_dir = new HashSet<string>();
    public override void init()
    {
        base.init();
        id = "WordLibraryManager";
        SubmitDirectoryToLoad(Path.Combine(ModClass.Instance.GetDeclaration().FolderPath, "word_libraries/default"));
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
    internal static string GetRandomWord(string pId)
    {
        if (Instance.dict.TryGetValue(pId, out WordLibraryAsset asset) && asset.words.Count > 0)
        {
            return Instance.dict[pId].words.GetRandom();
        }
        return "";
    }
    public static void SubmitDirectoryToLoad(string pDirectory)
    {
        if (submitted_dir.Contains(pDirectory)) return;
        TextAsset[] text_assets = GeneralUtils.LoadAllFrom(pDirectory);
        foreach (TextAsset text_asset in text_assets)
        {
            Instance.add(new WordLibraryAsset(text_asset.name, text_asset.text.Replace("\r", "").Split('\n').ToList()));
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