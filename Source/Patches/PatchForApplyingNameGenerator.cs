using Chinese_Name.Abstract;
using HarmonyLib;

namespace Chinese_Name.Patches;

internal class PatchForApplyingNameGenerator : IPatch
{
    [HarmonyPrefix, HarmonyPatch(typeof(NameGenerator), nameof(NameGenerator.generateNameFromTemplate), [typeof(NameGeneratorAsset),  typeof(Actor), typeof(Kingdom), typeof(bool), typeof(int), typeof(string), typeof(string[]), typeof(bool), typeof(long?), typeof(ActorSex), typeof(bool)])]
    private static bool generateNameFromTemplate(ref string __result, NameGeneratorAsset pAsset, Actor pActor = null,
        Kingdom pKingdom = null, bool pForceLegacy = false, int pCalls = 0, string pOnomasticsTemplate = null,
        string[] pClassicTemplate = null, bool pTestReplacer = false, long? pSeed = null, ActorSex pSex = ActorSex.None,
        bool pIgnoreBlacklist = false)
    {
        if (pAsset.hasOnomastics() && !pForceLegacy)
        {
            __result = NameGenerator.generateNameFromOnomastics(pAsset, pOnomasticsTemplate, pActor, pSeed, pSex);
            return false;
        }

        var name_generator = ChineseNameGeneratorLibrary.Instance.get(pAsset.id);
        if (name_generator == null)
        {
            return true;
        }

        var parameters = DictionaryPool<string, string>.Get();
        name_generator.ObtainParameters(pActor, pKingdom, parameters);
        __result = name_generator.GenerateName(null);
        DictionaryPool<string, string>.Release(parameters);
        return false;
    }
}