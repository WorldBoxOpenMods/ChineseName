using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static Chinese_Name.Naming.Game.NativeNamingSettings;

namespace Chinese_Name.Naming.Game;

internal sealed class NativePackagePage : MonoBehaviour
{
    private const string ReassignmentHint = "为整个世界重新分配文化命名预设。可以同时重取已有对象的名字。";
    private RectTransform _results;
    private KeyValueField _rescan;
    private KeyValueField _status;
    private KeyValueField _reassign;
    private RectTransform _reassignment;
    private string _query = "";
    private string _details;

    private void OnEnable()
    {
        string scanHint = "重新扫描命名包目录\n" + WorldNamingStore.PackageDirectory;
        if (_results == null)
        {
            _reassign = Row(transform, "WorldNamingReassignment", "全局命名", "重配", ToggleReassignment,
                "iconCulture", ReassignmentHint);
            _reassignment = Stack(transform, "WorldNamingReassignmentOptions");
            Search(transform, "PackageSearch", "搜索命名包", query => { _query = query.Trim(); Refresh(); });
            _rescan = Row(transform, "RescanPackages", "命名包目录", "刷新", Rescan, "iconBooks", scanHint);
            _status = Row(transform, "PackageStatus", "", "");
            _results = Stack(transform, "PackageResults", spacing: 0);
        }
        Bind(_reassign, ToggleReassignment, ReassignmentHint);
        Bind(_rescan, Rescan, scanHint);
        var storage = WorldNamingStore.StorageWarnings();
        Status(storage.Count == 0 ? null : "有 " + storage.Count + " 条命名设置待恢复", storage.Count == 0 ? null : string.Join("\n", storage.Take(10)));
        Refresh();
    }

    private void OnDisable()
    {
        if (_reassignment != null) Clear(_reassignment);
    }

    private void ToggleReassignment()
    {
        if (_reassignment.Cast<Transform>().Any(item => item.gameObject.activeSelf)) CloseReassignment();
        else ShowReassignmentChoices();
    }

    private void ShowReassignmentChoices()
    {
        Clear(_reassignment);
        Status(null);
        var map = World.world.map_stats;
        var service = NamingApi.Current;
        var packs = service.ActivePacks(service.ActiveWorld).Values
            .Where(pack => !pack.Supplemental && pack.Id != service.DefaultPack)
            .OrderBy(pack => pack.Label ?? pack.Id, StringComparer.Ordinal).ToArray();
        var choices = Stack(_reassignment, "WorldNamingReassignmentChoices");
        void Show(string query)
        {
            Clear(choices);
            if (string.IsNullOrWhiteSpace(query) || Matches(query, "随物种", ""))
                Row(choices, "WorldNamingSpecies", "随物种", "选用", () => ConfirmReassignment(map, null, "随物种"));
            foreach (var pack in packs.Where(pack => Matches(query, pack.Label ?? pack.Id, pack.Id)))
                Row(choices, "WorldNamingStyle." + pack.Id, pack.Label ?? pack.Id, "选用",
                    () => ConfirmReassignment(map, pack.Id, pack.Label ?? pack.Id));
            if (!choices.Cast<Transform>().Any(item => item.gameObject.activeSelf))
                Row(choices, "WorldNamingNoStyle", "没有匹配的预设", "");
            RebuildLayout();
        }
        var search = Search(_reassignment, "WorldNamingStyleSearch", "搜索预设", query => Show(query.Trim()));
        search.transform.SetSiblingIndex(choices.GetSiblingIndex());
        Show("");
    }

