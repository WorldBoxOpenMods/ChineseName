using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace Chinese_Name.Naming.Game;

internal static class VanillaNames
{
    internal const string Person = "worldbox:person";
    internal const string Kingdom = "worldbox:kingdom";
    internal const string Alliance = "worldbox:alliance";
    internal const string War = "worldbox:war";
    internal const string Plot = "worldbox:plot";
    internal const string Surname = "surname";
    internal const string CultureSpecies = "culture-species";
    private const string SurnameKey = "chinesename.surname.v1";
    private const string LayoutKey = "chinesename.person-parts.v1";
    private const string SeparatorKey = "chinesename.person-separator.v1";

    internal static readonly Dictionary<MetaType, string> ByMeta = new()
    {
        [MetaType.Unit] = Person, [MetaType.Family] = "worldbox:family", [MetaType.Language] = "worldbox:language",
        [MetaType.Culture] = "worldbox:culture", [MetaType.Religion] = "worldbox:religion",
        [MetaType.Clan] = "worldbox:clan", [MetaType.City] = "worldbox:city",
        [MetaType.Kingdom] = Kingdom, [MetaType.Alliance] = Alliance,
        [MetaType.Army] = "worldbox:army", [MetaType.Subspecies] = "worldbox:subspecies",
        [MetaType.War] = War, [MetaType.Item] = "worldbox:item", [MetaType.World] = "worldbox:world"
    };

    private static readonly InputField[] Fields =
    {
        new("species", InputKind.Text, label: "物种"), new("sex", InputKind.Text, label: "性别"),
        new("kind", InputKind.Text, label: "对象类别"), new("origin", InputKind.Text, label: "来源名称"),
        new(CultureSpecies, InputKind.Text, label: "文化起源物种")
    };

    internal static IEnumerable<NameType> Types
    {
        get
        {
            yield return new NameType(Person, "生物姓名", Fields, maxLength: 24, fixedParts: new[] { Surname });
            foreach (var pair in ByMeta)
            {
                if (pair.Key == MetaType.Unit) continue;
                IEnumerable<InputField> fields = pair.Key switch
                {
                    MetaType.War => Fields.Where(field => field.Id != "origin").Concat(new[] {
                        new InputField("origin", InputKind.Text, label: "命名所用国家", example: "临川国"),
                        new InputField("attacker", InputKind.Text, label: "进攻国", example: "临川国"),
                        new InputField("defender", InputKind.Text, label: "防守国", example: "松岭国")
                    }),
                    MetaType.Alliance => Fields.Where(field => field.Id != "origin").Concat(new[] {
                        new InputField("origin", InputKind.Text, label: "发起国", example: "临川国"),
                        new InputField("founder-short", InputKind.Text, label: "发起国简称", example: "临川"),
                        new InputField("partner-short", InputKind.Text, label: "缔约国简称", example: "松岭")
                    }),
                    MetaType.Army => Fields.Append(new("station", InputKind.Text, label: "驻地城市", example: "澄江")),
                    MetaType.Family => Fields.Append(new("founder-surname", InputKind.Text, label: "创始人姓氏", example: "欧阳")),
                    MetaType.City => Fields.Append(new("biome", InputKind.Text, label: "建城地貌", example: "biome_grass")),
                    MetaType.Item => Fields.Concat(new[] {
                        new InputField("equipment", InputKind.Text, label: "装备种类", example: "sword"),
                        new InputField("material", InputKind.Text, label: "材质", example: "iron"),
                        new InputField("stage", InputKind.Text, label: "命名阶段", example: "legendary")
                    }),
                    _ => Fields
                };
                yield return new NameType(pair.Value, Label(pair.Key), fields);
            }
            yield return new NameType("worldbox:motto", "格言", Fields, maxLength: 64);
            yield return new NameType("worldbox:book", "书籍",
                Fields.Append(new("topic", InputKind.Text, label: "书籍题材", example: "history")));
            yield return new NameType(Plot, "谋划",
                Fields.Append(new("plot", InputKind.Text, label: "谋划类型", example: "language-divergence")));
            yield return new NameType("worldbox:player", "造物主", Fields);
        }
    }

    private static string Label(MetaType type) => type switch
    {
        MetaType.Family => "家族", MetaType.Language => "语言", MetaType.Culture => "文化", MetaType.Religion => "宗教",
        MetaType.Clan => "氏族", MetaType.City => "城市", MetaType.Kingdom => "国家", MetaType.Alliance => "联盟",
        MetaType.Army => "军队", MetaType.Subspecies => "亚种", MetaType.War => "战争", MetaType.Item => "装备",
        MetaType.World => "世界", _ => type.ToString()
    };

