using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using static Chinese_Name.Naming.Game.NativeNamingSettings;

namespace Chinese_Name.Naming.Game;

internal sealed class NativeSurnameEditor : MonoBehaviour
{
    private const string Header = "ChineseName.Surname";
    private UnitWindow _window;
    private Actor _actor;
    private CanvasGroup _nativeName;
    private Button _open;
    private GameObject _editStrip;
    private NameInput _input;

    [HarmonyPatch(typeof(UnitWindow), nameof(UnitWindow.showInfo))]
    internal static class Attach
    {
        private static void Postfix(UnitWindow __instance)
        {
            var attached = __instance.name_input.transform.parent.Find(Header);
            var editor = attached != null ? attached.GetComponent<NativeSurnameEditor>() : Create(__instance);
            editor.Refresh();
        }
    }

    private static NativeSurnameEditor Create(UnitWindow window)
    {
        var original = (RectTransform)window.name_input.transform;
        var node = new GameObject(Header, typeof(RectTransform));
        var rect = (RectTransform)node.transform;
        rect.SetParent(original.parent, false);
        rect.SetSiblingIndex(original.GetSiblingIndex() + 1);
        rect.anchorMin = original.anchorMin;
        rect.anchorMax = original.anchorMax;
        rect.pivot = original.pivot;
        rect.offsetMin = original.offsetMin;
        rect.offsetMax = original.offsetMax;

        var editor = node.AddComponent<NativeSurnameEditor>();
        editor._window = window;
        editor._open = IconButton(rect, "EditSurname", "iconFamilyList", "修改姓氏", "", editor.Begin);
        PlaceAtRight(editor._open, -10);

        var strip = new GameObject("SurnameEdit", typeof(RectTransform));
        var stripRect = (RectTransform)strip.transform;
        stripRect.SetParent(rect, false);
        stripRect.anchorMin = Vector2.zero;
        stripRect.anchorMax = Vector2.one;
        stripRect.offsetMin = stripRect.offsetMax = Vector2.zero;
        strip.SetActive(false);
        editor._editStrip = strip;

        var field = Field(stripRect, window.name_input, "SurnameInput", "", "");
        editor._input = field;
        var fieldRect = (RectTransform)field.transform;
        fieldRect.anchorMin = Vector2.zero;
        fieldRect.anchorMax = Vector2.one;
        fieldRect.offsetMin = Vector2.zero;
        fieldRect.offsetMax = new Vector2(-41, 0);
        field.inputField.characterLimit = 24;
        field.textField.alignment = TextAnchor.MiddleLeft;
        field.textField.rectTransform.offsetMin = new Vector2(29, 0);
        field.textField.rectTransform.offsetMax = new Vector2(-5, 2);

        var label = new GameObject("SurnameLabel", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(fieldRect, false);
        label.rectTransform.anchorMin = new Vector2(0, 0);
        label.rectTransform.anchorMax = new Vector2(0, 1);
        label.rectTransform.offsetMin = new Vector2(6, 0);
        label.rectTransform.offsetMax = new Vector2(28, 2);
        label.font = field.textField.font;
        label.fontSize = field.textField.fontSize;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = Toolbox.color_log_neutral;
        label.raycastTarget = false;
        label.text = "姓氏";

        PlaceAtRight(IconButton(stripRect, "ApplySurname", "IconOn", "确认", "留空可清除姓氏", editor.Apply), -30);
        PlaceAtRight(IconButton(stripRect, "CancelSurname", "IconOff", "取消", "", () => editor.SetEditing(false)), -10);
        // Deactivating the native NameInput would remove UnitWindow's rename listener.
        editor._nativeName = original.gameObject.AddComponent<CanvasGroup>();
        return editor;
    }

    private static void PlaceAtRight(Button button, float x)
    {
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(1, .5f);
        rect.anchoredPosition = new Vector2(x, 0);
    }

    private void OnDisable()
    {
        if (_nativeName != null) SetEditing(false);
        _actor = null;
    }

    private void Refresh()
    {
        var actor = _window.actor;
        if (_actor != actor) SetEditing(false);
        _actor = actor;
        bool eligible = actor != null && !actor.isRekt() && actor.isSapient();
        var original = (RectTransform)_window.name_input.transform;
        var rightEdge = ((RectTransform)transform).offsetMax;
        original.offsetMax = eligible ? rightEdge - new Vector2(21, 0) : rightEdge;
        gameObject.SetActive(eligible);
    }

    private void Begin()
    {
        if (_actor == null || _actor != _window.actor || _actor.isRekt()) return;
        _input.textField.color = _window.name_input.textField.color;
        _input.setText(VanillaNames.SurnameOf(_actor) ?? "");
        SetEditing(true);
        _input.inputField.ActivateInputField();
    }

    private void SetEditing(bool editing)
    {
        _nativeName.alpha = editing ? 0 : 1;
        _nativeName.interactable = _nativeName.blocksRaycasts = !editing;
        _open.gameObject.SetActive(!editing);
        _editStrip.SetActive(editing);
    }

    private void Apply()
    {
        if (_actor == null || _actor != _window.actor || _actor.isRekt()) return;
        try
        {
            VanillaNames.ChangeSurname(_actor, _input.inputField.text);
            _window._initial_name = _actor.getName().Trim();
            _window.name_input.setText(_window._initial_name);
            SetEditing(false);
        }
        catch (Exception issue) when (issue is ArgumentException or InvalidOperationException)
        {
            WorldTip.showNow(issue.Message, pTranslate: false, pPosition: "top", pColor: "#FF637D");
        }
    }
}
