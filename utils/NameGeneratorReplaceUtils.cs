using System.Collections.Generic;

namespace Chinese_Name.utils;

internal static class NameGeneratorReplaceUtils
{
    private static readonly Dictionary<string, NameGeneratorAsset> storage = new();

    public static void ReplaceNameGeneratorEmpty(string pID)
    {
        NameGeneratorAsset asset = new()
        {
            id = pID,
            use_dictionary = false,
            templates = new List<string>
            {
                "space"
            },
            vowels = new[] { "" }
        };
        if (!storage.ContainsKey(pID)) storage.Add(pID, AssetManager.nameGenerator.get(pID) ?? asset);
        AssetManager.nameGenerator.Replace(asset);
    }

    public static void RestoreNameGenerators()
    {
        foreach (var pair in storage) AssetManager.nameGenerator.Replace(pair.Value);
    }

    private static void Replace<T>(this AssetLibrary<T> library, T pAsset) where T : Asset
    {
        var id = pAsset.id;
        if (library.dict.ContainsKey(id))
        {
            for (var index = 0; index < library.list.Count; ++index)
            {
                if (library.list[index].id != id) continue;
                library.list.RemoveAt(index);
                break;
            }

            library.dict.Remove(id);
        }

        library.t = pAsset;
        library.t.create();
        library.t.setHash(BaseAssetLibrary._latest_hash++);
        library.list.Add(pAsset);
        library.dict.Add(id, pAsset);
    }
}