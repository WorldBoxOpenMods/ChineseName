using System;
using System.Linq;
using UnityEngine;
using static Chinese_Name.Naming.Game.NativeNamingSettings;

namespace Chinese_Name.Naming.Game;

internal sealed class NativeLexiconPage : MonoBehaviour
{
    private Language _language;
    private RectTransform _results;
    private EffectiveNames _effective;
    private string[] _culturePools = Array.Empty<string>();
    private string _pool;
    private string _word;
    private bool _chooseSource;
    private bool _browseAll;
    private string _query = "";

    private void OnEnable()
    {
        _language = SelectedMetas.selected_language;
        _pool = _word = null;
        _query = "";
        _chooseSource = false;
        _browseAll = false;
        Render();
    }

    private void OnDisable()
    {
        _language = null;
        _effective = null;
    }

    private void LateUpdate()
    {
        if (_language != SelectedMetas.selected_language) OnEnable();
    }

    private Actor Speaker
    {
        get
        {
            var speaker = _language.units.FirstOrDefault(actor => actor.culture != null);
            if (speaker != null) return speaker;
            var creator = World.world.units.get(_language.data.creator_id);
            return creator?.language == _language && creator.culture != null ? creator : null;
        }
    }
    private long? Culture => Speaker?.culture.id;

    private void Render()
    {
        Clear(transform);
        if (_language == null) return;
        var service = NamingApi.Current;
        var world = service.ActiveWorld;
        world.Languages.TryGetValue(_language.id, out var settings);
        var speaker = Speaker;
        _effective = service.Inspect(new NameRequest(VanillaNames.Person, 0, speaker?.culture.id, _language.id), world);
        if (_chooseSource)
        {
            Row(transform, "BackToLexicons", "语言词库", "返回", () => ChooseSource(false));
            Row(transform, "LanguageSource.Auto", "自动", "选择", () => SetSource(null));
            foreach (var source in service.ActivePacks(world).Values.Where(pack => !pack.Supplemental)
                         .OrderBy(pack => pack.Label ?? pack.Id, StringComparer.Ordinal))
                Row(transform, "LanguageSource." + source.Id, source.Label ?? source.Id, "选择", () => SetSource(source.Id));
            return;
        }

        if (_pool == null)
        {
            _culturePools = CommonPools(service, speaker);
            string source = settings?.Pack ?? world.LanguagePack;
            Row(transform, "LanguageSource", "词库来源", source == null ? "自动" : service.DescribePackage(source)?.Label ?? source,
                () => ChooseSource(true), "iconLanguage");
            if (world.LanguageParents.TryGetValue(_language.id, out long parent))
                Row(transform, "ParentLanguage", "源语言", World.world.languages.get(parent)?.name ?? parent.ToString(), icon: "iconLanguage");
        }
        else
        {
            Row(transform, "BackToLexicons", PoolLabel(_pool), "返回", () => OpenPool(null));
            RenderPoolSettings(service, world, settings);
        }

        var search = Search(transform, "LexiconSearch", _pool == null ? "搜索词库" : "搜索词条", query =>
        {
            _query = query.Trim();
            Filter();
        });
        search.inputField.SetTextWithoutNotify(_query);
        _results = Stack(transform, "LexiconResults", spacing: 0);
        Filter();
        if (_pool == null && settings?.Pools.Count > 0)
            Row(transform, "ToggleLexiconExport", "命名包", "导出", ToggleExport, "iconBooks");
    }

    private void Filter()
    {
        Clear(_results);
        var state = NamingApi.Current.ActiveWorld;
        state.Languages.TryGetValue(_language.id, out var settings);
        if (_pool == null) RenderPools(settings);
        else RenderWords(settings);
    }

    private string PoolLabel(string id) => _effective.PoolLabels.TryGetValue(id, out string label) ? label : id;

