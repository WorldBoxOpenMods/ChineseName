using Chinese_Name.Abstract;
using strings;

namespace Chinese_Name;

[InitAfter(typeof(NameSets))]
public class ActorAssets : ExtendLibrary<ActorAsset, ActorAssets>
{
    [GetOnly, AssetId(SA.human)]
    public static ActorAsset Human { get; private set; }
    [GetOnly, AssetId(SA.orc)]
    public static ActorAsset Orc { get; private set; }
    [GetOnly, AssetId(SA.dwarf)]
    public static ActorAsset Dwarf { get; private set; }
    [GetOnly, AssetId(SA.elf)]
    public static ActorAsset Elf { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets();
        Human.name_template_sets = new[] { NameSets.DefaultEasternNameSet.id };
        Orc.name_template_sets = new[] { NameSets.DefaultWesternNameSet.id };
        Dwarf.name_template_sets = new[] { NameSets.DefaultWesternNameSet.id };
        Elf.name_template_sets = new[] { NameSets.DefaultWesternNameSet.id };
    }
}
