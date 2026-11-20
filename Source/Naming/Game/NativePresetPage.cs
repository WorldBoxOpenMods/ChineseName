using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static Chinese_Name.Naming.Game.NativeNamingSettings;

namespace Chinese_Name.Naming.Game;

internal sealed class NativePresetPage : MonoBehaviour
{
    private RectTransform _confirm;
    private RectTransform _results;
    private NameInput _search;
    private string _query;
    private string _managed;

    private void OnEnable()
    {
        Clear(transform);
        _query = "";
        _managed = null;
        _search = Search(transform, "PlayerPresetSearch", "搜索预设", query => { _query = query.Trim(); Render(); });
        Row(transform, "ImportPlayerPreset", "剪贴板", "导入", BeginImport, "iconBooks");
        _confirm = Stack(transform, "PlayerPresetImportConfirm");
        _results = Stack(transform, "PlayerPresetResults", spacing: 0);
        Render();
    }

    private void BeginImport()
    {
        Clear(_confirm);
        if (_managed != null)
        {
            _managed = null;
            Render();
        }
        Try(() =>
        {
            var preset = PlayerPresetLibrary.Parse(GUIUtility.systemCopyBuffer);
            var row = Row(_confirm, "ConfirmPlayerPreset", preset.Snapshot.Label, "确认导入", () => Try(() =>
            {
                var imported = PlayerPresetLibrary.Import(preset);
                Clear(_confirm);
                FilterTo(imported.Snapshot.Label);
                Status("预设已入库 · " + imported.Snapshot.Label);
            }));
            AddIconButton(row, "CancelPlayerPreset", "IconOff", "取消", false, () =>
            {
                Clear(_confirm);
                RefreshLayout();
            });
            var missing = PlayerPresetLibrary.Missing(preset);
            if (missing.Length > 0)
                Row(_confirm, "PlayerPresetMissing", "缺少 " + string.Join("、", missing), "");
        });
        RefreshLayout();
    }

    private void Render()
    {
        Clear(_results);
        Try(() =>
        {
            var entries = PlayerPresetLibrary.Entries.Where(item =>
                Matches(_query, item.Snapshot.Label, item.Snapshot.Id)).ToArray();
            foreach (var preset in entries)
            {
                string id = preset.Snapshot.Id;
                var row = Row(_results, "ExportPlayerPreset." + id, preset.Snapshot.Label, "导出", () => Try(() =>
                {
                    PlayerPresetLibrary.Export(preset);
                    Status("已导出并复制 · " + preset.Snapshot.Label);
                }), tooltip: PlayerPresetLibrary.ExportDirectory);
                AddIconButton(row, "ManagePlayerPreset." + id, "iconOptions", "重命名或删除", _managed == id, () =>
                {
                    Clear(_confirm);
                    _managed = _managed == id ? null : id;
                    Render();
                });
                if (_managed == id) ShowActions(Stack(_results, "ManagePlayerPresetDetails"), preset);
            }
            if (entries.Length == 0)
                Row(_results, "NoPlayerPreset", _query.Length == 0 ? "还没有预设" : "没有匹配的预设", "");
        });
        RefreshLayout();
    }

    private void ShowActions(RectTransform panel, PlayerNamingPreset preset)
    {
        Clear(panel);
        var actions = ActionRow(panel, "PlayerPresetActions");
        var rename = LabelButton(actions, "RenamePlayerPreset", "重命名", false, () => BeginRename(panel, preset));
        rename.GetComponent<LayoutElement>().preferredWidth = 44;
        var delete = LabelButton(actions, "DeletePlayerPreset", "删除", false, () => BeginDelete(panel, preset));
        delete.GetComponent<LayoutElement>().preferredWidth = 36;
        RefreshLayout();
    }

    private void BeginRename(RectTransform panel, PlayerNamingPreset preset)
    {
        Clear(panel);
        var form = ActionRow(panel, "PlayerPresetRenameForm");
        var input = Field(form, SettingsInput(), "RenamePlayerPresetInput", "预设名称", preset.Snapshot.Label);
        input.inputField.characterLimit = PlayerPresetLibrary.NameLimit;
        var size = input.GetComponent<LayoutElement>() ?? input.gameObject.AddComponent<LayoutElement>();
        size.minWidth = size.preferredWidth = 0;
        size.flexibleWidth = 1;
        IconButton(form, "ConfirmRenamePlayerPreset", "IconOn", "确认重命名", "", () => Try(() =>
        {
            var renamed = PlayerPresetLibrary.Rename(preset.Snapshot.Id, input.inputField.text);
            _managed = null;
            if (Matches(_query, renamed.Snapshot.Label, renamed.Snapshot.Id)) Render();
            else FilterTo(renamed.Snapshot.Label);
            Status("已重命名 · " + renamed.Snapshot.Label);
        }));
        IconButton(form, "CancelRenamePlayerPreset", "IconOff", "取消", "", () => ShowActions(panel, preset));
        RefreshLayout();
        input.inputField.ActivateInputField();
    }

    private void BeginDelete(RectTransform panel, PlayerNamingPreset preset)
    {
        Clear(panel);
        var row = Row(panel, "ConfirmDeletePlayerPreset", "从玩家库删除？", "删除", () => Try(() =>
        {
            PlayerPresetLibrary.Delete(preset.Snapshot.Id);
            _managed = null;
            Render();
            Status("已从玩家库删除 · " + preset.Snapshot.Label);
        }), tooltip: "已应用的存档会保留自己的副本。");
        AddIconButton(row, "CancelDeletePlayerPreset", "IconOff", "取消", false, () => ShowActions(panel, preset));
        RefreshLayout();
    }

    private static RectTransform ActionRow(Transform parent, string name)
    {
        var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().preferredHeight = 16;
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        layout.spacing = 4;
        layout.padding.right = 6;
        return (RectTransform)row.transform;
    }

    private void FilterTo(string label)
    {
        if (_search.inputField.text == label) Render();
        else _search.setText(label);
    }

    private void RefreshLayout() => LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponentInParent<ScrollRect>().content);

    private static void Try(Action action)
    {
        try { action(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException or InvalidOperationException)
        {
            Status(error.Message, "#FF637D");
        }
    }

    private static void Status(string message, string color = "#F3961F") =>
        WorldTip.showNow(message, pTranslate: false, pPosition: "top", pColor: color);
}
