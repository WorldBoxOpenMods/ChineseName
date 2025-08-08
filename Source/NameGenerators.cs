using Chinese_Name.Abstract;
using strings;

namespace Chinese_Name;

public class NameGenerators : ExtendLibrary<NameGeneratorAsset, NameGenerators>
{
    public static NameGeneratorAsset EasternUnit { get; private set; }
    public static NameGeneratorAsset EasternCity { get; private set; }
    public static NameGeneratorAsset EasternKingdom { get; private set; }
    public static NameGeneratorAsset EasternCulture { get; private set; }
    public static NameGeneratorAsset EasternClan { get; private set; }
    public static NameGeneratorAsset EasternFamily { get; private set; }
    public static NameGeneratorAsset EasternLanguage { get; private set; }
    public static NameGeneratorAsset EasternReligion { get; private set; }
    
    public static NameGeneratorAsset WesternUnit { get; private set; }
    public static NameGeneratorAsset WesternCity { get; private set; }
    public static NameGeneratorAsset WesternKingdom { get; private set; }
    public static NameGeneratorAsset WesternCulture { get; private set; }
    public static NameGeneratorAsset WesternClan { get; private set; }
    public static NameGeneratorAsset WesternFamily { get; private set; }
    public static NameGeneratorAsset WesternLanguage { get; private set; }
    public static NameGeneratorAsset WesternReligion { get; private set; }
    
    protected override void OnInit()
    {
        RegisterAssets();
        
        EasternUnit.addOnomastic("0Lo,1L,2L|0:百家姓;1:千字文;2:千字文");
        EasternCity.addOnomastic("0L|0:真实城名");
        EasternKingdom.addOnomastic("0L|0:真实国名");
        EasternCulture.addOnomastic("d1|1:文化");
        EasternClan.addOnomastic("de0|0:家");
        EasternFamily.addOnomastic("de0|0:家");
        EasternLanguage.addOnomastic("0L,1|0:千字文;1:语");
        EasternReligion.addOnomastic("0L,1|0:千字文;1:教");
        
        WesternUnit.addOnomastic("0L,_1fL,_2Lo|0:西方名字;1:西方名字中缀;2:西方姓氏");
        WesternCity.addOnomastic("e0|0:城");
        WesternKingdom.addOnomastic("e|");
        WesternCulture.addOnomastic("0L,1|0:西方名字;1:文化");
        WesternClan.addOnomastic("e0|0:家族");
        WesternFamily.addOnomastic("e0|0:家族");
        WesternLanguage.addOnomastic("0L,1|0:西方名字;1:语");
        WesternReligion.addOnomastic("0L,1|0:西方名字;1:教");
        //HumanUnit.addOnomastic("0L,_1fL,_2Lo|0:西方名字;1:西方名字中缀;2:西方姓氏");
    }
}