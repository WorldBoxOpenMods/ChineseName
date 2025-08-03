using Chinese_Name.Abstract;
using strings;

namespace Chinese_Name;

[InitAfter(typeof(NameSets))]
public class ActorAssets : ExtendLibrary<ActorAsset, ActorAssets>
{
    [GetOnly, AssetId(SA.human)]
    public static ActorAsset Human { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets();
        Human.name_template_sets = new[] { NameSets.DefaultEasternNameSet.id };
    }
}