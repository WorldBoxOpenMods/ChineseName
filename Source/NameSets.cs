using Chinese_Name.Abstract;
using strings;

namespace Chinese_Name;

public class NameSets : ExtendLibrary<NameSetAsset, NameSets>
{
    [GetOnly, AssetId(S_NameSet.human_default_set)]
    public static NameSetAsset HumanDefaultSet { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets();
    }
}