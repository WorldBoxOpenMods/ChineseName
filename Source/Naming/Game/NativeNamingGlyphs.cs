using System;
using System.Collections.Generic;
using NeoModLoader.General;
using System.Linq;
using NeoModLoader.services;

namespace Chinese_Name.Naming.Game;

internal static class NativeNamingGlyphs
{
    internal const string KeepSurname = "chinesename_keep_surname";
    internal const char KeepSurnameCode = '\uE117';
    internal const string RuleSeparator = "chinesename_rule_separator";
    internal const char RuleSeparatorCode = '\uE116';
    internal const string AsPool = "chinesename_as_pool";
    internal const char AsPoolCode = '\uE118';
    private static IReadOnlyDictionary<string, CultureNameGlyph> _inputs;

    internal sealed class Glyph
    {
        internal readonly MetaType Target;
        internal readonly string Part;
        internal readonly string Label;
        internal readonly string Icon;
        internal readonly string Description;
        internal readonly char Code;
        internal string Id => "chinesename_" + (Target == MetaType.Unit ? "unit_" : "city_") + Part;

        internal Glyph(MetaType target, string part, string label, string icon, string description, char code)
        {
            Target = target;
            Part = part;
            Label = label;
            Icon = icon;
            Description = description;
            Code = code;
        }
    }

    // 这些短码属于存档和剪贴板格式。已有字形的短码不能改动。
    private static readonly Glyph[] Glyphs =
    {
        new(MetaType.Unit, "surname", "姓氏", "iconFamilyList", "优先保留继承的姓氏，新取的姓氏按当前语言和文化设置生成。", '\uE110'),
        new(MetaType.Unit, "first", "给名首部", "actor_traits/iconAttractive", "从当前语言和文化设置取给名的第一部分，也可以用作单字名或完整的给名。", '\uE111'),
        new(MetaType.Unit, "given", "给名", "iconBooks", "从当前语言和文化设置取给名。和给名首部一起使用时，可以组成双字名。", '\uE112'),
        new(MetaType.Unit, "middle", "中间名", "iconClanList", "从当前语言和文化设置取中间名。", '\uE113'),
        new(MetaType.City, "name", "地名", "iconCityList", "从当前语言和文化设置取地名。和地名后缀一起使用时，提供地名主体。", '\uE114'),
        new(MetaType.City, "suffix", "地名后缀", "iconKingdomList", "按文化规则加上地名后缀，例如府。", '\uE115')
    };

    internal static Glyph Find(string id) => Glyphs.FirstOrDefault(glyph => glyph.Id == id);

    internal static CultureNameGlyph FindInput(string id) =>
        id != null && _inputs?.TryGetValue(id, out var input) == true ? input : null;

    internal static CultureNameGlyph ForInput(string type, Part part) =>
        part.Input == null && part.PoolInput == null ? null : _inputs?.Values.FirstOrDefault(glyph => glyph.Type == type &&
            glyph.Field == (part.Input ?? part.PoolInput) && glyph.PoolPrefix == (part.PoolInput == null ? null : part.Pool));

    internal static bool AppliesTo(string id, string type)
    {
        if (FindInput(id) is { } input) return input.Type == type;
        if (Find(id) is { } legacy) return legacy.Target == NativeOnomastics.Meta(type);
        return id switch
        {
            KeepSurname => type == VanillaNames.Person,
            AsPool => type != null,
            _ => true
        };
    }

