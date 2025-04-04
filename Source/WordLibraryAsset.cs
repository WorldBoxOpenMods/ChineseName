using System.Collections.Generic;

namespace Chinese_Name;

public class WordLibraryAsset : Asset
{
    public List<string> Words = new();

    public string GetRandom()
    {
        return Words.GetRandom();
    }
}