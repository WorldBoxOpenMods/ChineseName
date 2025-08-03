using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Chinese_Name.Abstract;
using HarmonyLib;
using NeoModLoader.utils;

namespace Chinese_Name.Patches;

internal class PatchForApplyingOnomastics : IPatch
{
    [HarmonyTranspiler, HarmonyPatch(typeof(NameGenerator), nameof(NameGenerator.generateName))]
    private static IEnumerable<CodeInstruction> NameGenerator_generateName(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();

        int index = 0;
        do
        {
            index = list.FindIndex(index,
                x => x.opcode == OpCodes.Callvirt &&
                     (x.operand as MethodInfo)?.Name == nameof(OnomasticsData.generateName));
            if (index == -1) break;
            list[index] = new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(PatchOnomasticsData), nameof(PatchOnomasticsData.GenerateNameExtended)));
            list.Insert(index, new CodeInstruction(OpCodes.Ldarg_0));
        } while (true);
        return list;
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(NameGenerator), nameof(NameGenerator.generateNameFromOnomastics))]
    private static IEnumerable<CodeInstruction> NameGenerator_generateNameFromOnomastics(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();

        int index = 0;
        do
        {
            index = list.FindIndex(index,
                x => x.opcode == OpCodes.Callvirt &&
                     (x.operand as MethodInfo)?.Name == nameof(OnomasticsData.generateName));
            if (index == -1) break;
            list[index] = new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(PatchOnomasticsData), nameof(PatchOnomasticsData.GenerateNameExtended)));
            list.Insert(index, new CodeInstruction(OpCodes.Ldarg_2));
        } while (true);
        return list;
    }
}