    internal static NameRequest Request(string type, Actor actor, ulong seed, ActorSex sex = ActorSex.None,
        string kind = null, long? culture = null, long? language = null, IReadOnlyDictionary<string, string> fixedParts = null,
        Dictionary<string, object> inputs = null)
    {
        inputs ??= new Dictionary<string, object>();
        if (actor?.asset != null)
        {
            inputs["species"] = actor.asset.id;
            inputs["kind"] = actor.asset.is_boat ? "boat" : actor.isSapient() ? "sapient" : "creature";
            if (actor.asset.is_boat)
            {
                culture ??= actor.city?.culture?.id ?? actor.kingdom?.culture?.id;
                language ??= actor.city?.language?.id ?? actor.kingdom?.language?.id;
            }
        }
        if (kind != null) inputs["kind"] = kind;
        var owner = culture.HasValue ? World.world?.cultures.get(culture.Value) : actor?.culture;
        if ((owner?.data.original_actor_asset ?? actor?.asset?.id) is string origin) inputs[CultureSpecies] = origin;
        if (sex != ActorSex.None) inputs["sex"] = sex == ActorSex.Male ? "male" : "female";
        if (type == "worldbox:family" && actor != null && SurnameOf(actor) is { Length: > 0 } surname)
            inputs["founder-surname"] = surname;
        string parent = type switch
        {
            NativeOnomastics.City => actor?.kingdom?.name,
            "worldbox:clan" => actor?.city?.name,
            "worldbox:culture" => actor?.culture?.name,
            "worldbox:language" => actor?.language?.name,
            "worldbox:religion" => actor?.religion?.name,
            _ => null
        };
        if (!string.IsNullOrWhiteSpace(parent) && parent.Length <= 256) inputs.TryAdd("origin", parent);
        return new NameRequest(type, seed, culture ?? actor?.culture?.id, language ?? actor?.language?.id, inputs, fixedParts);
    }

    internal static string Generate(string type, Actor source, long seed, ActorSex sex = ActorSex.None, string kind = null,
        long? culture = null, long? language = null, Func<string, bool> accept = null,
        Dictionary<string, object> inputs = null)
    {
        var result = NamingApi.Current.Generate(Request(type, source, unchecked((ulong)seed), sex, kind, culture, language,
            inputs: inputs), accept);
        return result.Success ? result.Text : null;
    }

    private static ulong PersonSeed(Actor actor) =>
        unchecked((ulong)((World.world?.map_stats?.life_dna ?? 0) + actor.getID() * 543));

    private static bool ReadSurname(Actor actor, out string surname)
    {
        actor.data.get(SurnameKey, out surname);
        return surname != null;
    }

    internal static string SurnameOf(Actor actor) => ReadSurname(actor, out var value) ? value : null;

    internal static void ChangeSurname(Actor actor, string value)
    {
        if (!Ids.Text(value, 24) || value != value.Trim())
            throw new ArgumentException("姓氏最多 24 个字符，首尾不能含空白。留空表示无姓氏。");
        actor.data.get(LayoutKey, out string serialized);
        if (!actor.data.custom_name && serialized != null)
        {
            var layout = NamingJson.Read<List<KeyValuePair<string, string>>>(serialized);
            actor.data.get(SeparatorKey, out string separator);
            var name = NameEngine.Join(layout.Select(part => part.Value), separator ?? "");
            if (string.Equals(name, actor.data.name, StringComparison.Ordinal))
            {
                int index = layout.FindIndex(part => part.Key == Surname);
                var surname = new KeyValuePair<string, string>(Surname, value);
                if (index >= 0) layout[index] = surname;
                else layout.Insert(0, surname);
                string updated = NameEngine.Join(layout.Select(part => part.Value), separator ?? "");
                if (updated.Length is < 1 or > 24) throw new ArgumentException("修改后的全名须为 1 至 24 个字符。");
                actor.setName(updated);
                actor.data.set(LayoutKey, NamingJson.Write(layout));
            }
        }
        actor.data.set(SurnameKey, value);
    }

    private static string DrawSurname(Actor actor)
    {
        var result = NamingApi.Current.Generate(Request(Person, actor, PersonSeed(actor), actor.data.sex));
        return result.Success ? result.Parts.TryGetValue(Surname, out var surname) ? surname : "" : null;
    }

    private static string ParentSurname(Actor actor)
    {
        if (actor == null) return null;
        if (ReadSurname(actor, out var surname)) return surname;
        surname = DrawSurname(actor);
        if (surname != null) actor.data.set(SurnameKey, surname);
        return surname;
    }

