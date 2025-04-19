using System.IO;
using System.Linq;
using System.Net;
using NeoModLoader.api;

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

    public void LoadFromMod(IMod mod)
    {
        var folder = Path.Combine(mod.GetDeclaration().FolderPath, "ChineseNamePackages");
        if (!Directory.Exists(folder)) return;
        foreach (var path in Directory.GetFiles(folder, "*.txt", SearchOption.AllDirectories))
        {
            LoadFromFile(path, Path.GetFileNameWithoutExtension(path));
        }
    }
}