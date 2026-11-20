using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Chinese_Name.Naming.Game;

public sealed partial class OnomasticsEditor
{
    private const string ConditionArt = "ui/chinesename/conditions/";
    private static readonly Dictionary<Sprite, Sprite> CompactIcons = new();

    private static Sprite ConditionIcon(string name) => SpriteTextureLoader.getSprite(ConditionArt + name);

    private static Sprite CompactIcon(Sprite sprite)
    {
        if (sprite == null || sprite.packed || sprite.border != Vector4.zero) return sprite;
        if (CompactIcons.TryGetValue(sprite, out var result)) return result;

        // War banners use a tight mesh inside a large, unreadable texture.
        var rect = sprite.rect;
        var min = rect.size;
        var max = Vector2.zero;
        foreach (var vertex in sprite.vertices)
        {
            var point = vertex * sprite.pixelsPerUnit + sprite.pivot;
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        float x = Mathf.Clamp(Mathf.Floor(min.x) - 1, 0, rect.width);
        float y = Mathf.Clamp(Mathf.Floor(min.y) - 1, 0, rect.height);
        float width = Mathf.Clamp(Mathf.Ceil(max.x) + 1, 0, rect.width) - x;
        float height = Mathf.Clamp(Mathf.Ceil(max.y) + 1, 0, rect.height) - y;
        result = sprite;
        if (width > 0 && height > 0 && (width < rect.width || height < rect.height))
        {
            result = Sprite.Create(sprite.texture, new Rect(rect.x + x, rect.y + y, width, height),
                Vector2.one * .5f, sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            result.name = sprite.name;
        }
        return CompactIcons[sprite] = result;
    }

    private static Sprite WarConditionIcon(string kind)
    {
        if (kind == "inspire") return ConditionIcon("independence");
        var war = AssetManager.war_types_library.get(kind);
        return war == null ? null : SpriteTextureLoader.getSprite(war.path_icon);
    }

    private Sprite FieldIcon(string field) => field switch
    {
        "element" => SpriteTextureLoader.getSprite("ui/Icons/iconFire"),
        "form" => ConditionIcon("ball"),
        "defender" => ConditionIcon("defender"),
        "sex" => GameIcon("iconMale"),
        "species" => GameIcon("actor_traits/iconAttractive"),
        VanillaNames.CultureSpecies => GameIcon("iconCulture"),
        "biome" => GameIcon("iconTileSoil"),
        "founder-surname" => GameIcon("iconFamilyList"),
        "founder-short" or "partner-short" => GameIcon("iconKingdomList"),
        "station" => GameIcon("iconCityList"),
        "topic" => GameIcon("iconBooks"),
        "stage" or "equipment" or "material" => GameIcon("iconItemType"),
        "plot" => GameIcon("iconPlot"),
        "kind" when _type.Id == VanillaNames.War => WarConditionIcon("normal"),
        "kind" when _type.Id == "worldbox:motto" => GameIcon("iconDiplomacy"),
        "kind" when _type.Id == VanillaNames.Person => GameIcon("actor_traits/iconAttractive"),
        _ => ConditionIcon("term")
    };

    private static Sprite GameIcon(string name) => SpriteTextureLoader.getSprite("ui/Icons/" + name);

    private Sprite ValueIcon(string field, string value)
    {
        if (_type.Id == VanillaNames.War && field == "kind") return WarConditionIcon(value) ?? FieldIcon(field);
        if (ConditionChoices.TryGetValue(field, out var choices) && choices.Presence)
            return PresenceIcon(field, value == "present");
        if (value is "true" or "false") return PresenceIcon(field, value == "true");

        var native = field switch
        {
            "sex" => GameIcon(value == "male" ? "iconMale" : "iconFemale"),
            "species" or VanillaNames.CultureSpecies => AssetManager.actor_library.get(value)?.getSpriteIcon(),
            "topic" => SpriteForBook(value),
            "plot" => AssetManager.plots_library.get(value.Replace('-', '_'))?.getSprite(),
            "equipment" => AssetManager.items.list.Find(asset => asset.group_id == value)?.getSprites()?.FirstOrDefault(),
            "stage" => GameIcon(value == "reforge" ? "iconEdit" : "iconItemType"),
            "biome" => GameIcon(value is "biome_desert" ? "iconTileSand" : "iconTileSoil"),
            "kind" when _type.Id == "worldbox:motto" => GameIcon(value switch
            {
                "clan" => "iconClanList", "alliance" => "iconAlliance", _ => "iconKingdomList"
            }),
            _ => null
        };
        if (native != null) return native;

        string prefix = "semantic." + field + ".";
        if (value.StartsWith(prefix, StringComparison.Ordinal))
        {
            string variant = value.Substring(prefix.Length);
            if (field == "element")
            {
                var root = SpriteTextureLoader.getSprite("cultiway/icons/element_root/" + variant);
                if (root != null) return root;
                if (variant == "fire") return FieldIcon(field);
            }
            if (variant == "shield") return ConditionIcon("defender");
            return ConditionIcon(variant) ?? FieldIcon(field);
        }
        return FieldIcon(field);
    }

    private static Sprite SpriteForBook(string topic)
    {
        string template = "book_name_" + topic.Replace('-', '_');
        var book = AssetManager.book_types.list.Find(asset => asset.name_template == template);
        return book == null ? null : SpriteTextureLoader.getSpriteList(book.getFullIconPath())?.FirstOrDefault();
    }

    private static Sprite PresenceIcon(string field, bool present) => ConditionIcon(field == "defender"
        ? present ? "defender" : "no_defender"
        : present ? "present" : "absent");

    private Sprite ClauseIcon(ArrangementCondition clause)
    {
        var test = DisplayTest(clause);
        if (test is ArrangementTest.Present or ArrangementTest.Absent)
            return PresenceIcon(clause.Field, test == ArrangementTest.Present);
        if (test == ArrangementTest.Equal && clause.Values.Count == 1 &&
            ConditionChoices.TryGetValue(clause.Field, out var options) && options.Values.ContainsKey(clause.Values[0]))
            return ValueIcon(clause.Field, clause.Values[0]);
        return ConditionIcon(test.ToString().ToLowerInvariant()) ?? FieldIcon(clause.Field);
    }

    private static Button ConditionCard(Transform parent, string id, Sprite sprite, string label, Action action,
        bool selected = false, float size = 22)
    {
        string key = "chinesename_condition_card_" + id;
        NeoModLoader.General.LM.AddToCurrentLocale(key, label);
        var button = PreviewButton(parent, id, key, false, action);
        Measure(button.gameObject, size, size);
        var face = button.image;
        face.color = selected ? new Color(.75f, .67f, .42f, .58f) : Color.clear;
        var picture = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        picture.transform.SetParent(button.transform, false);
        Inset(picture.rectTransform, size >= 20 ? 3 : 2, size >= 20 ? 3 : 2);
        picture.sprite = CompactIcon(sprite ?? ConditionIcon("term"));
        picture.preserveAspect = true;
        picture.raycastTarget = false;
        button.targetGraphic = picture;
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, .93f, .72f);
        colors.pressedColor = new Color(.79f, .76f, .66f);
        button.colors = colors;
        return button;
    }

    private static void ConditionCards<T>(Transform parent, IEnumerable<T> items, float width, Action<Transform, T> draw)
    {
        const int cell = 22;
        const int gap = 2;
        int columns = Mathf.Max(1, Mathf.FloorToInt((width + gap) / (cell + gap)));
        RectTransform row = null;
        int index = 0;
        foreach (var item in items)
        {
            if (index++ % columns == 0)
            {
                row = Row(parent, "ConditionCards", cell, gap);
                Measure(row.gameObject, width, cell);
            }
            draw(row, item);
        }
    }
}
