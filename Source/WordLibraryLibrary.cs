using System.IO;
using System.Linq;
using System.Net;

namespace Chinese_Name;

public class WordLibraryLibrary : AssetLibrary<WordLibraryAsset>
{
    public static WordLibraryLibrary Instance { get; } = new WordLibraryLibrary();

    public void LoadFromFile(string path, string asset_id)
    {
        var texts = File.ReadAllLines(path);
        var asset = new WordLibraryAsset()
        {
            id = asset_id
        };
        asset.Words.AddRange(texts.Where(x => !string.IsNullOrEmpty(x)));
        add(asset);
    }
}