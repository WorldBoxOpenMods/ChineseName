using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Chinese_Name.Naming.Game;

public sealed partial class OnomasticsEditor
{
    private const string ArrangementTooltip = "chinesename_arrangement";
    private const int TooltipIconSlots = 11;

    private sealed class ArrangementTipData : TooltipData
    {
        internal Sprite[] Conditions;
        internal Sprite[] Glyphs;
        internal string[] Samples;
        internal string EmptyConditions;
    }

    internal static void RegisterArrangementTooltip() => AssetManager.tooltips.add(new TooltipAsset
    {
        id = ArrangementTooltip,
        callback = (tooltip, _, data) => DrawArrangementTooltip(tooltip, (ArrangementTipData)data)
    });

    private void ShowArrangementTooltip(Button button, ConditionalArrangement item)
    {
        if (!Visible || !CurrentContext || _rule == null) return;
        var service = NamingApi.Current;
        var choice = item?.Cards ?? (UsesCards(_card, _rule) ? _card : NativeNamingArrangement.ForRule(_type.Id, _rule));
        bool matches = true;
        var request = item == null ? PartPreviewRequest(_type, _culture, _page, _rule) : Scenario(item, out matches);
        var prepared = service.PrepareContext(request);
        var effective = service.Inspect(request, item == null ? _draft : service.ActiveWorld);
        using var cards = item == null ? ReadDefaultTooltipCards(choice, effective) :
            NativeNamingArrangement.ReadPackaged(_type.Id, choice);
        var conditions = item == null ? Array.Empty<Sprite>() : ArrangementSyntax.Tokens(item.Expression)
            .Select(token => token.Clause == null ? ConditionIcon(token.Symbol.ToString().ToLowerInvariant()) : ClauseIcon(token.Clause))
            .ToArray();
        var glyphs = cards?.getFullTemplateData()
            .Select(id => AssetManager.onomastics_library.get(id)?.getSprite()).ToArray() ?? Array.Empty<Sprite>();

        // Generate from the hovered arrangement even if another row would win in the world.
        var samples = Array.Empty<string>();
        string notice = "暂无命名样例";
        if (item is { IsConfigured: true } && !matches) notice = "未找到符合条件的命名样例";
        else if (cards != null && choice.Enabled && prepared != null)
        {
            var rule = item == null ? _rule : NativeOnomastics.BoundRule(choice, prepared, effective);
            if (choice.SourceRule == null || rule != null)
                samples = CollectArrangementSamples(prepared, effective, choice, rule, cards);
        }
        Tooltip.show(button.gameObject, ArrangementTooltip, new ArrangementTipData
        {
            tip_name = item == null ? "默认排列" : item.Disabled ? "条件排列 · 已停用" : "条件排列",
            tip_description = notice,
            Conditions = conditions,
            Glyphs = glyphs,
            Samples = samples,
            EmptyConditions = item == null ? "无条件" : "未设置条件"
        });
    }

