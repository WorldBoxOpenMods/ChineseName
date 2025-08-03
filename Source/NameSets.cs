using Chinese_Name.Abstract;
using strings;

namespace Chinese_Name;
[InitAfter(typeof(NameGenerators))]
public class NameSets : ExtendLibrary<NameSetAsset, NameSets>
{
    public static NameSetAsset DefaultEasternNameSet { get; private set; }
    public static NameSetAsset DefaultWesternNameSet { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets();

        DefaultEasternNameSet.unit = NameGenerators.EasternUnit.id;
        DefaultEasternNameSet.city = NameGenerators.EasternCity.id;
        DefaultEasternNameSet.kingdom = NameGenerators.EasternKingdom.id;
        DefaultEasternNameSet.culture = NameGenerators.EasternCulture.id;
        DefaultEasternNameSet.family = NameGenerators.EasternFamily.id;
        DefaultEasternNameSet.clan = NameGenerators.EasternClan.id;
        DefaultEasternNameSet.language = NameGenerators.EasternLanguage.id;
        DefaultEasternNameSet.religion = NameGenerators.EasternReligion.id;
    }
}