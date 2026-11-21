using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace Chinese_Name.Naming.Game;

internal static class KingdomNames
{
    private const string FullName = "chinesename.kingdom-full-name.v1";
    private const string ShortName = "chinesename.kingdom-short-name.v1";
    private static readonly string[] Titles =
    {
        "人民共和国", "联合王国", "联邦共和国", "共和国", "大公国", "酋长国", "王国", "帝国", "公国",
        "侯国", "联邦", "邦联", "王庭", "晶庭", "菌庭", "星盟", "诸邦", "诸部", "部族", "巢群", "泽国",
        "潮邦", "火领", "领地", "国", "邦", "领", "域"
    };

    internal static string Short(Kingdom kingdom, string saved = null)
    {
        string name = kingdom?.name;
        if (string.IsNullOrWhiteSpace(name)) name = saved;
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (kingdom != null)
        {
            kingdom.data.get(FullName, out string full);
            kingdom.data.get(ShortName, out string shortName);
            if (name == full && !string.IsNullOrWhiteSpace(shortName)) return shortName;
        }
        return WithoutTitle(name);
    }

    private static string WithoutTitle(string name)
    {
        name = name.Trim();
        foreach (string suffix in Titles)
        {
            if (!name.EndsWith(suffix, StringComparison.Ordinal)) continue;
            return name.Length == suffix.Length ? name : name.Substring(0, name.Length - suffix.Length).TrimEnd();
        }
        return name;
    }

    internal static string Generate(Kingdom kingdom, Actor source, long seed, long? culture = null,
        long? language = null, Func<string, bool> accept = null)
    {
        var naming = Calculate(Request(kingdom, source, seed, culture, language), request => NamingApi.Current.Generate(request, accept));
        if (naming == null) return null;
        Save(kingdom, naming);
        return naming.Text;
    }

    internal sealed class Calculated(string text, string root)
    {
        internal string Text { get; } = text;
        internal string Root { get; } = root;
        internal string Short => Root ?? WithoutTitle(Text);
    }

    internal static NameRequest Request(Kingdom kingdom, Actor source, long seed, long? culture, long? language)
    {
        var inputs = new Dictionary<string, object>();
        if (kingdom.data.original_actor_asset is string species)
            inputs["species"] = inputs[VanillaNames.CultureSpecies] = species;
        return VanillaNames.Request(VanillaNames.Kingdom, source, unchecked((ulong)seed),
            culture: culture, language: language, inputs: inputs, subject: kingdom, tile: kingdom.capital?.getTile());
    }

    internal static Calculated Calculate(NameRequest request, Func<NameRequest, NameResult> generate)
    {
        var result = generate(request);
        if (!result.Success) return null;
        string shortName = result.Parts.TryGetValue("suffix", out string suffix) && !string.IsNullOrWhiteSpace(suffix) &&
            result.Parts.TryGetValue("name", out string root) && !string.IsNullOrWhiteSpace(root) && root != result.Text
            ? root : null;
        return new Calculated(result.Text, shortName);
    }

    internal static void Save(Kingdom kingdom, Calculated naming)
    {
        kingdom.data.set(FullName, naming.Root == null ? null : naming.Text);
        kingdom.data.set(ShortName, naming.Root);
    }

    [HarmonyPatch(typeof(Kingdom), nameof(Kingdom.newCivKingdom))]
    internal static class Founding
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) =>
            IndependentNames.ReplaceCall(codes, AccessTools.Method(typeof(Actor), nameof(Actor.generateName)),
                AccessTools.Method(typeof(KingdomNames), nameof(GenerateAtFounding)), 0);
    }

    private static string GenerateAtFounding(Actor actor, MetaType type, long seed, ActorSex sex, Kingdom kingdom) =>
        Generate(kingdom, actor, unchecked(seed + World.world.map_stats.life_dna)) ?? actor.generateName(type, seed, sex);
}