    private string[] CommonPools(NamingService service, Actor speaker)
    {
        if (speaker == null) return Array.Empty<string>();
        var everyday = new[] { VanillaNames.Person, NativeOnomastics.City, "worldbox:kingdom" }
            .SelectMany(type => service.Preview(VanillaNames.Request(type, speaker, 0)).Sources.Values)
            .Select(source => source.Reference).Where(id => _effective.PoolIds.Contains(id));
        var culture = NativeOnomastics.CulturePools(service, speaker).OrderBy(PoolLabel, StringComparer.Ordinal);
        return everyday.Concat(culture).Distinct(StringComparer.Ordinal).ToArray();
    }

    private void RenderPools(LanguageNaming settings)
    {
        var edited = settings == null ? Enumerable.Empty<string>() : settings.Pools.Keys;
        var preferred = settings == null ? Enumerable.Empty<string>() : settings.Groups.Keys.Concat(settings.GroupPreferences.Keys);
        bool browseAll = _browseAll || _query.Length > 0;
        var all = edited.Concat(_culturePools).Concat(preferred).Concat(_effective.PoolIds).Distinct(StringComparer.Ordinal);
        var ids = browseAll ? all.Where(id => Matches(_query, PoolLabel(id), id)).ToArray() : all.Take(12).ToArray();
        if (_browseAll && _query.Length == 0)
            Row(_results, "CommonPools", "常用词库", "返回", () => BrowseAll(false));
        foreach (var id in ids.Take(60))
        {
            var preferences = settings?.PreferencesFor(id).OrderByDescending(pair => pair.Value).ToArray();
            string hint = preferences == null || preferences.Length == 0 ? id :
                string.Join("\n", preferences.Select(pair => GroupName(pair.Key) + " " + pair.Value.ToString("P0")));
            Row(_results, "LexiconPool." + id, PoolLabel(id), "查看", () => OpenPool(id), tooltip: hint);
        }
        if (ids.Length == 0) Row(_results, "NoPools", "", "没有匹配的词库");
        else if (ids.Length > 60) Row(_results, "MorePools", "", "搜索查看更多词库");
        else if (!browseAll && all.Skip(ids.Length).Any())
            Row(_results, "AllPools", "全部词库", "浏览", () => BrowseAll(true), "iconBooks");
    }

    private void RenderPoolSettings(NamingService service, WorldNaming world, LanguageNaming settings)
    {
        var groupChoices = service.WordGroups(world, settings, Culture);
        PoolEdit edit = null;
        settings?.Pools.TryGetValue(_pool, out edit);
        string group = edit?.Group;
        var preferred = settings?.PreferencesFor(_pool).OrderByDescending(pair => pair.Value).FirstOrDefault();
        if (preferred.HasValue && preferred.Value.Key != null)
            Row(transform, "LexiconPreference", "词群偏好", GroupName(preferred.Value.Key, edit) + " · " + Percent(preferred.Value.Value),
                tooltip: preferred.Value.Key);
        if (groupChoices.TryGetValue(_pool, out var choices) || group != null)
        {
            var options = new[] { (string)null }.Concat(choices ?? Array.Empty<string>()).Append(group).Distinct().ToArray();
            Row(transform, "LexiconGroup", "限定词群", group == null ? "自动" : GroupName(group, edit),
                () => EditPool(pool => pool.Group = options[(Array.IndexOf(options, group) + 1) % options.Length]),
                tooltip: "点击切换词群");
        }
        if (edit != null)
            Row(transform, "ResetLexiconPool", "手动调整", "重置", () => Apply(profile => profile.Pools.Remove(_pool)));
    }

