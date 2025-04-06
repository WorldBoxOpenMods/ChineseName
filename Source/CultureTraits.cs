using System.Linq;
using Chinese_Name.Abstract;

namespace Chinese_Name;
[InitAfter(typeof(CultureTraitGroups))]
public class CultureTraits : ExtendTraitLibrary<CultureTrait, CultureTraits>
{
    public static CultureTrait FamilyNameFromFather { get; private set; }
    public static CultureTrait FamilyNameFromMother { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets();

        FamilyNameFromFather.group_id = CultureTraitGroups.FamilyName.id;
        FamilyNameFromMother.group_id = CultureTraitGroups.FamilyName.id;
        
        OppositeEach(FamilyNameFromFather, FamilyNameFromMother);
    }

    private void OppositeEach(params CultureTrait[] traits)
    {
        foreach (var trait in traits)
        {
            trait.opposite_list = traits.Select(x => x.id).ToList();
        }
    }
}