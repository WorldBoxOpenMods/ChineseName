using UnityEngine;

namespace Chinese_Name;

public class WordLibraryManager : AssetLibrary<WordLibraryAsset>
{
    internal static readonly WordLibraryManager Instance = new();

    public override void init()
    {
        base.init();
        id = "WordLibraryManager";
        SubmitDirectoryToLoad("chinese_name/word_libraries/default");
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
        TextAsset[] text_assets = Resources.LoadAll<TextAsset>(pDirectory);
        foreach (TextAsset text_asset in text_assets)
        {
            Instance.add(new WordLibraryAsset(text_asset.name, text_asset.text.Replace("\r", "").Split('\n').ToList()));
        }
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