    private void ConfirmReassignment(MapStats map, string pack, string label, bool renameWorld = false, bool repairOnly = false)
    {
        Clear(_reassignment);
        Row(_reassignment, "WorldNamingRules", label, "仅重配规则", ApplyRules,
            tooltip: "重置本世界各文化的原版类别排列、规则和预设，保留各语言的用词设置。");
        var worldTitle = Row(_reassignment, "WorldNamingTitles", "世界与造物主", "保留");
        Bind(worldTitle, ToggleTitles, "开启后会重取这两个名称，原版存档没有记录它们是否经过手动编辑。");
        var scope = Row(_reassignment, "WorldNamingScope", "重取范围", "全部自动命名");
        Bind(scope, ToggleScope, "可只修英文名和空白名，同时保护手改名。所选预设仍会用于重配文化规则。");
        Row(_reassignment, "WorldNamingObjects", "已有对象", "预演重取", Preview,
            tooltip: "先看国家和联盟的新名字。确认后重配规则并重取所选范围内的自动名称。");
        Row(_reassignment, "WorldNamingCancel", "", "取消", CloseReassignment);
        UpdateScope();
        RebuildLayout();

        void ToggleTitles()
        {
            renameWorld = !renameWorld;
            SetActionText(worldTitle, renameWorld ? "重取" : "保留");
        }

        void ToggleScope()
        {
            repairOnly = !repairOnly;
            UpdateScope();
            RebuildLayout();
        }

        void UpdateScope()
        {
            SetActionText(scope, repairOnly ? "仅英文和空白" : "全部自动命名");
            SetActionText(worldTitle, renameWorld ? "重取" : "保留");
            worldTitle.gameObject.SetActive(!repairOnly);
            if (!repairOnly)
                Bind(worldTitle, ToggleTitles, "开启后会重取这两个名称，原版存档没有记录它们是否经过手动编辑。");
        }

        void ApplyRules()
        {
            CloseReassignment();
            if (!ReferenceEquals(World.world.map_stats, map)) return;
            try
            {
                WorldNamingReassignment.ApplyStyle(pack);
                Status("已重配文化命名规则");
            }
            catch (InvalidOperationException error) { Status(error.Message); }
            RebuildLayout();
        }

        void Preview()
        {
            if (!ReferenceEquals(World.world.map_stats, map)) { CloseReassignment(); return; }
            try
            {
                var plan = new WorldNamingReassignment.Plan(pack, renameWorld, repairOnly);
                Clear(_reassignment);
                var countries = plan.Countries.ToArray();
                var alliances = plan.Alliances.ToArray();
                var cities = plan.Cities.ToArray();
                var actors = plan.Actors.ToArray();
                var wars = plan.Wars.ToArray();
                Row(_reassignment, "WorldNamingPreviewSummary", "国家 " + countries.Length + " · 联盟 " + alliances.Length,
                    "确认重取", Commit, tooltip: "确认后应用本次预演的全部结果。");
                Row(_reassignment, "WorldNamingPreviewSubjects", "城市 " + cities.Length + " · 生物 " + actors.Length + " · 战争 " + wars.Length,
                    plan.Others > 0 ? "其他 " + plan.Others : "");
                Row(_reassignment, "WorldNamingPreviewProtected", "保留手改名 " + plan.Manual, "未取到新名 " + plan.Failed);
                for (int i = 0; i < Math.Min(3, countries.Length); i++)
                {
                    var country = countries[i];
                    Example("WorldNamingPreviewKingdom." + i, country.Before, country.Name + "（" + country.Short + "）");
                }
                for (int i = 0; i < Math.Min(3, alliances.Length); i++)
                    Example("WorldNamingPreviewAlliance." + i, alliances[i].Before, alliances[i].Name);
                ShowExample("City", cities);
                ShowExample("Actor", actors);
                ShowExample("War", wars);
                Row(_reassignment, "WorldNamingPreviewBack", "", "返回", () => ConfirmReassignment(map, pack, label, renameWorld, repairOnly));
                RebuildLayout();

                void ShowExample(string type, (string Before, string Name)[] changes)
                {
                    if (changes.Length == 0) return;
                    var (before, name) = changes[0];
                    Example("WorldNamingPreview" + type + ".0", before, name);
                }

                void Commit()
                {
                    try
                    {
                        var result = plan.Apply();
                        CloseReassignment();
                        if (renameWorld && !repairOnly) GetComponentInParent<WorldWindow>()?.nameInput.setText(map.name);
                        Status("已改名 " + result.Changed + (result.Failed == 0 ? "" : " · 未取到新名 " + result.Failed));
                    }
                    catch (InvalidOperationException error)
                    {
                        ConfirmReassignment(map, pack, label, renameWorld, repairOnly);
                        Status(error.Message);
                    }
                    RebuildLayout();
                }
            }
            catch (InvalidOperationException error) { Status(error.Message); }
        }

        void Example(string id, string before, string after)
        {
            var row = Row(_reassignment, id, string.IsNullOrWhiteSpace(before) ? "（空白）" : before, after);
            row.name_text.supportRichText = row.value.supportRichText = false;
        }
    }

    private void CloseReassignment()
    {
        Clear(_reassignment);
        RebuildLayout();
    }

    private void RebuildLayout()
    {
        var scroll = GetComponentInParent<ScrollRect>();
        if (scroll != null) LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
    }

    private void Rescan()
    {
        var errors = WorldNamingStore.ReloadPacks();
        var warnings = WorldNamingStore.StorageWarnings().Concat(NamingPackReferences.Inspect(NamingApi.Current)).ToArray();
        Status(errors.Count == 0 ? warnings.Length == 0 ? "命名包列表已刷新" : "命名包已刷新 · 引用提示 " + warnings.Length + " 项"
            : string.Join(" · ", errors), warnings.Length == 0 ? null : string.Join("\n", warnings.Take(10)));
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

    private void Status(string message, string details = null)
    {
        _status.name_text.text = message ?? "";
        _status.on_hover_value = null;
        if (details != null) Bind(_status, null, details);
        _status.gameObject.SetActive(message != null);
    }
}