    internal static void Register()
    {
        var library = AssetManager.onomastics_library;
        foreach (var glyph in Glyphs)
            RegisterOutput(glyph.Id, glyph.Code, "ui/Icons/" + glyph.Icon, glyph.Label, glyph.Description,
                (card, data, builder, _, index, _) => NativeOnomastics.DrawGlyph(card.id, data, index, builder.Length));
        if (library.getByShortId(KeepSurnameCode.ToString()) == null && !library.dict.ContainsKey(KeepSurname))
        {
            var asset = library.add(new OnomasticsAsset
            {
                id = KeepSurname,
                short_id = KeepSurnameCode,
                type = OnomasticsAssetType.Special,
                path_icon = "ui/Icons/iconFamilyList",
                affects_left = true,
                affects_left_group_only = true
            });
            library.list_special.Remove(asset);
            LM.AddToCurrentLocale(asset.getLocaleID(), "沿用已有姓氏");
            LM.AddToCurrentLocale(asset.getIDSubname(), "");
            LM.AddToCurrentLocale(asset.getDescriptionID(), "放在姓氏词组或其「作为词库」字形右边。有姓时沿用已有姓氏。");
            LM.AddToCurrentLocale(asset.getDescriptionID2(), "");
        }
        if (library.getByShortId(RuleSeparatorCode.ToString()) == null && !library.dict.ContainsKey(RuleSeparator))
        {
            var separator = library.add(new OnomasticsAsset
            {
                id = RuleSeparator,
                short_id = RuleSeparatorCode,
                type = OnomasticsAssetType.Special,
                color_text = "#D2DAC0",
                path_icon = library.get("space").path_icon,
                is_word_divider = true,
                namemaker_delegate = (_, data, _, _, _, _) => NativeOnomastics.DrawRuleSeparator(data)
            });
            library.list_special.Remove(separator);
            LM.AddToCurrentLocale(separator.getLocaleID(), "命名分隔符");
            LM.AddToCurrentLocale(separator.getIDSubname(), "");
            LM.AddToCurrentLocale(separator.getDescriptionID(), "按命名规则连接两部分，例如中文译名用间隔号，原文用空格。");
            LM.AddToCurrentLocale(separator.getDescriptionID2(), "");
        }
        if (library.getByShortId(AsPoolCode.ToString()) == null && !library.dict.ContainsKey(AsPool))
        {
            var asset = library.add(new OnomasticsAsset
            {
                id = AsPool,
                short_id = AsPoolCode,
                type = OnomasticsAssetType.Special,
                path_icon = "ui/Icons/iconBooks",
                affects_left = true,
                affects_left_group_only = true
            });
            library.list_special.Remove(asset);
            LM.AddToCurrentLocale(asset.getLocaleID(), "作为词库");
            LM.AddToCurrentLocale(asset.getIDSubname(), "");
            LM.AddToCurrentLocale(asset.getDescriptionID(), "紧挨左侧词组。用空格分隔词库名，例如「江河地名 山岭地名 沿海地名 边塞地名」。先抽词库，再按当前语言取词。");
            LM.AddToCurrentLocale(asset.getDescriptionID2(), "");
        }
        var service = NamingApi.Current;
        service.AttachGlyphCatalog(AddContext, glyph =>
            library.getByShortId(glyph.ShortCode.ToString()) == null && !library.dict.ContainsKey(glyph.Id));
        _inputs = service.CultureGlyphs;
    }

    private static void AddContext(CultureNameGlyph input)
    {
        if (!RegisterOutput(input.Id, input.ShortCode, input.Icon, input.Label, input.Description,
            (_, data, _, _, index, _) => NativeOnomastics.DrawInputGlyph(input, data, index)))
            throw new InvalidOperationException("Duplicate native glyph ID or short code: " + input.Id);
    }

    private static bool RegisterOutput(string id, char code, string icon, string label, string description,
        OnomasticsNameMakerDelegate draw)
    {
        var library = AssetManager.onomastics_library;
        if (library.getByShortId(code.ToString()) != null || library.dict.ContainsKey(id))
        {
            LogService.LogWarning("ChineseName cannot register glyph " + id + ". Its ID or short code is already in use.");
            return false;
        }
        var asset = library.add(new OnomasticsAsset
        {
            id = id, short_id = code, type = OnomasticsAssetType.Special,
            path_icon = icon, color_text = "#F4D287", namemaker_delegate = draw
        });
        // 扩展字形只由玩家放入，不参与原版的随机排列。
        library.list_special.Remove(asset);
        LM.AddToCurrentLocale(asset.getLocaleID(), label);
        LM.AddToCurrentLocale(asset.getIDSubname(), "");
        LM.AddToCurrentLocale(asset.getDescriptionID(), description);
        LM.AddToCurrentLocale(asset.getDescriptionID2(), "");
        return true;
    }
}
