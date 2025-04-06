using Chinese_Name.Abstract;

namespace Chinese_Name;

public class CultureTraitGroups : ExtendLibrary<CultureTraitGroupAsset, CultureTraitGroups>
{
    public static CultureTraitGroupAsset FamilyName { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets();
        
        FamilyName.name = "trait_group_family_name";
        FamilyName.color = "#E75340";
    }
}