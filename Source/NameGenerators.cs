using Chinese_Name.Abstract;
using strings;

namespace Chinese_Name;

public class NameGenerators : ExtendLibrary<NameGeneratorAsset, NameGenerators>
{
    [GetOnly, AssetId(S_NameTemplate.human_unit)]
    public static NameGeneratorAsset HumanUnit { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets();
        
        HumanUnit.onomastics_templates.Clear();
        HumanUnit.addOnomastic("0Lo,1L,1L|0:百家姓;1:千字文");
        HumanUnit.addOnomastic("0L,_1fL,_2Lo|0:西方名字;1:西方名字中缀;2:西方姓氏");
    }
}