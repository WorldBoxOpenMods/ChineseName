using System;
using System.Collections.Generic;
using System.Linq;

namespace Chinese_Name.Naming.Game;

internal static class VanillaNamingConditions
{
    private static readonly (string Id, string Label)[] Origins =
    {
        ("human", "人族"), ("elf", "精灵"), ("orc", "兽人"), ("dwarf", "矮人"), ("civ_cat", "灵猫"),
        ("civ_fox", "狐族"), ("civ_dog", "犬族"), ("civ_wolf", "狼族"), ("civ_rabbit", "兔族"),
        ("civ_bear", "熊族"), ("civ_frog", "蛙族"), ("civ_turtle", "龟族")
    };

    private static readonly (string Id, string Label)[] Biomes =
    {
        ("biome_grass", "草原"), ("biome_hill", "丘陵"), ("biome_birch", "桦林"), ("biome_maple", "枫林"),
        ("biome_jungle", "丛林"), ("biome_savanna", "稀树草原"), ("biome_desert", "沙漠"), ("biome_swamp", "沼泽"),
        ("biome_permafrost", "冻土"), ("biome_crystal", "水晶地"), ("biome_enchanted", "魔法森林"),
        ("biome_corrupted", "腐化之地"), ("biome_infernal", "地狱"), ("biome_celestial", "天界"),
        ("biome_mushroom", "蘑菇林"), ("biome_wasteland", "荒原"), ("biome_biomass", "生质地"),
        ("biome_candy", "糖果地"), ("biome_clover", "三叶草地"), ("biome_cybertile", "赛博地"),
        ("biome_flower", "花海"), ("biome_garlic", "蒜地"), ("biome_lemon", "柠檬地"),
        ("biome_paradox", "悖论地"), ("biome_pumpkin", "南瓜地"), ("biome_rocklands", "岩地"),
        ("biome_sand", "沙地"), ("biome_singularity", "奇点地"), ("biome_tumor", "肿瘤地")
    };

