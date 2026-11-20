using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Chinese_Name.Naming.Game;

public sealed partial class OnomasticsEditor
{
    private string _tokenArrangement;
    private List<(ConditionSymbol Symbol, ArrangementCondition Clause)> _tokens;
    private readonly Dictionary<string, List<(ConditionSymbol Symbol, ArrangementCondition Clause)>> _unfinishedConditions = new();
    private int _conditionCursor;
    private string _presetField;
    private bool _previewMatchesExpression;

    private IReadOnlyDictionary<string, NamingService.CultureCondition> ConditionChoices => NamingApi.Current.Conditions(_type.Id);

    private void CloseConditionEditor()
    {
        _tokenArrangement = null;
        _presetField = null;
    }

    private string FieldLabel(string id) => ConditionChoices.TryGetValue(id, out var choices) ? choices.Label :
        _type.Fields.TryGetValue(id, out var field) ? field.Label : id;

    private ArrangementTest DisplayTest(ArrangementCondition condition)
    {
        if (condition.Test != ArrangementTest.Registered) return condition.Test;
        if (!ConditionChoices.TryGetValue(condition.Field, out var choices) || !choices.Presence) return ArrangementTest.Equal;
        if (condition.Values.Count == 1) return condition.Values[0] == "present" ? ArrangementTest.Present : ArrangementTest.Absent;
        return ArrangementTest.Registered;
    }

    private static string TestLabel(ArrangementTest test) => test switch
    {
        ArrangementTest.Equal => "等于",
        ArrangementTest.Prefix => "开头",
        ArrangementTest.Suffix => "结尾",
        ArrangementTest.Contains => "包含",
        ArrangementTest.Present => "有值",
        ArrangementTest.Absent => "无值",
        ArrangementTest.Greater => "大于",
        ArrangementTest.AtLeast => "至少",
        ArrangementTest.Less => "小于",
        ArrangementTest.AtMost => "至多",
        _ => "任意"
    };

    private string ValueLabel(string field, string value)
    {
        if (ConditionChoices.TryGetValue(field, out var choices) && !choices.Presence &&
            choices.Values.TryGetValue(value, out var label)) return label;
        return _type.Fields.TryGetValue(field, out var input) && input.Kind == InputKind.Flag
            ? value == "true" ? "是" : "否" : value;
    }

    private string ConditionLabel(ArrangementCondition condition)
    {
        var test = DisplayTest(condition);
        string label = FieldLabel(condition.Field);
        string values = condition.Values.Count == 0 ? "待输入" :
            string.Join("、", condition.Values.Select(value => ValueLabel(condition.Field, value)));
        return test switch
        {
            ArrangementTest.Equal => label + "＝" + values,
            ArrangementTest.Prefix => label + "以「" + values + "」开头",
            ArrangementTest.Suffix => label + "以「" + values + "」结尾",
            ArrangementTest.Contains => label + "包含「" + values + "」",
            ArrangementTest.Present => "有" + label,
            ArrangementTest.Absent => "无" + label,
            ArrangementTest.Registered => label + "任意",
            _ => label + TestLabel(test) + values
        };
    }

    private string ArrangementLabel(ConditionalArrangement arrangement) =>
        arrangement == null ? "默认" : ExpressionLabel(arrangement.Expression, root: true);

    private string ExpressionLabel(ArrangementCondition node, bool root = false)
    {
        if (node.Children == null)
        {
            string leaf = ConditionLabel(node);
            return node.Negated ? "非（" + leaf + "）" : leaf;
        }
        string content = node.Children.Count == 0 ? (root ? "设置条件" : "待添加条件") :
            string.Join(node.Any ? " 或 " : " 与 ", node.Children.Select(child => ExpressionLabel(child)));
        if (node.Negated) return "非（" + content + "）";
        return root ? content : "（" + content + "）";
    }

    private static string SymbolLabel(ConditionSymbol symbol) => symbol switch
    {
        ConditionSymbol.And => "与",
        ConditionSymbol.Or => "或",
        ConditionSymbol.Not => "非",
        ConditionSymbol.Open => "（",
        ConditionSymbol.Close => "）",
        _ => ""
    };

