namespace Chinese_Name;

public class WordLibraryAsset : Asset
{
    public readonly List<string> words;
    internal WordLibraryAsset(string id, List<string> words)
    {
        this.id = id;
        this.words = words;
        this.words ??= new List<string>();
    }
}