    internal static void Register(NamingService service)
    {
        var categories = VanillaNames.Types
            .ToDictionary(type => type.Id, type => new NamingCategory(type), StringComparer.Ordinal);
        RegisterTabs(categories);
        RegisterContexts(categories);
        foreach (string type in new[] { "worldbox:subspecies", "worldbox:world", "worldbox:player" })
            categories[type].WorldScoped = true;
        foreach (string type in new[] { VanillaNames.War, "worldbox:book", "worldbox:motto", "worldbox:item" })
            categories[type].ShareDefaultLayout = true;
        Choices("person", "sex", "性别", ("male", "男性"), ("female", "女性"));
        Choices("person", "kind", "生物类别", ("sapient", "智慧生物"), ("creature", "普通生物"), ("boat", "船只"));
        Choices("person", "species", "个体物种", Origins.Concat(new[]
        {
            ("cat", "猫"), ("wolf", "狼"), ("boat_fishing", "渔船"),
            ("boat_trading", "商船"), ("boat_transport", "运输船"), ("battle_boat", "战船")
        }).ToArray());
        Choices("city", "biome", "建城群系", Biomes);
        Choices("city", PlaceNames.Landform, "建城地貌", ("plain", "平原"), ("mountain", "山地"),
            ("basin", "盆地"), ("canyon", "峡谷"), ("river-valley", "河谷沿岸"), ("island", "岛屿"), ("coast", "海滨"),
            ("peninsula", "半岛"), ("archipelago", "群岛"));
        Presence("city", PlaceNames.SourceCity, "移民来源城", "有来源城", "无来源城");
        Presence("city", PlaceNames.SourceFoundingName, "移民来源城旧名", "有来源城旧名", "无来源城旧名");
        Presence("city", PlaceNames.Region, "所属地区", "有地区", "无地区");
        Presence("city", "origin", "所属国家", "有所属国家", "无所属国家");
        Presence("family", "founder-surname", "创始人姓氏", "创始人有姓氏", "创始人无姓氏");
        Presence("clan", "origin", "发源城市", "有发源城市", "无发源城市");
        Presence("culture", "origin", "源文化", "有源文化", "无源文化");
        Presence("language", "origin", "源语言", "有源语言", "无源语言");
        Presence("religion", "origin", "源宗教", "有源宗教", "无源宗教");
        Presence("alliance", "founder-short", "发起国简称", "有发起国简称", "无发起国简称");
        Presence("alliance", "partner-short", "缔约国简称", "有缔约国简称", "无缔约国简称");
        Presence("army", "station", "驻地城市", "有驻地", "无驻地");
        Choices("war", "kind", "战争类型", ("normal", "征服"), ("spite", "仇恨"), ("inspire", "独立"),
            ("rebellion", "叛乱"), ("whisper_of_war", "煽动"));
        Presence("war", "defender", "防守国", "有防守国", "无防守国");
        Presence("war", PlaceNames.RebellionCity, "起事城市", "有起事城市", "无起事城市");
        Presence("book", PlaceNames.AuthorCity, "作者所属城市", "有作者城市", "无作者城市");
        Presence("book", PlaceNames.AuthorFoundingName, "作者城市旧名", "有作者城市旧名", "无作者城市旧名");
        Choices("book", "topic", "书籍题材", ("history", "史书"), ("love-story", "故事"),
            ("bad-story", "讽刺故事"), ("fable", "寓言"), ("warfare-manual", "兵法"), ("economy-manual", "经济"),
            ("stewardship-manual", "治理"), ("diplomacy-manual", "外交"), ("math", "算学"), ("biology", "博物"));
        Choices("motto", "kind", "格言归属", ("kingdom", "国家"), ("clan", "氏族"), ("alliance", "联盟"));
        Choices("item", "stage", "命名阶段", ("legendary", "传奇命名"), ("reforge", "重铸"));
        Choices("item", "equipment", "装备种类", ("sword", "剑"), ("axe", "斧"), ("spear", "矛"),
            ("bow", "弓"), ("staff", "杖"), ("helmet", "头盔"), ("armor", "护甲"), ("boots", "靴"),
            ("ring", "指环"), ("amulet", "护符"), ("hammer", "锤"), ("firearm", "火器"));
        Choices("item", "material", "装备材质", ("wood", "木"), ("stone", "石"), ("iron", "铁"),
            ("steel", "钢"), ("silver", "银"), ("mythril", "秘银"), ("adamantine", "精金"),
            ("bone", "骨"), ("leather", "皮革"), ("copper", "铜"), ("bronze", "青铜"));
        Choices("plot", "plot", "谋划类型", ("new-war", "筹备战争"), ("rebellion", "发动叛乱"),
            ("alliance-create", "建立联盟"), ("alliance-join", "加入联盟"), ("alliance-destroy", "解散联盟"),
            ("new-book", "编书"), ("new-culture", "开创文化"), ("culture-divide", "文化分化"),
            ("new-language", "创立语言"), ("language-divergence", "语言分化"),
            ("new-religion", "建立宗教"), ("religion-schism", "宗教分裂"), ("clan-ascension", "氏族崛起"));

        foreach (var type in VanillaNames.Types.Where(type => !categories[type.Id].WorldScoped && type.Id != VanillaNames.War))
            categories[type.Id].Values(VanillaNames.CultureSpecies, "文化族源",
                Origins.ToDictionary(value => value.Id, value => value.Label, StringComparer.Ordinal));

        Choices("subspecies", "kind", "亚种起源", Biomes.Concat(new[] { ("skeleton", "骸骨化"), ("uplift", "开智") }).ToArray());
        Choices("subspecies", "species", "物种", Origins);
        var sizes = new[] { ("tiny", "微型"), ("small", "小型"), ("standard", "标准"), ("large", "大型"),
            ("huge", "巨型"), ("gigantic", "庞大"), ("titanic", "泰坦级"), ("iceberg", "冰山级") };
        Choices("world", "kind", "地图尺寸", sizes);
        Choices("player", "kind", "地图尺寸", sizes);

        RegisterGlyphs(categories);
        foreach (var category in categories.Values) service.RegisterCategory(category);

        void Choices(string type, string field, string title, params (string Id, string Label)[] values) =>
            categories["worldbox:" + type].Values(field, title,
                values.ToDictionary(value => value.Id, value => value.Label, StringComparer.Ordinal));

        void Presence(string type, string field, string title, string present, string absent) =>
            categories["worldbox:" + type].Presence(field, title, present, absent);
    }