    private string[] CollectArrangementSamples(NameRequest request, EffectiveNames effective, CardNaming choice, Rule rule,
        OnomasticsData cards)
    {
        NativeNamingArrangement.TryUpgrade(_type.Id, cards, rule, choice, out var previewChoice);
        var samples = new List<string>(PreviewNames);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int draw = 0; draw < PreviewDraws && samples.Count < PreviewNames; draw++)
        {
            var sample = draw == 0 ? request : request.WithSeed(unchecked(request.Seed + (ulong)draw));
            var result = NativeOnomastics.GenerateCards(sample, effective, previewChoice, rule, cards);
            if (result.Failures.Any(failure => failure.Reason is NameFailureReason.MissingGlyph or NameFailureReason.InvalidArrangement)) break;
            if (IsPreviewName(result) && seen.Add(result.Text)) samples.Add(result.Text);
        }
        return samples.ToArray();
    }

    private OnomasticsData ReadDefaultTooltipCards(CardNaming choice, EffectiveNames effective)
    {
        if (NativeNamingTab.DraftTemplate(_culture.id, _type.Id) is string template &&
            NativeNamingArrangement.ReadTemplate(template) is { } draft)
            return draft;
        var cards = NativeOnomastics.Read(_culture, _type.Id, choice);
        if (!UsesCards(_card, _rule)) NativeNamingArrangement.FillDefault(cards, _type.Id, _rule, effective, out _);
        return cards;
    }

    private static void DrawArrangementTooltip(Tooltip tooltip, ArrangementTipData data)
    {
        int widest = Mathf.Min(TooltipIconSlots, Mathf.Max(data.Conditions.Length, data.Glyphs.Length));
        float width = Mathf.Max(data.Samples.Length > 1 ? 190 : 113, 14 * widest + 16);
        ((RectTransform)tooltip.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        tooltip.topGraphics.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        ((RectTransform)tooltip.description.transform.parent).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 9);
        tooltip.description.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 9);
        tooltip.name.text = data.tip_name;
        tooltip.description.fontSize = tooltip.name.fontSize;
        tooltip.description.supportRichText = false;
        tooltip.setDescription(data.Samples.Length == 0 ? data.tip_description : FormatArrangementSamples(tooltip.description, data.Samples));
        DrawArrangementTooltipRow(tooltip, data, "ConditionIcons", data.Conditions, data.EmptyConditions, width - 10);
        DrawArrangementTooltipRow(tooltip, data, "ArrangementGlyphs", data.Glyphs, "字形不可用", width - 10);
    }

    private static string FormatArrangementSamples(Text label, string[] samples)
    {
        if (samples.Length == 1) return samples[0];
        const string gap = "　";
        var size = label.rectTransform.rect.size;
        var settings = label.GetGenerationSettings(size);
        settings.horizontalOverflow = HorizontalWrapMode.Overflow;
        var generator = label.cachedTextGeneratorForLayout;
        var rows = new List<string>();
        string row = samples[0];
        for (int index = 1; index < samples.Length; index++)
        {
            string next = row + gap + samples[index];
            if (generator.GetPreferredWidth(next, settings) / label.pixelsPerUnit <= size.x)
                row = next;
            else
            {
                rows.Add(row);
                row = samples[index];
            }
        }
        rows.Add(row);
        return string.Join("\n", rows);
    }

    private static void DrawArrangementTooltipRow(Tooltip tooltip, TooltipData data, string id, Sprite[] sprites,
        string emptyText, float width)
    {
        var row = tooltip.transform.Find(id)?.GetComponent<TooltipIconsRow>();
        if (row == null)
        {
            var template = Resources.Load<Tooltip>("tooltips/tooltip_actor")
                .GetComponentInChildren<TooltipActorTraitsRow>(true);
            var native = Instantiate(template, tooltip.transform, false);
            native.name = id;
            native.transform.SetSiblingIndex(tooltip.description.transform.parent.GetSiblingIndex());
            native.GetComponent<Image>().raycastTarget = false;
            row = native.gameObject.AddComponent<TooltipIconsRow>();
            row.items_parent = native.items_parent;
            row.item = native.item;
            row.item.preserveAspect = true;
            row.item.raycastTarget = false;
            Inset((RectTransform)row.items_parent, 2, 2);
            Destroy(native);

            var emptyLabel = TextAt(row.transform, "Empty", "", 7, TextAnchor.MiddleCenter);
            emptyLabel.color = new Color32(182, 170, 144, 255);
            Inset(emptyLabel.rectTransform, 2, 0);
            var overflowLabel = TextAt(row.items_parent, "Overflow", "", 7, TextAnchor.MiddleCenter);
            overflowLabel.resizeTextForBestFit = true;
            overflowLabel.resizeTextMinSize = 4;
            overflowLabel.resizeTextMaxSize = 7;
        }

        int count = sprites.Length > TooltipIconSlots ? TooltipIconSlots - 1 : sprites.Length;
        for (int i = 0; i < count; i++)
            row.addIcon(CompactIcon(sprites[i]) ?? ConditionIcon("term"));
        row.init(tooltip, data);
        row.gameObject.SetActive(true);
        Measure(row.gameObject, width, row.items_parent.GetComponent<GridLayoutGroup>().cellSize.y + 4);

        var empty = row.transform.Find("Empty").GetComponent<Text>();
        empty.text = emptyText;
        empty.gameObject.SetActive(count == 0);
        var overflow = row.items_parent.Find("Overflow").GetComponent<Text>();
        overflow.text = "+" + (sprites.Length - count);
        overflow.gameObject.SetActive(sprites.Length > count);
        overflow.transform.SetAsLastSibling();
    }
}