    private void DrawConditionArea(Transform parent, ConditionalArrangement item)
    {
        if (_tokenArrangement != item.Id)
        {
            _tokenArrangement = item.Id;
            _tokens = _unfinishedConditions.TryGetValue(item.Id, out var pending)
                ? pending : ArrangementSyntax.Tokens(item.Expression);
            _conditionCursor = _tokens.Count;
        }
        DrawConditionHeading(parent, item);
        DrawConditionLine(parent);
        const float width = PreviewWidth - 6;
        var operators = ConditionPalette(parent, "ConditionOperators", "运算符盘");
        var symbols = new[] { ConditionSymbol.And, ConditionSymbol.Or, ConditionSymbol.Not, ConditionSymbol.Open, ConditionSymbol.Close };
        ConditionCards(operators, symbols, width, (row, symbol) =>
            ConditionCard(row, "ConditionSymbol." + symbol, ConditionIcon(symbol.ToString().ToLowerInvariant()),
                SymbolLabel(symbol), () => Change(() => InsertCondition(symbol))));
        FinishPalette(operators);

        var presets = ConditionPalette(parent, "ConditionPresets", "预制条件盘");
        DrawConditionPresets(presets, width);
        FinishPalette(presets);
    }

    private void DrawConditionHeading(Transform parent, ConditionalArrangement item)
    {
        var heading = ArrangementTitle(parent, "ArrangementConditionsTitle", "排列条件");
        var toggle = HeadingButton(heading.Find("icon_left"), "ArrangementEnabled." + item.Id, "iconOn",
            item.Disabled ? "启用排列" : "停用排列", () => Change(() => ToggleArrangement(item)));
        if (item.Disabled) toggle.image.color = new Color(.55f, .55f, .55f);
        toggle.interactable = item.IsConfigured;
        HeadingButton(heading.Find("icon_right"), "ArrangementDelete." + item.Id, "iconOff", "删除排列", () => Change(() =>
        {
            _unfinishedConditions.Remove(item.Id);
            DeleteArrangement();
        }));

        static Button HeadingButton(Transform slot, string id, string icon, string hint, Action action)
        {
            string tip = "chinesename_condition_action_" + hint;
            NeoModLoader.General.LM.AddToCurrentLocale(tip, hint);
            slot.name = id;
            var image = slot.GetComponent<Image>();
            image.sprite = SpriteTextureLoader.getSprite("ui/Icons/" + icon);
            image.raycastTarget = true;
            var button = slot.GetComponent<Button>();
            button.targetGraphic = image;
            button.enabled = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                MusicBox.playSoundUI("event:/SFX/UI/Click");
                action();
            });
            var tooltip = slot.gameObject.AddComponent<TipButton>();
            tooltip.textOnClick = tip;
            tooltip.textOnClickDescription = "";
            tooltip.showOnClick = false;
            return button;
        }
    }

    private RectTransform ConditionPalette(Transform parent, string id, string caption)
    {
        var panel = Stack(parent, id);
        panel.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(3, 3, 2, 2);
        FrameConditionPanel(panel);
        AddPanelCaption(panel, caption);
        return panel;
    }

    private static void FinishPalette(RectTransform panel)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        Measure(panel.gameObject, PreviewWidth, LayoutUtility.GetPreferredHeight(panel));
    }

    private void DrawConditionLine(Transform parent)
    {
        const float spacing = 3;
        const int inset = 3;
        const float controlSize = 16;
        float height = _nativeArrangement.rect.height;
        float cell = _tab.parent_name_variation_1.GetComponent<GridLayoutGroup>().cellSize.y;
        float width = _nativeArrangement.rect.width - 2 * inset - 3 * (controlSize + spacing);
        var line = Row(parent, "ConditionLine", height, spacing);
        Measure(line.gameObject, _nativeArrangement.rect.width, height);
        line.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(inset, inset, 0, 0);
        FrameConditionPanel(line);
        AddPanelCaption(line, "条件行");

        var viewport = new GameObject("ConditionLineScroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewport.transform.SetParent(line, false);
        Measure(viewport, width, height);
        viewport.GetComponent<Image>().color = Color.clear;
        var tokens = Row(viewport.transform, "ConditionTokens", height, spacing);
        var caret = CreateConditionCaret(tokens);
        float caretPadding = caret.rect.width * .5f + 2;
        float glyphWidth = _tokens.Count * cell + Mathf.Max(0, _tokens.Count - 1) * spacing;
        float contentWidth = Mathf.Max(width, glyphWidth + spacing + 2 * caretPadding);
        Measure(tokens.gameObject, contentWidth, height);
        tokens.anchorMin = tokens.anchorMax = new Vector2(0, .5f);
        tokens.pivot = new Vector2(0, .5f);
        tokens.anchoredPosition = Vector2.zero;
        var scroll = viewport.GetComponent<ScrollRect>();
        scroll.viewport = (RectTransform)viewport.transform;
        scroll.content = tokens;
        scroll.vertical = false;
        scroll.inertia = false;
        scroll.scrollSensitivity = 24;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        Button left = null, right = null, backspace = null;
        left = ConditionCard(line, "ConditionLeft", SpriteTextureLoader.getSprite("ui/Icons/iconArrowMetaLeft"),
            "光标左移", () => MoveCursor(_conditionCursor - 1), size: controlSize);
        right = ConditionCard(line, "ConditionRight", SpriteTextureLoader.getSprite("ui/Icons/iconArrowMetaRight"),
            "光标右移", () => MoveCursor(_conditionCursor + 1), size: controlSize);
        viewport.transform.SetAsLastSibling();
        backspace = ConditionCard(line, "ConditionRemove", AssetManager.onomastics_library.get("backspace").getSprite(),
            "删除光标前的条件或符号", () => Change(() =>
        {
            _tokens.RemoveAt(--_conditionCursor);
            CommitConditionLine();
        }), size: controlSize);
        for (int i = 0; i < _tokens.Count; i++)
        {
            int position = i + 1;
            var token = _tokens[i];
            var picture = token.Clause == null ? ConditionIcon(token.Symbol.ToString().ToLowerInvariant()) : ClauseIcon(token.Clause);
            string label = token.Clause == null ? SymbolLabel(token.Symbol) : ConditionLabel(token.Clause);
            var button = ConditionCard(tokens, "ConditionToken." + i, picture, label,
                () => MoveCursor(position), size: cell);
            button.gameObject.AddComponent<ScrollableButton>();
        }
        caret.transform.SetAsLastSibling();
        MoveCursor(_conditionCursor);

        void MoveCursor(int position)
        {
            _conditionCursor = position;
            // Place the marker between cards without moving the cards themselves.
            float x = _tokens.Count == 0 ? contentWidth * .5f :
                (contentWidth - glyphWidth - spacing) * .5f + position * (cell + spacing);
            caret.anchoredPosition = new Vector2(x, 0);
            scroll.StopMovement();
            float offset = Mathf.Clamp(-tokens.anchoredPosition.x,
                Mathf.Max(0, x + caretPadding - width), Mathf.Min(contentWidth - width, x - caretPadding));
            tokens.anchoredPosition = new Vector2(-offset, 0);
            SetAvailable(left, position > 0);
            SetAvailable(right, position < _tokens.Count);
            SetAvailable(backspace, position > 0);
        }

        static void SetAvailable(Button button, bool available)
        {
            button.interactable = available;
            button.GetComponent<Image>().color = new Color(1, 1, 1, available ? 1 : .45f);
        }
    }

    private RectTransform CreateConditionCaret(Transform parent)
    {
        var source = _tab.selected_icon_effect_2;
        Vector2 arrowSize = source.rectTransform.rect.size;
        var caret = new GameObject("ConditionCaret", typeof(RectTransform), typeof(LayoutElement));
        var rect = (RectTransform)caret.transform;
        rect.SetParent(parent, false);
        caret.GetComponent<LayoutElement>().ignoreLayout = true;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.up;
        rect.sizeDelta = new Vector2(arrowSize.x, -4);

        var line = new GameObject("Line", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        line.transform.SetParent(rect, false);
        line.color = new Color32(255, 210, 49, 255);
        line.raycastTarget = false;
        line.rectTransform.anchorMin = new Vector2(.5f, 0);
        line.rectTransform.anchorMax = new Vector2(.5f, 1);
        line.rectTransform.offsetMin = new Vector2(-.5f, arrowSize.y - 1);
        line.rectTransform.offsetMax = new Vector2(.5f, 1 - arrowSize.y);
        AddArrow("UpperArrow", top: true);
        AddArrow("LowerArrow", top: false);
        return rect;

        void AddArrow(string name, bool top)
        {
            var arrow = Instantiate(source, rect, false);
            arrow.name = name;
            arrow.raycastTarget = false;
            var slot = arrow.rectTransform;
            slot.anchorMin = slot.anchorMax = new Vector2(.5f, top ? 1 : 0);
            slot.pivot = new Vector2(.5f, .5f);
            slot.anchoredPosition3D = new Vector3(0, arrowSize.y * (top ? -.5f : .5f), 0);
            slot.localRotation = top ? Quaternion.identity : Quaternion.Euler(0, 0, 180);
        }
    }

    private void DrawConditionPresets(Transform panel, float width)
    {
        var choices = ConditionChoices.Values.ToArray();
        if (choices.Sum(field => field.Values.Count) > 14)
        {
            _presetField ??= choices[0].Field;
            ConditionCards(panel, choices, width, (row, field) =>
                ConditionCard(row, "ConditionPresetField." + field.Field, FieldIcon(field.Field), field.Label,
                    () => Change(() => _presetField = field.Field), field.Field == _presetField));
            choices = choices.Where(field => field.Field == _presetField).ToArray();
        }
        var presets = choices.SelectMany(field => field.Values.Select(value => (Field: field, Value: value)));
        ConditionCards(panel, presets, width, (row, preset) =>
        {
            var (field, value) = preset;
            ConditionCard(row, "ConditionPreset." + field.Field + "." + value.Key, ValueIcon(field.Field, value.Key), value.Value,
                () => Change(() => InsertCondition(ConditionSymbol.Clause, new ArrangementCondition
                {
                    Field = field.Field,
                    Test = field.Presence ? value.Key == "present" ? ArrangementTest.Present : ArrangementTest.Absent :
                        ArrangementTest.Equal,
                    Values = field.Presence ? new() : new() { value.Key }
                })));
        });
    }

    private void InsertCondition(ConditionSymbol symbol, ArrangementCondition clause = null)
    {
        if (_tokens.Count >= ArrangementCondition.MaxNodes * 4) throw new InvalidOperationException("条件表达式太长。");
        _tokens.Insert(_conditionCursor++, (symbol, clause));
        CommitConditionLine();
    }

    private void CommitConditionLine()
    {
        var expression = ArrangementSyntax.Parse(_tokens);
        if (expression == null || _tokens.Count != 0 && !expression.IsConfigured)
        {
            _unfinishedConditions[_editingArrangement.Id] = _tokens;
            return;
        }
        if (_commitPending) Render();
        var item = NamingJson.Copy(_editingArrangement);
        item.Expression = expression;
        ReplaceArrangement(item);
        _unfinishedConditions.Remove(item.Id);
    }

    private NameRequest Scenario(ConditionalArrangement arrangement, out bool matches)
    {
        var service = NamingApi.Current;
        var inputs = _type.Fields.Values.Select(field => (field.Id, Value: _page.Samples.TryGetValue(field.Id, out var sample)
            ? ArrangementExamples.Input(field, sample) : field.Example)).Where(pair => pair.Value != null).ToDictionary(pair => pair.Id, pair => pair.Value);
        matches = ArrangementExamples.TryApply(service, _type, arrangement, inputs);
        switch (_type.Id)
        {
            case "worldbox:book" when inputs.TryGetValue("topic", out var topic):
                inputs["kind"] = "book_name_" + ((string)topic).Replace('-', '_');
                break;
            case VanillaNames.Plot when inputs.TryGetValue("plot", out var plot):
                inputs["kind"] = ((string)plot).Replace('-', '_');
                break;
            case VanillaNames.Alliance:
                if (inputs.ContainsKey("founder-short") && inputs.ContainsKey("partner-short")) inputs["kind"] = "founding-pair";
                else inputs.Remove("kind");
                break;
            case "worldbox:item" when inputs.TryGetValue("stage", out var stage):
                if (Equals(stage, "reforge")) inputs["kind"] = stage;
                else if (inputs.TryGetValue("equipment", out var equipment)) inputs["kind"] = ((string)equipment).Replace('-', '_');
                break;
        }
        if (_type.Id == VanillaNames.War && inputs.TryGetValue("kind", out var war) && Equals(war, "spite") &&
            inputs.TryGetValue("defender", out var defender))
        {
            inputs.Remove("defender");
            if (matches && !service.MatchesExpression(arrangement, Request())) inputs["defender"] = defender;
        }
        var request = Request();
        matches = matches && service.MatchesExpression(arrangement, request);
        return request;

        NameRequest Request() => new(_type.Id, _page.Seed, _culture.id, _page.ReferenceLanguage, inputs);
    }

    private string ConditionalPreviewNotice(NameResult result)
    {
        if (_editingArrangement != null && _unfinishedConditions.ContainsKey(_editingArrangement.Id))
            return "条件未完成 · 沿用已保存的表达式";
        if (_editingArrangement is { IsConfigured: false }) return "设置好条件后将参与取名";
        if (_editingArrangement?.Disabled == true) return "此排列已停用";
        if (_editingArrangement != null && !_previewMatchesExpression) return "未找到符合此条件的预览情境";
        const string prefix = "排列:";
        string id = result?.Diagnostics.FirstOrDefault(item => item.StartsWith(prefix, StringComparison.Ordinal))?.Substring(prefix.Length);
        if (id == _editingArrangement?.Id) return null;
        return "此情境使用「" + ArrangementLabel(Arrangements.FirstOrDefault(item => item.Id == id)) + "」";
    }
}