    private static void RegisterTabs(IReadOnlyDictionary<string, NamingCategory> categories)
    {
        int order = 0;
        Add("culture", "iconCulture", "编辑本文化生成文化名称的规则。", label: "文化名称");
        Add("language", "iconLanguage", "编辑本文化生成语言名称的规则。", label: "语言名称");
        Add("religion", "iconReligion", "编辑本文化生成宗教名称的规则。", label: "宗教名称");
        Add("war", "iconWar", "由战争类型指定的国家取名", "normal");
        Add("alliance", "iconAlliance", "由发起国取名，可使用缔约双方的简称", "founding-pair");
        Add("army", "iconArmy", "由驻地城市取名");
        Add("book", "iconBooks", "由作者取名，预览示例为史书", "book_name_history");
        Add("plot", "iconDiplomacy", "由发起者取名，预览示例为语言分化", "language_divergence");
        Add("motto", "iconDiplomacy", "用于国家、氏族和联盟，预览示例为国家格言", "kingdom");
        Add("item", "iconItemType", "由制作者取名，用于传奇装备和重铸");

        void Add(string type, string icon, string description, string example = null, string label = null)
        {
            var category = categories["worldbox:" + type];
            category.CultureIcon = "ui/Icons/" + icon;
            category.Description = description;
            category.TabId = "tab_onomastics_" + type;
            category.TabLabel = label;
            category.TabOrder = order++;
            category.ExampleKind = example;
        }
    }

