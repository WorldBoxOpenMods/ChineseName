using Chinese_Name.Abstract;
using Chinese_Name.Utils;
using HarmonyLib;

namespace Chinese_Name.Patches;

internal class PatchOnomasticsData : IPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(OnomasticsData), nameof(OnomasticsData.generateName))]
    private static bool generateName_prefix(OnomasticsData __instance, ref string __result, ActorSex pSex, int pCalls,
        long? pSeed)
    {
        __result = GenerateNameExtended(__instance, pSex, pCalls, pSeed, null);
        return false;
    }
    internal static string GenerateNameExtended(OnomasticsData data, ActorSex sex, int call, long? seed, Actor namer)
    {
		if (call > 50)
		{
			return "无名";
		}
		if (sex == ActorSex.None)
		{
			sex = Randy.randomBool() ? ActorSex.Female : ActorSex.Male;
		}
		string last_part = string.Empty;
		if (seed != null)
		{
			Randy.resetSeed(seed.Value);
		}
		string text;
		using StringBuilderPool full_name_builder = new StringBuilderPool();
		using ListPool<string> t_sub_group = data.getSubgroup(data._template_data);
		data._current_subgroup.Clear();
		data._current_subgroup.AddRange(t_sub_group);
		int component_count = data._current_subgroup.Count;
		bool upper_exists = false;
		
		using var local_name_builder = new StringBuilderPool();
		DictionaryPool<string, string> parameters = new DictionaryPool<string, string>(); 
		for (int i = 0; i < component_count; i++)
		{
			string component_id = data._current_subgroup[i];
			OnomasticsAsset component_asset = AssetManager.onomastics_library.get(component_id);
			if (component_asset.is_word_divider)
			{
				full_name_builder.Append(local_name_builder);
				local_name_builder.Clear();
				last_part = string.Empty;
			}
			var extend_asset = component_asset.Get<ExtendOnomasticsAsset>();
			if (OnomasticsData.hasCheckOnTheRight(data, i) &&
			    !OnomasticsData.passesCheckOnTheRight(data, full_name_builder, last_part, i, sex)) continue;
			
			OnomasticsAssetType type = component_asset.type;
			if (type > OnomasticsAssetType.Special) continue;
			
			string part = extend_asset.ChineseNameMakerDelegate?.Invoke(component_asset, data, local_name_builder, full_name_builder, last_part, i, sex, parameters, namer);
			if (component_asset.is_upper)
			{
				upper_exists = true;
			}
			if (part is { Length: > 0 })
			{
				last_part = part;
				local_name_builder.Append(part);
			}
			if (component_asset.is_word_divider)
			{
				full_name_builder.Append(local_name_builder);
				local_name_builder.Clear();
				last_part = string.Empty;
			}
		}

		full_name_builder.Append(local_name_builder);
		full_name_builder.Remove(new char[] { ',' });
		full_name_builder.TrimEnd(new char[] { ' ', '-' });
		if (full_name_builder.Length > 30)
		{
			full_name_builder.Cut(0, 30);
		}
		if (full_name_builder.Length == 0)
		{
			text = GenerateNameExtended(data, sex, ++call, null, namer);
		}
		else if (Blacklist.checkBlackList(full_name_builder))
		{
			text = GenerateNameExtended(data, sex, ++call, null, namer);
		}
		else
		{
			if (upper_exists)
			{
				full_name_builder.ToUpperInvariant();
			}
			else
			{
				full_name_builder.ToTitleCase();
			}
			text = full_name_builder.ToString();
		}

		return text;
    }
}