using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Chinese_Name.Abstract;
using Chinese_Name.Utils;
using HarmonyLib;
using UnityEngine.Pool;

namespace Chinese_Name.Patches;

internal class PatchForApplyingNameGenerator : IPatch
{
    private static string GetNameExtended(string pAssetID, ActorSex pSex = ActorSex.Male, bool pForceLegacy = false,
        string pTemplate = null, long? pSeed = null, bool pIgnoreBlackList = false, Actor pActor = null)
    {
        NameGenerator.init();
        if (pActor != null)
        {
            
        }
        NameGeneratorAsset tAsset = AssetManager.name_generator.get(pAssetID);
        if (tAsset == null)
        {
            ModClass.LogInfo("NameGeneratorAsset not found: " + pAssetID);
        }
        NameGenerator._current_consonants = 0;
        NameGenerator._current_vowels = 0;
        string tName = NameGenerator.generateNameFromTemplate(tAsset, pActor, pActor?.kingdom, pForceLegacy, 0, pTemplate, null, false, pSeed, pSex, pIgnoreBlackList);
        if (!tAsset.hasOnomastics() && pSex == ActorSex.Female)
        {
            string lastLetter = tName.Substring(tName.Length - 1, 1);
            bool tFound = false;
            string[] vowels = tAsset.vowels;
            for (int i = 0; i < vowels.Length; i++)
            {
                if (vowels[i].CompareTo(lastLetter) == 0)
                {
                    tFound = true;
                    break;
                }
            }
            if (!tFound)
            {
                tName += Randy.getRandom<string>(tAsset.vowels);
            }
        }
        return tName;
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(NameGenerator), nameof(NameGenerator.generateName))]
    private static IEnumerable<CodeInstruction> generateName_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();

        int index = 0;
        do
        {
            index = list.FindIndex(x =>
                x.opcode == OpCodes.Call && (x.operand as MethodInfo)?.Name == nameof(NameGenerator.getName));
            if (index == -1) break;
            list[index] = new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(PatchForApplyingNameGenerator), nameof(GetNameExtended)));
            list.Insert(index, new CodeInstruction(OpCodes.Ldarg_0));
        } while (true);

        return list;
    }
    [HarmonyPrefix, HarmonyPatch(typeof(NameGenerator), nameof(NameGenerator.generateNameFromTemplate), [typeof(NameGeneratorAsset),  typeof(Actor), typeof(Kingdom), typeof(bool), typeof(int), typeof(string), typeof(string[]), typeof(bool), typeof(long?), typeof(ActorSex), typeof(bool)])]
    private static bool generateNameFromTemplate(ref string __result, NameGeneratorAsset pAsset, Actor pActor = null,
        Kingdom pKingdom = null, bool pForceLegacy = false, int pCalls = 0, string pOnomasticsTemplate = null,
        string[] pClassicTemplate = null, bool pTestReplacer = false, long? pSeed = null, ActorSex pSex = ActorSex.None,
        bool pIgnoreBlacklist = false)
    {
        if (pAsset == null)
        {
            return true;
        }

        if (pAsset.hasOnomastics() && !pForceLegacy)
        {
            __result = NameGenerator.generateNameFromOnomastics(pAsset, pOnomasticsTemplate, pActor, pSeed, pSex);
            return string.IsNullOrEmpty(__result);
        }

        var name_generator = ChineseNameGeneratorLibrary.Instance.get(pAsset.id);
        if (name_generator == null)
        {
            return true;
        }

        var parameters = DictionaryPool<string, string>.Get();
        name_generator.ObtainParameters(pActor, pKingdom, parameters);
        __result = name_generator.GenerateName(parameters);
        name_generator.StoreParameters(pActor, pKingdom, parameters);
        DictionaryPool<string, string>.Release(parameters);
        return string.IsNullOrEmpty(__result);
    }
}
