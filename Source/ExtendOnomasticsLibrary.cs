using Chinese_Name.Abstract;
using Chinese_Name.Utils;

namespace Chinese_Name;

public class ExtendOnomasticsLibrary : ExtendLibrary<OnomasticsAsset, ExtendOnomasticsLibrary>
{
    [AssetId("word_library")]
    public static OnomasticsAsset WordLibrary { get; private set; }
    [GetOnly, AssetId(S_Onomastics.clone_last)]
    public static OnomasticsAsset CloneLast { get; private set; }
    [GetOnly, AssetId(S_Onomastics.mirror)]
    public static OnomasticsAsset Mirror { get; private set; }
    [GetOnly, AssetId(S_Onomastics.wild_6)]
    public static OnomasticsAsset Wild6 { get; private set; }
    [GetOnly, AssetId(S_Onomastics.domino)]
    public static OnomasticsAsset Domino { get; private set; }
    [GetOnly, AssetId(S_Onomastics.repeater)]
    public static OnomasticsAsset Repeater { get; private set; }
    [GetOnly, AssetId(S_Onomastics.upper)]
    public static OnomasticsAsset Upper { get; private set; }
    [GetOnly, AssetId(S_Onomastics.backspace)]
    public static OnomasticsAsset Backspace { get; private set; }
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
    }
}