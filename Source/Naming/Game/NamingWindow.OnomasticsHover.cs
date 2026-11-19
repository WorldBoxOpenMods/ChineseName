using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Chinese_Name.Naming.Game;

public sealed partial class NamingWindow
{
    public sealed partial class OnomasticsEditor
    {
        private OnomasticsAssetButton _hoveredWordGroup;
        private (int Index, string Group)? _highlightedWordGroup;
        private OnomasticsAssetButton[] _previewCards = Array.Empty<OnomasticsAssetButton>();
        private Dictionary<string, string> _previewGroupRoles;

        private void BindArrangementHover()
        {
            _previewCards = Cards();
            foreach (var card in _previewCards)
            {
                if (!card.isGroupType()) continue;
                var hover = card.GetComponent<WordGroupHover>() ?? card.gameObject.AddComponent<WordGroupHover>();
                hover.Bind(this, card);
            }
            _previewGroupRoles = null;
            if (NativeOnomastics.Meta(_type.Id) == null && !UsesCards(_card, _rule))
                NativeNamingArrangement.Default(_type.Id, _rule, out _previewGroupRoles);
        }

        private void UpdatePreviewHighlight(bool force = false)
        {
            (int Index, string Group)? target = null;
            var card = _hoveredWordGroup;
            if (Visible && CurrentContext && card != null && card.isActiveAndEnabled && card.isGroupType() &&
                card.transform.parent == _tab.parent_name_variation_1 && !Config.isDraggingItem())
            {
                int index = Array.IndexOf(_previewCards, card);
                if (index >= 0) target = (index, card.onomastics_asset.id);
            }
            if (!force && target == _highlightedWordGroup) return;
            _highlightedWordGroup = target;
            string rulePart = null;
            int position = -1;
            string[] branch = null;
            if (target is { } slot)
            {
                _previewGroupRoles?.TryGetValue(slot.Group, out rulePart);
                var layout = _previewCards.Select(item => item.onomastics_asset.id).ToArray();
                int start = Array.LastIndexOf(layout, "divider", slot.Index) + 1;
                int end = Array.IndexOf(layout, "divider", slot.Index);
                if (end < 0) end = layout.Length;
                branch = layout.Skip(start).Take(end - start).ToArray();
                position = slot.Index - start;
            }
            foreach (var (label, name) in _sampleLabels)
            {
                string highlighted = null;
                if (branch != null)
                {
                    if (name.CardBranch?.SequenceEqual(branch) == true)
                        highlighted = Highlight(name, position, target.Value.Group, null);
                    else if (name.CardBranch == null && rulePart != null)
                        highlighted = Highlight(name, -1, null, rulePart);
                }
                label.supportRichText = highlighted != null;
                label.text = highlighted ?? name.Text;
            }
        }

        private static string Highlight(NameResult name, int cardIndex, string group, string rulePart)
        {
            // Unity's legacy Text has no escape for literal rich-text tags.
            if (name.Text.IndexOf('<') >= 0) return null;
            var text = new StringBuilder();
            bool matched = false;
            int offset = 0;
            string separator = name.Separator ?? "";
            foreach (string id in name.PartOrder)
            {
                if (!name.Parts.TryGetValue(id, out string part) || string.IsNullOrEmpty(part)) continue;
                if (offset > 0)
                {
                    text.Append(separator);
                    offset += separator.Length;
                }
                if (offset + part.Length > name.Text.Length ||
                    string.CompareOrdinal(name.Text, offset, part, 0, part.Length) != 0) return null;
                name.Sources.TryGetValue(id, out var source);
                bool highlight = rulePart != null ? id == rulePart : source?.CardIndex == cardIndex && source.Group == group;
                if (highlight) text.Append("<color=#FFDC6C>");
                text.Append(part);
                if (highlight) text.Append("</color>");
                matched |= highlight;
                offset += part.Length;
            }
            return matched && offset == name.Text.Length ? text.ToString() : null;
        }

        public sealed class WordGroupHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            private OnomasticsEditor _editor;
            private OnomasticsAssetButton _card;

            internal void Bind(OnomasticsEditor editor, OnomasticsAssetButton card)
            {
                _editor = editor;
                _card = card;
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                if (_editor != null) _editor._hoveredWordGroup = _card;
            }
            public void OnPointerExit(PointerEventData eventData) => ClearHover();
            private void OnDisable() => ClearHover();

            private void ClearHover()
            {
                if (_editor != null && _editor._hoveredWordGroup == _card) _editor._hoveredWordGroup = null;
            }
        }
    }
}
