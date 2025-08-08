using System;
using System.Collections.Generic;
using System.Linq;
using Chinese_Name.Abstract;
using Chinese_Name.Utils;
using strings;

namespace Chinese_Name;

public class ExtendOnomasticsLibrary : ExtendLibrary<OnomasticsAsset, ExtendOnomasticsLibrary>
{
    /// <summary>
    /// 读词库
    /// </summary>
    [AssetId("word_library")]
    public static OnomasticsAsset WordLibrary { get; private set; }
    /// <summary>
    /// 复制组至今的结果
    /// </summary>
    [GetOnly, AssetId(S_Onomastics.clone_last)]
    public static OnomasticsAsset CloneLast { get; private set; }
    /// <summary>
    /// 翻转组至今的结果
    /// </summary>
    [GetOnly, AssetId(S_Onomastics.mirror)]
    public static OnomasticsAsset Mirror { get; private set; }
    /// <summary>
    /// 添加空格
    /// </summary>
    [GetOnly, AssetId(S_Onomastics.space)]
    public static OnomasticsAsset Space { get; private set; }
    [GetOnly, AssetId(S_Onomastics.wild_6)]
    public static OnomasticsAsset Wild6 { get; private set; }
    [GetOnly, AssetId(S_Onomastics.domino)]
    public static OnomasticsAsset Domino { get; private set; }
    /// <summary>
    /// 复制上一字形结果
    /// </summary>
    [GetOnly, AssetId(S_Onomastics.repeater)]
    public static OnomasticsAsset Repeater { get; private set; }
    [GetOnly, AssetId(S_Onomastics.upper)]
    public static OnomasticsAsset Upper { get; private set; }
    /// <summary>
    /// 删除一个字
    /// </summary>
    [GetOnly, AssetId(S_Onomastics.backspace)]
    public static OnomasticsAsset Backspace { get; private set; }
    /// <summary>
    /// 删除当前组的第一个字(用于切片)
    /// </summary>
    [GetOnly, AssetId(S_Onomastics.consonant_separator)]
    public static OnomasticsAsset RemoveFirst { get; private set; }
    /// <summary>
    /// 删除当前组的最后一个字(用于切片)
    /// </summary>
    [GetOnly, AssetId(S_Onomastics.vowel_separator)]
    public static OnomasticsAsset RemoveLast { get; private set; }
    [GetOnly, AssetId(S_Onomastics.vowel_replacer)]
    public static OnomasticsAsset NamerFamilyName { get; private set; }
    [GetOnly, AssetId(S_Onomastics.consonant_duplicator)]
    public static OnomasticsAsset KingdomName { get; private set; }
    [GetOnly, AssetId(S_Onomastics.vowel_duplicator)]
    public static OnomasticsAsset CityName { get; private set; }
    [GetOnly, AssetId(S_Onomastics.consonant_replacer)]
    public static OnomasticsAsset ViewFirst { get; private set; }
    [GetOnly, AssetId(S_Onomastics.consonant_requirer)]
    public static OnomasticsAsset ViewLast { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets();

        for (int i = 1; i <= 10; i++)
        {
            Get($"group_{i}").Get<ExtendOnomasticsAsset>().ChineseNameMakerDelegate =
                (asset, data, localBuilder, globalBuilder, part, index, sex, parameters, namer) =>
                    data.getRandomPartFromGroup(asset.id);
        }

        WordLibrary.type = OnomasticsAssetType.Special;
        WordLibrary.short_id = 'L';
        WordLibrary.path_icon = "ui/icons/iconBooks";
        WordLibrary.affects_left_word = true;
        WordLibrary.Get<ExtendOnomasticsAsset>().ChineseNameMakerDelegate = (asset, data, localBuilder, globalBuilder,
            lastPart,
            index, sex, parameters, namer) =>
        {
            var word_library = WordLibraryLibrary.Instance.get(localBuilder.ToString());
            if (word_library == null)
            {
                return string.Empty;
            }

            localBuilder.Clear();

            var result = word_library.GetRandom();
            return result;
        };

        CloneLast.Get<ExtendOnomasticsAsset>().ChineseNameMakerDelegate = (asset, data, localBuilder, globalBuilder,
            part, index, sex, parameters, namer) => localBuilder.ToString();

        Mirror.Get<ExtendOnomasticsAsset>().ChineseNameMakerDelegate = (asset, data, localBuilder, globalBuilder,
            part, index, sex, parameters, namer) =>
        {
            if (localBuilder.Length == 0) return string.Empty;
            var res = localBuilder.ToString().Reverse();
            localBuilder.Clear();
            return res;
        };
        Domino.Get<ExtendOnomasticsAsset>().ChineseNameMakerDelegate = (asset, data, localBuilder, globalBuilder,
            lastPart, index, sex, parameters, namer) =>
        {
            if (namer == null) return string.Empty;
            namer.data.get(S_DataKey.FamilyName, out string family_name);
            if (string.IsNullOrEmpty(family_name))
            {
                namer.data.set(S_DataKey.FamilyName, localBuilder.ToString());
                //ModClass.LogInfo($"Set family name {localBuilder} for {namer.data.id}");
                return string.Empty;
            }

            localBuilder.Clear();
            return family_name;
        };
        Domino.affects_left = true;
        Domino.affects_left_word = true;
        Domino.affects_left_group_only = true;
        NamerFamilyName.Get<ExtendOnomasticsAsset>().ChineseNameMakerDelegate = (asset, data, localBuilder, globalBuilder,
            lastPart, index, sex, parameters, namer) =>
        {
            if (namer == null) return string.Empty;
            namer.data.get(S_DataKey.FamilyName, out string family_name);
            return family_name;
        };
        NamerFamilyName.affects_everything = false;
        NamerFamilyName.affects_left = false;
        NamerFamilyName.affects_left_group_only = false;
        NamerFamilyName.affects_left_word = false;
        NamerFamilyName.is_divider = false;
        NamerFamilyName.is_immune = false;
        NamerFamilyName.is_upper = false;
        NamerFamilyName.is_word_divider = false;
        KingdomName.Get<ExtendOnomasticsAsset>().ChineseNameMakerDelegate = (asset, data, localBuilder, globalBuilder,
            lastPart, index, sex, parameters, namer) =>
        {
            if (namer?.kingdom == null) return string.Empty;
            return namer.kingdom.name;
        };
        KingdomName.affects_everything = false;
        KingdomName.affects_left = false;
        KingdomName.affects_left_group_only = false;
        KingdomName.affects_left_word = false;
        KingdomName.is_divider = false;
        KingdomName.is_immune = false;
        KingdomName.is_upper = false;
        KingdomName.is_word_divider = false;
        CityName.Get<ExtendOnomasticsAsset>().ChineseNameMakerDelegate = (asset, data, localBuilder, globalBuilder,
            lastPart, index, sex, parameters, namer) =>
        {
            if (namer?.city == null) return string.Empty;
            return namer.city.name;
        };
        CityName.affects_everything = false;
        CityName.affects_left = false;
        CityName.affects_left_group_only = false;
        CityName.affects_left_word = false;
        CityName.is_divider = false;
        CityName.is_immune = false;
        CityName.is_upper = false;
        CityName.is_word_divider = false;
        Repeater.Get<ExtendOnomasticsAsset>().ChineseNameMakerDelegate = (asset, data, localBuilder, globalBuilder,
            last_part, index, sex, parameters, namer) => last_part;
        Backspace.is_word_divider = true;
        Backspace.Get<ExtendOnomasticsAsset>().ChineseNameMakerDelegate = (asset, data, localBuilder, globalBuilder,
            part, index, sex, parameters, namer) =>
        {
            if (globalBuilder.Length > 0)
            {
                globalBuilder.Remove(globalBuilder.Length - 1, 1);
            }

            return string.Empty;
        };
        RemoveFirst.check_delegate = null;
        RemoveFirst.Get<ExtendOnomasticsAsset>().ChineseNameMakerDelegate = (asset, data, localBuilder, globalBuilder,
            lastPart, index, sex, parameters, namer) =>
        {
            if (localBuilder.Length > 0)
            {
                localBuilder.Remove(0, 1);
            }

            return string.Empty;
        };
        RemoveLast.check_delegate = null;
        RemoveLast.Get<ExtendOnomasticsAsset>().ChineseNameMakerDelegate = (asset, data, localBuilder, globalBuilder,
            lastPart, index, sex, parameters, namer) =>
        {
            if (localBuilder.Length > 0)
            {
                localBuilder.Remove(localBuilder.Length - 1, 1);
            }

            return string.Empty;
        };
        
        Space.Get<ExtendOnomasticsAsset>().ChineseNameMakerDelegate = (asset, data, localBuilder, globalBuilder,
            lastPart, index, sex, parameters, namer) =>
        {
            if (localBuilder.Length == 0)
            {
                if (globalBuilder.Length == 0)
                {
                    return " ";
                }

                if (globalBuilder[globalBuilder.Length - 1] == ' ')
                {
                    return String.Empty;
                }

                return " ";
            }

            if (localBuilder[localBuilder.Length - 1] == ' ') return string.Empty;

            return " ";
        };
        Wild6.Get<ExtendOnomasticsAsset>().ChineseNameMakerDelegate = (asset, data, localBuilder, globalBuilder,
            lastPart, index, sex, parameters, namer) =>
        {
            string text;
            using ListPool<string> groups = new ListPool<string>(data.groups.Count);
            foreach (KeyValuePair<string, OnomasticsDataGroup> pair in data.groups)
            {
                if (!pair.Value.isEmpty() && AssetManager.onomastics_library.get(pair.Key).group_id < 6)
                {
                    groups.Add(pair.Key);
                }
            }
            if (!groups.Any())
            {
                text = string.Empty;
            }
            else
            {
                text =  AssetManager.onomastics_library.get(OnomasticsLibrary.GetRandom<string>(groups)).Get<ExtendOnomasticsAsset>().ChineseNameMakerDelegate.Invoke(asset, data,
                    localBuilder, globalBuilder,
                    lastPart, index, sex, parameters, namer) ?? string.Empty;
            }

            return text;
        };
        ViewFirst.Get<ExtendOnomasticsAsset>().ChineseNameMakerDelegate = (asset, data, localBuilder, globalBuilder,
            lastPart, index, sex, parameters, namer) =>
        {
            return localBuilder.Length > 0 ? localBuilder[0].ToString() : string.Empty;
        };
        ViewLast.Get<ExtendOnomasticsAsset>().ChineseNameMakerDelegate = (asset, data, localBuilder, globalBuilder,
            lastPart, index, sex, parameters, namer) =>
        {
            return localBuilder.Length > 0 ? localBuilder[localBuilder.Length - 1].ToString() : string.Empty;
        };
    }

    public override void PostInit(OnomasticsAsset asset)
    {
        base.PostInit(asset);
        AssetManager.onomastics_library._dict_short_id[asset.short_id.ToString()] = asset;
    }
}