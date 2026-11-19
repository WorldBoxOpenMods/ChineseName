using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NativeInput = UnityEngine.UI.InputField;

namespace Chinese_Name.Naming.Game;

public sealed partial class NamingWindow
{
    /// <summary>在文化档案原版命名学中显示当前类型的草稿。</summary>
    public sealed partial class OnomasticsEditor : MonoBehaviour
    {
        private const float Width = 215;
        private static OnomasticsEditor _active;
        private OnomasticsTab _tab;
        private RectTransform _preview;
        private MapStats _map;
        private Culture _culture;
        private NameType _type;
        private PartsPage _page;
        private Rule _rule;
        private CardNaming _card;
        private bool _dirty;
        private bool _commitPending;
        private bool _integrated;

        internal static bool ConsumesKeyboard => _active != null && _active.Visible &&
            EventSystem.current?.currentSelectedGameObject is { } selected && selected != null &&
            ((selected.GetComponent<NativeInput>() is { isFocused: true } && _active.Owns(selected.transform)) ||
            (Input.GetKeyDown(KeyCode.Tab) && _active.Owns(selected.transform)));

        private bool Visible => isActiveAndEnabled && _integrated && _type != null && ScrollWindow.isCurrentWindow("culture");
        private bool Owns(Transform target) => target.IsChildOf(transform) || target.IsChildOf(_preview);

        internal void Attach(OnomasticsTab tab)
        {
            if (_tab != null) return;
            _tab = tab;
            _palette = tab.parent_asset_specials.GetComponentsInChildren<OnomasticsAssetButton>(true);
            AttachPalette();

            var groupEditorTitle = tab.GetComponentsInChildren<LocalizedText>(true)
                .FirstOrDefault(label => label.key == "group_editor");
            if (groupEditorTitle != null) groupEditorTitle.gameObject.SetActive(false);

            _preview = Stack(tab.text_name_examples.transform.parent, "ChineseName.Examples");
            _preview.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Inset(_preview, 5, 2);
            _preview.GetComponent<VerticalLayoutGroup>().spacing = 0;
            var shade = _preview.gameObject.AddComponent<Image>();
            shade.color = new Color(.08f, .075f, .06f, .33f);
            shade.raycastTarget = false;

            _preview.gameObject.SetActive(false);
        }

        internal void NativeChanged()
        {
            if (!CurrentContext || _loadingNative) return;
            _page.PreviewRevision++;
            _dirty = true;
            _commitPending |= _integrated;
            _page.Message = null;
        }

        private bool CurrentContext => _page != null && _culture == SelectedMetas.selected_culture &&
            _type.Id == NativeNamingTab.CurrentType(_tab);

        private void OnDisable()
        {
            if (_active == this) _active = null;
            _culture = null;
            _page = null;
            _type = null;
            if (_tab != null) SetIntegrated(false);
        }

