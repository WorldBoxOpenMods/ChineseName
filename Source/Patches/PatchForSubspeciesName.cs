using Chinese_Name.Abstract;
using HarmonyLib;

namespace Chinese_Name.Patches;

internal class PatchForSubspeciesName : IPatch
{
    private const string GeneratorId = "default_subspecies";

    [HarmonyPrefix, HarmonyPatch(typeof(Subspecies), nameof(Subspecies.generateName))]
    private static bool generateName_prefix(Subspecies __instance, ActorAsset pAsset, WorldTile pTile)
    {
        var nameGenerator = ChineseNameGeneratorLibrary.Instance.get(GeneratorId);
        if (nameGenerator == null)
        {
            return true;
        }

        try
        {
            var context = NameGenerationContextScope.Current?.Fork() ?? new NameGenerationContext();
            context.Source = "subspecies";
            context.ChineseAsset = nameGenerator;
            context.Subspecies = __instance;
            context.ActorAsset = pAsset;
            context.Tile = pTile;
            context.BiomeAsset = pTile?.Type != null && pTile.Type.is_biome ? pTile.Type.biome_asset : null;

            using (NameGenerationContextScope.Push(context))
            {
                string firstCandidate = null;
                for (int i = 0; i < 24; i++)
                {
                    var parameters = new NameParameterBag(context);
                    parameters.Set("attempt", (i + 1).ToString());
                    var candidate = nameGenerator.GenerateName(parameters);
                    if (string.IsNullOrEmpty(candidate))
                    {
                        continue;
                    }

                    firstCandidate ??= candidate;
                    if (!HasNameInWorld(__instance, candidate))
                    {
                        __instance.setName(candidate);
                        return false;
                    }
                }

                if (!string.IsNullOrEmpty(firstCandidate))
                {
                    foreach (var suffix in DuplicateSuffixes.LoopRandom())
                    {
                        var duplicateCandidate = firstCandidate + suffix;
                        if (!HasNameInWorld(__instance, duplicateCandidate))
                        {
                            __instance.setName(duplicateCandidate);
                            return false;
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            ModClass.LogAllException(e);
        }

        return true;
    }

    private static bool HasNameInWorld(Subspecies subspecies, string name)
    {
        using StringBuilderPool builder = new StringBuilderPool();
        builder.Append(name);
        return subspecies.hasNameInWorld(builder);
    }

    private static readonly string[] DuplicateSuffixes = { "别支", "新支", "旁支", "远支" };
}
