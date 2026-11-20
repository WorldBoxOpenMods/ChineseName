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

    internal static bool WorldScoped(string type) => type is "worldbox:subspecies" or "worldbox:world" or "worldbox:player";

    internal static void Register(NamingService service)
    {
        Choices("person", "sex", "性别", ("male", "男性"), ("female", "女性"));
        Choices("person", "kind", "生物类别", ("sapient", "智慧生物"), ("creature", "普通生物"), ("boat", "船只"));
        Choices("person", "species", "个体物种", Origins.Concat(new[]
        {
            ("cat", "猫"), ("wolf", "狼"), ("boat_fishing", "渔船"),
            ("boat_trading", "商船"), ("boat_transport", "运输船"), ("battle_boat", "战船")
        }).ToArray());
        Choices("city", "biome", "建城地貌", Biomes);
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

        foreach (var type in VanillaNames.Types.Where(type => !WorldScoped(type.Id) && type.Id != VanillaNames.War))
            service.RegisterCultureCondition(type.Id, VanillaNames.CultureSpecies, "文化族源",
                Origins.ToDictionary(value => value.Id, value => value.Label, StringComparer.Ordinal));

        Choices("subspecies", "kind", "亚种起源", Biomes.Concat(new[] { ("skeleton", "骸骨化"), ("uplift", "开智") }).ToArray());
        Choices("subspecies", "species", "物种", Origins);
        var sizes = new[] { ("tiny", "微型"), ("small", "小型"), ("standard", "标准"), ("large", "大型"),
            ("huge", "巨型"), ("gigantic", "庞大"), ("titanic", "泰坦级"), ("iceberg", "冰山级") };
        Choices("world", "kind", "地图尺寸", sizes);
        Choices("player", "kind", "地图尺寸", sizes);

        void Choices(string type, string field, string title, params (string Id, string Label)[] values) =>
            service.RegisterCultureCondition("worldbox:" + type, field, title,
                values.ToDictionary(value => value.Id, value => value.Label, StringComparer.Ordinal));

        void Presence(string type, string field, string title, string present, string absent) =>
            service.RegisterCulturePresence("worldbox:" + type, field, title, present, absent);
    }
}