        private void LateUpdate()
        {
            if (_tab == null || _instance == null || !ScrollWindow.isCurrentWindow("culture") || SmoothLoader.isLoading()) return;
            string id = NativeNamingTab.CurrentType(_tab);
            var culture = SelectedMetas.selected_culture;
            var map = World.world?.map_stats;
            if (id == null || culture == null || map == null)
            {
                _type = null;
                _page = null;
                SetIntegrated(false);
                return;
            }
            if (!ReferenceEquals(map, _map) || _culture != culture || _type?.Id != id)
            {
                _map = map;
                _culture = culture;
                _instance.SetOnomasticsContext(culture, id);
                _type = NamingApi.Current.Types.First(type => type.Id == id);
                _page = _instance.Page(culture, _type);
                _placementBranch = null;
                _commitPending = false;
                _page.PreviewRevision++;
                _dirty = true;
            }
            _active = this;
            if (Input.GetKeyDown(KeyCode.Tab) && EventSystem.current?.currentSelectedGameObject is { } focused && focused != null && Owns(focused.transform))
                MoveFocus(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            // 文字输入会在鼠标按下时失焦。保留鼠标按下的按钮，等松开后再更新界面。
            if (_dirty && !Input.GetMouseButton(0)) Render();
            FitPalette();
            UpdatePreviewHighlight();
        }

        private void SetIntegrated(bool integrated)
        {
            if (_integrated == integrated) return;
            _integrated = integrated;
            _preview.gameObject.SetActive(integrated);
            _tab.text_name_examples.enabled = !integrated;
            if (!integrated)
            {
                _hoveredWordGroup = null;
                FitPalette();
            }
            UpdatePalette();
        }

        private void RefreshRule()
        {
            var service = NamingApi.Current;
            _rule = _instance.SelectedRule(service, _type, _culture, _page);
            _card = _rule == null ? null : ReadArrangementChoice();

        }

        private void Render()
        {
            _dirty = false;
            if (_page.ReferenceLanguage is long language && World.world.languages.get(language) == null)
            {
                _page.ReferenceLanguage = null;
                _page.PreviewRevision++;
            }
            RefreshRule();
            bool raw = UsesCards(_card, _rule);
            if (!raw && NativeNamingArrangement.Default(_type.Id, _rule) == null)
            {
                SetIntegrated(false);
                return;
            }
            SetIntegrated(true);
            SyncArrangement();
            NativeNamingTab.ShowBoundGroups(_culture.id, _type.Id,
                raw ? _card : NativeNamingArrangement.ForRule(_type.Id, _rule));
            _instance.RefreshPartPreview(NamingApi.Current, _type, _culture, _page, _rule, _card);
            UpdatePalette();
            if (_commitPending)
            {
                _commitPending = false;
                if (HasChanges(raw) && _page.Result?.Success == true && !_page.Result.Diagnostics.Contains("中文规则回退"))
                {
                    try
                    {
                        _instance.ApplyParts(_type, _culture, _page, _rule, _card);
                        NativeNamingTab.RememberDefault(_culture.id, _type.Id, _tab.getOnomasticsData());
                    }
                    catch (InvalidOperationException error) { _page.Message = error.Message; }
                }
            }

            var selected = EventSystem.current?.currentSelectedGameObject;
            string focusedName = selected != null && selected.transform.IsChildOf(_preview) ? selected.name : null;
            Clear(_preview);
            BindArrangementHover();
            RenderPreview();
            UpdatePreviewHighlight(force: true);
            if (focusedName != null) Selectables().FirstOrDefault(item => item.name == focusedName)?.Select();
        }

        private void Change(Action change, bool commit = false)
        {
            _page.Message = null;
            try { change(); }
            catch (InvalidOperationException error) { _page.Message = error.Message; }
            _page.PreviewRevision++;
            _dirty = true;
            _commitPending |= commit;
        }

        private IEnumerable<Selectable> Selectables() => _preview.GetComponentsInChildren<Selectable>()
            .Concat(GetComponentsInChildren<Selectable>()).Where(control => control.isActiveAndEnabled && control.IsInteractable());

        private void MoveFocus(bool backwards)
        {
            var controls = Selectables().ToArray();
            if (controls.Length == 0) return;
            int current = Array.FindIndex(controls, control => control.gameObject == EventSystem.current.currentSelectedGameObject);
            int target = current < 0 ? (backwards ? controls.Length - 1 : 0) : (current + (backwards ? -1 : 1) + controls.Length) % controls.Length;
            var control = controls[target];
            control.Select();
            if (control is NativeInput field) field.ActivateInputField();
            if (control.transform.IsChildOf(_preview)) RevealLanguageFlag(control.transform);
            RevealInScroll(control.transform);
        }

        private static void RevealLanguageFlag(Transform target)
        {
            var scroll = target.GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.name != "PreviewLanguageScroll") return;
            var viewport = scroll.viewport;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, target);
            float overflow = scroll.content.rect.width - viewport.rect.width;
            if (overflow <= 0) return;
            float shift = bounds.min.x < viewport.rect.xMin ? bounds.min.x - viewport.rect.xMin :
                Mathf.Max(0, bounds.max.x - viewport.rect.xMax);
            if (Mathf.Abs(shift) > .01f)
                scroll.horizontalNormalizedPosition = Mathf.Clamp01(scroll.horizontalNormalizedPosition + shift / overflow);
        }

        private static void RevealInScroll(Transform target, float margin = 0)
        {
            var shell = ScrollWindow.getCurrentWindow();
            if (!target.IsChildOf(shell.transform_content)) return;
            var viewport = shell.transform_viewport;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, target);
            float range = shell.transform_content.rect.height - viewport.rect.height;
            if (range <= 0) return;
            float delta = bounds.size.y + margin * 2 > viewport.rect.height ? bounds.max.y + margin - viewport.rect.yMax :
                bounds.min.y - margin < viewport.rect.yMin ? bounds.min.y - margin - viewport.rect.yMin :
                Mathf.Max(0, bounds.max.y + margin - viewport.rect.yMax);
            if (Mathf.Abs(delta) < .01f) return;
            shell.scrollRect.StopMovement();
            shell.scrollRect.verticalNormalizedPosition = Mathf.Clamp01(shell.scrollRect.verticalNormalizedPosition + delta / range);
        }

    }
}
