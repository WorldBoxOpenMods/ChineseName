using System;
using System.Linq;
using UnityEngine;
using static Chinese_Name.Naming.Game.NativeNamingSettings;

namespace Chinese_Name.Naming.Game;

internal sealed class NativePackagePage : MonoBehaviour
{
    private RectTransform _results;
    private KeyValueField _rescan;
    private KeyValueField _status;
    private string _query = "";
    private string _details;

    private void OnEnable()
    {
        string scanHint = "重新扫描命名包目录\n" + WorldNamingStore.PackageDirectory;
        if (_results == null)
        {
            Search(transform, "PackageSearch", "搜索命名包", query => { _query = query.Trim(); Refresh(); });
            _rescan = Row(transform, "RescanPackages", "命名包目录", "刷新", Rescan, "iconBooks", scanHint);
            _status = Row(transform, "PackageStatus", "", "");
            _results = Stack(transform, "PackageResults", spacing: 0);
        }
        Bind(_rescan, Rescan, scanHint);
        _status.gameObject.SetActive(false);
        Refresh();
    }

    private void Rescan()
    {
        var errors = WorldNamingStore.ReloadPacks();
        Status(errors.Count == 0 ? "命名包列表已刷新" : string.Join(" · ", errors));
        Refresh();
    }

    private void Refresh()
    {
        if (_results == null) return;
        Clear(_results);
        var service = NamingApi.Current;
        var world = service.ActiveWorld;
        var active = service.ActivePacks(world);
        foreach (var id in service.Packages.Concat(world.EnabledPacks).Distinct().OrderBy(key => key, StringComparer.Ordinal))
        {
            var pack = service.DescribePackage(id);
            string label = pack?.Label ?? id;
            if (!Matches(_query, label, id)) continue;
            bool required = id == service.DefaultPack || service.IsModDefault(id);
            bool enabled = world.EnabledPacks.Contains(id);
            bool available = active.ContainsKey(id);
            string state = required ? "内置" : pack == null ? "文件缺失" : !enabled ? "已关闭" :
                available ? "已启用" : "缺少依赖";
            string hint = pack == null || pack.Requires.Count == 0 ? id :
                id + "\n依赖 " + string.Join("、", pack.Requires.Keys.Select(key => service.DescribePackage(key)?.Label ?? key));
            string icon = required ? "iconMainInfo" : available ? "IconOn" : "IconOff";
            var row = Row(_results, "NamingPack." + id, label, state, required ? null : () => Toggle(id), icon,
                hint);
            if (required) row.value.color = Toolbox.color_grey;
            if (required || pack == null) continue;
            AddIconButton(row, "PackageOptions", "iconOptions", "优先级与依赖", _details == id,
                () => { _details = _details == id ? null : id; Refresh(); });
            if (_details == id)
            {
                int priority = world.PackPriorities.TryGetValue(id, out int assigned) ? assigned : pack.Priority;
                Row(_results, "PackagePriority." + id, "优先级 " + priority, "提高", () => Priority(id, priority + 1), tooltip: hint);
                Row(_results, "PackagePriorityLower." + id, "", "降低", () => Priority(id, priority - 1));
            }
        }
        if (!_results.Cast<Transform>().Any(item => item.gameObject.activeSelf))
            Row(_results, "NoPackages", "", "没有匹配的命名包");
    }

    private void Toggle(string id) => Edit(world =>
    {
        if (!world.EnabledPacks.Remove(id)) world.EnabledPacks.Add(id);
    });

    private void Priority(string id, int value) => Edit(world => world.PackPriorities[id] = Mathf.Clamp(value, -10000, 10000));

    private void Edit(Action<WorldNaming> edit)
    {
        var world = NamingApi.Current.GetWorld();
        edit(world);
        try
        {
            WorldNamingStore.ApplyPackageSelection(world);
            Status(null);
        }
        catch (InvalidOperationException error) { Status(error.Message); }
        Refresh();
    }

    private void Status(string message)
    {
        _status.name_text.text = message ?? "";
        _status.gameObject.SetActive(message != null);
    }
}
