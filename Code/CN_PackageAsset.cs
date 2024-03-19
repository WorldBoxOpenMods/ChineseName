namespace Chinese_Name;

public class CN_PackageAsset : Asset
{
    public CN_NameGeneratorLibrary generators     = new();
    public string                  name           = "default";
    public WordLibraryManager      word_libraries = new();
}