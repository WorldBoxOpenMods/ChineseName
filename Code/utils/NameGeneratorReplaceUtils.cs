using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace Chinese_Name.utils;

internal static class NameGeneratorReplaceUtils
{ 
    private static readonly HashSet<string> replaced = new();
    private static readonly HashSet<string> newed = new();
    public static IEnumerable<CodeInstruction> replace_name_generate_transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        var label = new Label();

        codes[0].WithLabels(label);

        codes.InsertRange(0, new List<CodeInstruction>()
        {
            new(OpCodes.Ldarg_0),
            new(OpCodes.Call, AccessTools.Method(typeof(NameGeneratorReplaceUtils), nameof(IsReplaced))),
            new(OpCodes.Brfalse_S, label),
            new(OpCodes.Ldstr, " "),
            new(OpCodes.Ret)
        });
        return codes;
    }
    public static void ReplaceNameGeneratorEmpty(string pID)
    {
        if (!replaced.Add(pID)) return;
        if (AssetManager.nameGenerator.dict.ContainsKey(pID))
        {
            return;
        }
        NameGeneratorAsset asset = new()
        {
            id = pID,
            use_dictionary = false,
            templates = new List<string>
            {
                "space"
            },
            vowels = new[] { "" }
        };
        AssetManager.nameGenerator.add(asset);
        newed.Add(pID);
    }

    public static void RestoreNameGenerators()
    {
        foreach (string id in newed)
        {
            AssetManager.nameGenerator.list.Remove(AssetManager.nameGenerator.dict[id]);
            AssetManager.nameGenerator.dict.Remove(id);
        }
        replaced.Clear();
        newed.Clear();
    }
    public static bool IsReplaced(string pID)
    {
        return replaced.Contains(pID);
    }
    /*
    private static void Replace<T>(this AssetLibrary<T> library, T pAsset) where T : Asset
    {
        var id = pAsset.id;
        if (library.dict.ContainsKey(id))
        {
            for (var index = 0; index < library.list.Count; ++index)
            {
                if (library.list[index].id != id) continue;
                library.list.RemoveAt(index);
                break;
            }

            library.dict.Remove(id);
        }

        library.t = pAsset;
        library.t.create();
        library.t.setHash(BaseAssetLibrary._latest_hash++);
        library.list.Add(pAsset);
        library.dict.Add(id, pAsset);
    }*/
}