    private void RenderWords(LanguageNaming settings)
    {
        PoolEdit edit = null;
        settings?.Pools.TryGetValue(_pool, out edit);
        var words = _effective.GetPool(_pool);
        double total = words.Sum(word => word.Weight);
        foreach (var word in words.Where(word => Matches(_query, word.Chinese ?? word.Text, word.Id) ||
                     Matches(_query, word.Text, word.Id)).Take(60))
        {
            string text = word.Chinese ?? word.Text;
            Row(_results, "LexiconWord." + word.Id, text, total > 0 ? Percent(word.Weight / total) : "调整",
                () => { _word = _word == word.Id ? null : word.Id; Filter(); }, tooltip: "点击调整词条");
            if (_word != word.Id) continue;
            string id = word.Id;
            Row(_results, "IncreaseWord." + id, "抽取权重", "提高",
                () => EditPool(pool => pool.Bias[id] = Math.Min(10000, pool.Bias.TryGetValue(id, out double bias) ? bias * 2 : 2)));
            Row(_results, "ExcludeWord." + id, "", "禁用此词", () =>
            {
                _word = null;
                EditPool(pool => pool.ExcludeIds.Add(id));
            });
        }
        if (edit == null) return;
        foreach (var excluded in edit.ExcludeText.Concat(edit.ExcludeIds).Distinct(StringComparer.Ordinal))
        {
            string text = DisplayWord(excluded);
            if (!Matches(_query, text, excluded)) continue;
            Row(_results, "RestoreWord." + excluded, text, "恢复", () => EditPool(pool =>
            {
                pool.ExcludeIds.Remove(excluded);
                pool.ExcludeText.Remove(excluded);
            }));
        }
    }

    private string DisplayWord(string id)
    {
        var service = NamingApi.Current;
        var word = service.ActivePacks(service.ActiveWorld).Values.SelectMany(pack => pack.Words).FirstOrDefault(word => word.Id == id);
        return word?.Chinese ?? word?.Text ?? id;
    }

    private static string GroupName(string id, PoolEdit edit = null)
    {
        int separator = id.LastIndexOf('/');
        string key = id.Substring(separator + 1);
        if (separator < 0) return key;
        var pack = NamingApi.Current.DescribePackage(id.Substring(0, separator));
        if (pack == null || !pack.WordGroups.TryGetValue(key, out var group)) return key;
        var examples = pack.Words.Where(word => group.Words.Contains(word.Id))
            .Where(word => edit == null || !edit.ExcludeIds.Contains(word.Id) && !edit.ExcludeText.Contains(word.Text) &&
                !edit.ExcludeText.Contains(word.Chinese))
            .Select(word => word.Chinese ?? word.Text).Where(text => text.Length <= 5).Distinct().Take(2).ToArray();
        return examples.Length == 0 ? key : string.Join("、", examples) + (group.Words.Count > examples.Length ? "…" : "");
    }

    private static string Percent(double value) => (value * 100).ToString("0.#") + "%";

    private void ToggleExport()
    {
        var current = transform.Find("ExportNamingPack");
        if (current != null)
        {
            current.gameObject.SetActive(false);
            Destroy(current.gameObject);
            return;
        }
        Export(transform, "local:language-" + _language.id,
            id => WorldNamingStore.ExportEdits(id, null, _language.id));
    }

    private void BrowseAll(bool open)
    {
        _browseAll = open;
        Filter();
        ScrollWindow.getCurrentWindow().resetScroll();
    }

    private void ChooseSource(bool open)
    {
        _chooseSource = open;
        Render();
        ScrollWindow.getCurrentWindow().resetScroll();
    }

    private void SetSource(string id)
    {
        _chooseSource = false;
        Apply(profile => profile.Pack = id);
        ScrollWindow.getCurrentWindow().resetScroll();
    }

    private void OpenPool(string id)
    {
        _pool = id;
        _word = null;
        _query = "";
        Render();
        ScrollWindow.getCurrentWindow().resetScroll();
    }

    private void EditPool(Action<PoolEdit> edit) => Apply(profile =>
    {
        if (!profile.Pools.TryGetValue(_pool, out var pool)) profile.Pools[_pool] = pool = new PoolEdit();
        edit(pool);
        if (pool.Group == null && pool.Bias.Count == 0 && pool.ExcludeIds.Count == 0 && pool.ExcludeText.Count == 0)
            profile.Pools.Remove(_pool);
    });

    private void Apply(Action<LanguageNaming> edit)
    {
        if (_language == null || SelectedMetas.selected_language != _language) return;
        try { WorldNamingStore.ApplyLanguageSettings(_language.id, edit); }
        catch (InvalidOperationException error)
        {
            Row(transform, "LanguageEditError", error.Message, "");
            return;
        }
        Render();
    }
}
