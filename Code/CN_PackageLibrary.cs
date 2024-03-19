using Chinese_Name.utils;

namespace Chinese_Name;

public class CN_PackageLibrary : AssetLibrary<CN_PackageAsset>
{
    internal static CN_PackageLibrary Instance = new();
    internal static CN_PackageAsset   CurrentPackage;
    internal static CN_PackageAsset   DefaultPackage;
    internal        bool              dirty;

    public override void init()
    {
        base.init();
        CurrentPackage = get("default");
        DefaultPackage = CurrentPackage;
        t.name = "默认";
        t.word_libraries.init();
        t.generators.init();
    }

    public override CN_PackageAsset add(CN_PackageAsset pAsset)
    {
        dirty = true;
        return base.add(pAsset);
    }

    public override CN_PackageAsset get(string pID)
    {
        if (dict.TryGetValue(pID, out CN_PackageAsset asset)) return asset;
        add(new CN_PackageAsset
        {
            id = pID
        });
        return t;
    }

    internal void SwitchPackage(string pID)
    {
        CurrentPackage = get(pID);
        NameGeneratorReplaceUtils.RestoreNameGenerators();
        foreach (CN_NameGeneratorAsset generator in CurrentPackage.generators.list)
            NameGeneratorReplaceUtils.ReplaceNameGeneratorEmpty(generator.id);
        if (CurrentPackage == DefaultPackage) return;
        foreach (CN_NameGeneratorAsset generator in DefaultPackage.generators.list)
            NameGeneratorReplaceUtils.ReplaceNameGeneratorEmpty(generator.id);
    }
}