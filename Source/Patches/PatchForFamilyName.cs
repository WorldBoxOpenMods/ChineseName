using Chinese_Name.Abstract;
using HarmonyLib;

namespace Chinese_Name.Patches;

internal class PatchForFamilyName : IPatch
{
    [HarmonyPostfix, HarmonyPatch(typeof(BabyMaker), nameof(BabyMaker.makeBaby))]
    private static void BabyMaker_makeBaby_postfix(Actor pParent1, Actor pParent2, Actor __result)
    {
        if (pParent2 == null)
        {
            pParent1.data.get(S_DataKey.FamilyName, out string family_name);
            if (!string.IsNullOrEmpty(family_name))
            {
                __result.data.set(S_DataKey.FamilyName, family_name);
                return;
            }
        }
        else
        {
            var father = pParent1.isSexMale() ? pParent1 : pParent2;
            var mother = pParent2.isSexFemale() ? pParent2 : pParent1;

            father.data.get(S_DataKey.FamilyName, out string father_family_name);
            mother.data.get(S_DataKey.FamilyName, out string mother_family_name);

            if (string.IsNullOrEmpty(father_family_name) && !string.IsNullOrEmpty(mother_family_name))
            {
                __result.data.set(S_DataKey.FamilyName, mother_family_name);
                return;
            }
            if (string.IsNullOrEmpty(mother_family_name) && !string.IsNullOrEmpty(father_family_name))
            {
                __result.data.set(S_DataKey.FamilyName, father_family_name);
                return;
            }

            var father_culture = father.culture;
            var mother_culture = mother.culture;

            bool father_patrilineal = father_culture?.hasTrait(nameof(CultureTraits.FamilyNameFromFather)) ?? false;
            bool mother_matrilineal = mother_culture?.hasTrait(nameof(CultureTraits.FamilyNameFromMother)) ?? false;

            Actor family_name_source;

            if (father_patrilineal && mother_matrilineal)
            {
                family_name_source = father.renown >= mother.renown ? father : mother;
            }
            else if (mother_matrilineal)
            {
                family_name_source = mother;
            }
            else
            {
                family_name_source = __result.culture == father_culture ? father : mother;
            }

            bool use_father_name = family_name_source == father;
            string selected_family_name = use_father_name ? father_family_name : mother_family_name;
            
            (use_father_name ? father : mother).data.get(S_DataKey.NameSet, out string name_set);
            __result.data.set(S_DataKey.FamilyName, selected_family_name);
            __result.data.set(S_DataKey.NameSet, name_set);
        }
    }
}