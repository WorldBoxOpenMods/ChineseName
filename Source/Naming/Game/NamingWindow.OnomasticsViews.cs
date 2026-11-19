using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Chinese_Name.Naming.Game;

public sealed partial class NamingWindow
{
    public sealed partial class OnomasticsEditor
    {
        private const float PreviewWidth = Width - 12;
        private const float PreviewToolbarHeight = 14;
        private const float SampleRowHeight = 12;
        private static readonly Color SampleColor = new Color32(231, 220, 193, 255);
        private static readonly Color IssueColor = new Color32(218, 210, 187, 255);
        private readonly List<(Text Label, NameResult Name)> _sampleLabels = new();

        private void RenderPreview()
        {
            _sampleLabels.Clear();
            var toolbar = Row(_preview, "PreviewToolbar", PreviewToolbarHeight, 3);
            LanguageFlags(toolbar);
            const string tooltip = "chinesename_preview_resample";
            NeoModLoader.General.LM.AddToCurrentLocale(tooltip, "换一组名字");
            var dice = PreviewButton(toolbar, "Resample", tooltip, false, () => Change(() => _page.Seed += PreviewDraws));
            Icon(dice.transform, "Dice", "ui/Icons/iconDice", 10);

            var result = _page.Result;
            var poolIssue = result?.Diagnostics.FirstOrDefault(value => value.StartsWith("词库字形", StringComparison.Ordinal));
            if (_page.Message != null || !IsPreviewName(result))
            {
                PreviewLine("PreviewError", "草稿无法预览", 14, 11, SampleColor);
                var issue = result?.Diagnostics.FirstOrDefault(value => value.StartsWith("卡片", StringComparison.Ordinal) ||
                    value.StartsWith("命名卡片", StringComparison.Ordinal) || value.StartsWith("请为", StringComparison.Ordinal));
                PreviewLine("PreviewIssue", _page.Message ?? poolIssue ?? issue ?? "请检查命名设置。", 20, 9, IssueColor);
                return;
            }

            RenderSamples(poolIssue == null ? 3 : 2);
            if (poolIssue != null) PreviewLine("PreviewIssue", poolIssue, 10, 8, IssueColor);
        }

        private void PreviewLine(string id, string value, float height, int size, Color color)
        {
            var label = TextAt(_preview, id, value, size, TextAnchor.MiddleCenter);
            label.color = color;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 8;
            label.resizeTextMaxSize = size;
            Measure(label.gameObject, PreviewWidth, height);
        }

        private void RenderSamples(int rowsAvailable)
        {
            const float gap = 4;
            const float minWidth = (PreviewWidth - gap * 3) / 4;
            var list = Stack(_preview, "PreviewSamples");
            var layout = list.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 0;
            Measure(list.gameObject, PreviewWidth, rowsAvailable * SampleRowHeight);

            RectTransform row = null;
            float occupied = 0;
            foreach (var sample in _page.PreviewSamples)
            {
                var label = TextAt(list, _sampleLabels.Count == 0 ? "PreviewName" : "PreviewSample",
                    sample.Text, 10, TextAnchor.MiddleCenter);
                float width = Mathf.Clamp(Mathf.Ceil(label.preferredWidth) + 4, minWidth, PreviewWidth);
                if (row == null || occupied + gap + width > PreviewWidth)
                {
                    if (rowsAvailable == 0)
                    {
                        label.gameObject.SetActive(false);
                        Destroy(label.gameObject);
                        break;
                    }
                    row = Row(list, "PreviewSampleRow", SampleRowHeight, gap);
                    rowsAvailable--;
                    occupied = 0;
                }
                label.transform.SetParent(row, false);
                label.color = SampleColor;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 8;
                label.resizeTextMaxSize = label.fontSize;
                Measure(label.gameObject, width, SampleRowHeight);
                _sampleLabels.Add((label, sample));
                occupied += occupied == 0 ? width : gap + width;
            }
        }

        private bool HasChanges(bool raw)
        {
            NamingApi.Current.ActiveWorld.Cultures.TryGetValue(_culture.id, out var active);
            var draft = _instance.DraftCulture(_culture.id);
            string id = RuleKey(_type, _rule);
            draft.Rules.TryGetValue(id, out var rule);
            var appliedRule = active != null && active.Rules.TryGetValue(id, out var edit) ? edit : null;
            var activeCards = NativeOnomastics.Selection(NamingApi.Current.ActiveWorld, _culture, _type.Id);
            if ((activeCards?.Enabled == true) != raw || NamingJson.Write(rule) != NamingJson.Write(appliedRule)) return true;
            if (!raw) return false;
            if (NativeNamingTab.HasChanged(_culture.id, _type.Id)) return true;
            var copy = _card == null ? null : NamingJson.Copy(_card);
            if (copy != null) copy.DraftTemplate = null;
            return NamingJson.Write(copy) != NamingJson.Write(activeCards);
        }
    }
}