    private static void AtBirth(Actor child, Actor parent1, Actor parent2)
    {
        if (child == null || ReadSurname(child, out _)) return;
        ulong seed = PersonSeed(child);
        var request = Request(Person, child, seed, child.data.sex);
        var service = NamingApi.Current;
        var effective = service.Inspect(request, null);
        using var conditional = NativeOnomastics.ReadConditionalSurnameCards(request, effective, out var branch);
        var cards = branch ?? NativeOnomastics.Selection(service.ActiveWorld, child.culture, Person);
        bool usesCards = cards?.Enabled == true && (cards.SourceRule == null || NativeOnomastics.BoundRule(cards, request, effective) != null);
        var rule = usesCards ? null : NativeOnomastics.MatchedRule(request, effective);
        if (!usesCards && rule == null) return;
        using var packaged = NativeNamingArrangement.ReadPackaged(Person, rule?.NativeCards);
        bool usesSurname = conditional != null ? NativeOnomastics.UsesSurname(conditional, branch) :
            usesCards ? NativeOnomastics.UsesSurname(child.culture, cards) :
            packaged != null ? NativeOnomastics.UsesSurname(packaged, rule.NativeCards) :
            rule.Parts.Exists(part => part.Id == Surname && NativeNamingArrangement.Visible(part));
        if (!usesSurname)
        {
            child.data.set(SurnameKey, "");
            return;
        }
        var father = parent1?.isSexMale() == true ? parent1 : parent2?.isSexMale() == true ? parent2 : parent1;
        var mother = parent2?.isSexFemale() == true ? parent2 : parent1?.isSexFemale() == true ? parent1 : parent2;
        if (father == mother) mother = father == parent1 ? parent2 : parent1;
        var inheritance = rule?.Inheritance ?? cards.Inheritance;
        if (inheritance == SurnameOrder.New) return;
        bool motherFirst = inheritance == SurnameOrder.Mother ||
                           (inheritance == SurnameOrder.Random && new NamingRandom(seed).Next() < 0.5);
        var first = motherFirst ? mother : father;
        var second = motherFirst ? father : mother;
        string surname = ParentSurname(first);
        if (string.IsNullOrEmpty(surname)) surname = ParentSurname(second);
        if (!string.IsNullOrEmpty(surname)) child.data.set(SurnameKey, surname);
    }

    [HarmonyPatch(typeof(NameGenerator), nameof(NameGenerator.generateName))]
    internal static class ActorNames
    {
        private static bool Prefix(Actor pActor, MetaType pType, long pSeed, ActorSex pSex, ref string __result)
        {
            if (pActor == null || pType == MetaType.Unit || !ByMeta.TryGetValue(pType, out string type)) return true;
            __result = Generate(type, pActor, pSeed, pSex);
            return __result == null;
        }
    }

    [HarmonyPatch(typeof(Actor), nameof(Actor.generateNewName))]
    internal static class FirstName
    {
        private static bool Prefix(Actor __instance) => !TryNamePerson(__instance);
    }

    [HarmonyPatch(typeof(City), nameof(City.generateName))]
    internal static class CityName
    {
        private static bool Prefix(City __instance, Actor pActor)
        {
            long seed = unchecked(World.world.map_stats.life_dna + __instance.id);
            string name = Generate(NativeOnomastics.City, pActor, seed, inputs: CityInputs(__instance));
            if (name == null) return true;
            __instance.setName(name);
            __instance.data.name_culture_id = __instance.culture?.id ?? -1;
            return false;
        }
    }

    internal static Dictionary<string, object> CityInputs(City city)
    {
        var inputs = new Dictionary<string, object>();
        if (city.getTile()?.Type?.biome_asset?.id is string biome) inputs["biome"] = biome;
        if (city.kingdom?.name is { Length: > 0 and <= 256 } country) inputs["origin"] = country;
        return inputs;
    }

    internal static bool TryNamePerson(Actor actor, Func<string, bool> accept = null)
    {
        IReadOnlyDictionary<string, string> fixedParts = null;
        if (ReadSurname(actor, out var surname))
            fixedParts = new Dictionary<string, string> { [Surname] = surname };
        var result = NamingApi.Current.Generate(Request(Person, actor, PersonSeed(actor),
            actor.data.sex, fixedParts: fixedParts), accept);
        if (!result.Success) return false;
        actor.setName(result.Text);
        actor.data.name_culture_id = actor.culture?.id ?? -1L;
        if (result.Parts.TryGetValue(Surname, out var part)) actor.data.set(SurnameKey, part);
        else if (surname == null) actor.data.set(SurnameKey, "");
        actor.data.set(LayoutKey, NamingJson.Write(result.PartOrder.Select(id =>
            new KeyValuePair<string, string>(id, result.Parts[id])).ToArray()));
        actor.data.set(SeparatorKey, result.Separator ?? "");
        return true;
    }

    [HarmonyPatch(typeof(BabyMaker), nameof(BabyMaker.makeBaby))]
    internal static class Birth
    {
        private static void Postfix(Actor __result, Actor pParent1, Actor pParent2) => AtBirth(__result, pParent1, pParent2);
    }

    [HarmonyPatch(typeof(ai.ActorTool), nameof(ai.ActorTool.copyImportantData))]
    internal static class CopyIdentity
    {
        private static void Postfix(ActorData pFrom, ActorData pCloneTo)
        {
            pFrom.get(SurnameKey, out string surname);
            pFrom.get(LayoutKey, out string layout);
            pFrom.get(SeparatorKey, out string separator);
            if (surname != null) pCloneTo.set(SurnameKey, surname);
            if (layout != null) pCloneTo.set(LayoutKey, layout);
            if (separator != null) pCloneTo.set(SeparatorKey, separator);
        }
    }

    [HarmonyPatch(typeof(ai.ActorTool), nameof(ai.ActorTool.copyUnitToOtherUnit))]
    internal static class MaterializeIdentity
    {
        private static void Prefix(Actor pParent) => pParent.getName();
    }
}