    private static void RegisterGlyphs(IReadOnlyDictionary<string, NamingCategory> categories)
    {
        var city = categories["worldbox:city"];
        city.InputGlyph("chinesename:source-city", '\uE124', PlaceNames.SourceCity, "移民来源城",
            "ui/Icons/iconCityList", description: "沿用迁出居民的城市地名。");
        city.InputGlyph("chinesename:region-name", '\uE125', PlaceNames.Region, "所属地区",
            "ui/Icons/iconWorldInfo", description: "取新城所在地的地区名，由支持地理地区的模组提供。");
        city.InputGlyph("chinesename:source-city-founding-name", '\uE12C', PlaceNames.SourceFoundingName, "移民来源城旧名",
            "ui/Icons/iconCityList", description: "取移民来源城的建城名，旧档可取原版记录的最早城名。");

        categories[VanillaNames.War].PublishedInputGlyph("chinesename_war_attacker", '\uE119', "attacker",
            "进攻国", "ui/Icons/iconArmyAttackers", "取这场战争的进攻国全名。");
        categories[VanillaNames.War].PublishedInputGlyph("chinesename_war_defender", '\uE11A', "defender",
            "防守国", "ui/Icons/iconArmyDefenders", "取这场战争的防守国全名。");
        categories["worldbox:family"].InputGlyph("chinesename:founder-surname", '\uE11B', "founder-surname",
            "创始人姓氏", "ui/Icons/iconFamilyList", description: "沿用创始人的姓氏，无姓时按当前语言抽取堂号。",
            fallbackPool: "chinesename:family-name");
        categories["worldbox:army"].InputGlyph("chinesename:station-city", '\uE11C', "station",
            "驻地城市", "ui/Icons/iconCityList");
        categories[VanillaNames.Alliance].InputGlyph("chinesename:founding-kingdom", '\uE11D', "origin",
            "发起国", "ui/Icons/iconKingdomList");
        categories["worldbox:book"].InputGlyph("chinesename:book-topic", '\uE11E', "topic",
            "书籍题材", "ui/Icons/iconBooks", "chinesename:book-", "按书籍题材和当前语言抽取一个主题词。",
            fallbackPool: "chinesename:book-name");
        categories["worldbox:item"].InputGlyph("chinesename:equipment-kind", '\uE11F', "equipment",
            "装备种类", "ui/Icons/iconItemType", "chinesename:equipment-kind", "按装备种类取称谓，例如剑、长弓或指环。");
        categories["worldbox:item"].InputGlyph("chinesename:equipment-material", '\uE120', "material",
            "装备材质", "ui/Icons/iconCraftIron", "chinesename:equipment-material", "按实际材质和当前语言抽取材质称谓。");
        categories[VanillaNames.Plot].InputGlyph("chinesename:plot-action", '\uE121', "plot",
            "谋划类型", "ui/Icons/iconDiplomacy", "chinesename:plot-action", "按实际谋划类型和当前语言取称谓。",
            fallbackPool: "chinesename:plot-action");
        categories[VanillaNames.Alliance].InputGlyph("chinesename:alliance-founder-short", '\uE122', "founder-short",
            "发起国简称", "ui/Icons/iconKingdomList", description: "取建立联盟的一方的国名简称。");
        categories[VanillaNames.Alliance].InputGlyph("chinesename:alliance-partner-short", '\uE123', "partner-short",
            "缔约国简称", "ui/Icons/iconAlliance", description: "取共同建立联盟的另一方的国名简称。");
        categories[VanillaNames.War].InputGlyph("chinesename:rebellion-city", '\uE126', PlaceNames.RebellionCity,
            "起事城市", "ui/Icons/iconCityList", description: "沿用这场叛乱发起城市的地名。");
        categories["worldbox:book"].InputGlyph("chinesename:author-city", '\uE127', PlaceNames.AuthorCity,
            "作者所属城市", "ui/Icons/iconCityList", description: "取写书时的城市名。作者文化已有外称时，优先使用外称。");
        categories[VanillaNames.War].InputGlyph("chinesename:war-origin", '\uE129', "origin",
            "命名国", "ui/Icons/iconKingdomList", description: "取该战型指定的命名方国号。");
        categories["worldbox:book"].InputGlyph("chinesename:author-city-founding-name", '\uE12B', PlaceNames.AuthorFoundingName,
            "作者城市旧名", "ui/Icons/iconCityList", description: "取作者城市的建城名，旧档可取原版记录的最早城名。");
    }

    private static void RegisterContexts(IReadOnlyDictionary<string, NamingCategory> categories)
    {
        const string bookPrefix = "book_name_";
        categories["worldbox:book"].PrepareContext = inputs =>
        {
            if (inputs.TryGetValue("topic", out var value) && value is string topic)
                inputs["kind"] = bookPrefix + topic.Replace('-', '_');
            else if (inputs.TryGetValue("kind", out value) && value is string kind && kind.StartsWith(bookPrefix, StringComparison.Ordinal))
                inputs["topic"] = kind.Substring(bookPrefix.Length).Replace('_', '-');
            return true;
        };
        categories[VanillaNames.Plot].PrepareContext = inputs =>
        {
            if (inputs.TryGetValue("plot", out var value) && value is string plot) inputs["kind"] = plot.Replace('-', '_');
            return true;
        };
        categories[VanillaNames.Alliance].PrepareContext = inputs =>
        {
            if (HasText(inputs, "founder-short") && HasText(inputs, "partner-short")) inputs["kind"] = "founding-pair";
            else if (Equals(inputs.TryGetValue("kind", out var kind) ? kind : null, "founding-pair")) inputs.Remove("kind");
            return true;
        };
        categories["worldbox:item"].PrepareContext = inputs =>
        {
            if (inputs.TryGetValue("stage", out var stage) && Equals(stage, "reforge")) inputs["kind"] = stage;
            else if (inputs.TryGetValue("equipment", out var value) && value is string equipment) inputs["kind"] = equipment.Replace('-', '_');
            return true;
        };

        static bool HasText(Dictionary<string, object> inputs, string key) =>
            inputs.TryGetValue(key, out var value) && value is string name && !string.IsNullOrWhiteSpace(name);
    }
}
