using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using NeoModLoader.services;

namespace Chinese_Name.Naming.Game;

public static class WorldNamingStore
{
    private const string SaveKey = "chinesename.rules.v1";
    private const string DraftKey = "chinesename.draft.v1";
    private const string CulturePrefix = "chinesename.culture.v1/";
    private const string LanguagePrefix = "chinesename.language.v1/";

    private sealed class SavedLanguage
    {
        public LanguageNaming Settings { get; set; }
        public long? Parent { get; set; }
    }

    private static NamingService _service;
    private static MapStats _map;
    private static string _packDirectory;
    private static string _bundledPackDirectory;
    private static string _loadError;
    private static readonly HashSet<long> UnavailableCultures = new();
    private static readonly HashSet<long> UnavailableLanguages = new();
    private static readonly List<string> LoadIssues = new();
    private static string _bundledSpeciesPack;
    private static Dictionary<string, (string Path, NamingPack Pack)> _installedFiles = new(StringComparer.Ordinal);
    private static readonly HashSet<string> BundledPacks = new(StringComparer.Ordinal);

    public static string PackageDirectory => _packDirectory;

    internal static void Initialize(string modFolder)
    {
        var defaultPack = ReadPack(Path.Combine(modFolder, "Content", "default.naming.json"));
        _service = new NamingService(defaultPack.Id);
        _service.GameCandidates = NativeOnomastics.Candidates;
        _service.ArrangementCodec = NativeNamingArrangement.Codec;
        VanillaNamingConditions.Register(_service);
        _service.InstallPack(defaultPack);
        var species = ReadPack(Path.Combine(modFolder, "Content", "species-defaults.naming.json"));
        _service.RegisterDefaultPack(species);
        _bundledSpeciesPack = species.Id;
        _bundledPackDirectory = Path.Combine(modFolder, "Content", "NamingPacks");
        _packDirectory = Path.Combine(UnityEngine.Application.persistentDataPath, "ChineseName", "NamingPacks");
        Directory.CreateDirectory(_packDirectory);
        foreach (var message in ReloadPacks()) LogService.LogWarning(message);
        NamingApi.Current = _service;
        NamingApi.SelectWorld = Sync;
    }

    private static NamingPack ReadPack(string file)
    {
        if (new FileInfo(file).Length > 16_000_000) throw new FormatException("Naming package exceeds the file limit.");
        return NamingJson.Read<NamingPack>(File.ReadAllText(file));
    }

    public static IReadOnlyList<string> ReloadPacks()
    {
        var errors = new List<string>();
        var previousByPath = _installedFiles.Values.ToDictionary(file => file.Path, file => file.Pack, StringComparer.Ordinal);
        Dictionary<string, (string Path, NamingPack Pack)> bundled;
        Dictionary<string, (string Path, NamingPack Pack)> files;
        try
        {
            bundled = ReadPacks(_bundledPackDirectory, previousByPath, errors);
            files = new(bundled, StringComparer.Ordinal);
            foreach (var entry in ReadPacks(_packDirectory, previousByPath, errors))
                files[entry.Key] = entry.Value;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            errors.Add("命名包目录读取失败，沿用当前版本 · " + error.Message);
            return errors;
        }

        var catalog = _service.PackageCatalog();
        foreach (string id in _installedFiles.Keys) catalog.Remove(id);
        foreach (var entry in files.ToArray())
        {
            if (catalog.TryAdd(entry.Key, entry.Value.Pack)) continue;
            errors.Add("命名包 ID 已注册 · " + entry.Key);
            files.Remove(entry.Key);
        }
        KeepCompatibleVersions(files, catalog, errors);
        _service.ReplacePackageCatalog(catalog);
        _installedFiles = files;
        BundledPacks.Clear();
        BundledPacks.UnionWith(bundled.Keys.Where(files.ContainsKey));
        return errors;
    }

    private static Dictionary<string, (string Path, NamingPack Pack)> ReadPacks(string directory,
        IReadOnlyDictionary<string, NamingPack> previousByPath, List<string> errors)
    {
        var candidates = new List<(string Path, NamingPack Pack)>();
        if (!Directory.Exists(directory)) return new(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(directory, "*.naming.json").OrderBy(file => file, StringComparer.Ordinal))
        {
            try
            {
                var pack = ReadPack(file);
                if (pack?.Id == _bundledSpeciesPack) continue;
                Configuration.Validate(pack);
                candidates.Add((file, pack));
            }
            catch (Exception error)
            {
                bool retained = previousByPath.TryGetValue(file, out var previous);
                if (retained) candidates.Add((file, previous));
                errors.Add(Path.GetFileName(file) + " · " + error.Message + (retained ? " · 沿用上次有效版本" : ""));
            }
        }
        var files = new Dictionary<string, (string Path, NamingPack Pack)>(StringComparer.Ordinal);
        foreach (var group in candidates.GroupBy(file => file.Pack.Id, StringComparer.Ordinal))
        {
            var first = group.First();
            if (group.Count() == 1) files.Add(group.Key, first);
            else
            {
                bool retained = _installedFiles.TryGetValue(group.Key, out var previous) &&
                    Path.GetDirectoryName(previous.Path) == directory;
                if (retained)
                {
                    string path = group.Any(file => file.Path == previous.Path) ? previous.Path : first.Path;
                    files.Add(group.Key, (path, previous.Pack));
                }
                errors.Add("重复的命名包 ID · " + group.Key + (retained ? " · 沿用上次有效版本" : ""));
            }
        }
        return files;
    }

    private static void KeepCompatibleVersions(Dictionary<string, (string Path, NamingPack Pack)> files,
        Dictionary<string, NamingPack> catalog, List<string> errors)
    {
        var world = _service.ActiveWorld;
        var current = _service.ActivePacks(world);
        if (current.Count == 0) return;
        bool restored;
        do
        {
            restored = false;
            var available = _service.ActivePacks(world, catalog);
            foreach (var entry in current.Where(entry => !available.ContainsKey(entry.Key) && catalog.ContainsKey(entry.Key)))
            {
                string id = entry.Key;
                var next = catalog[id];
                if (next.Requires.Any(requirement =>
                    (!entry.Value.Requires.TryGetValue(requirement.Key, out int before) || before < requirement.Value) &&
                    (!available.TryGetValue(requirement.Key, out var dependency) || dependency.Version < requirement.Value)))
                    Restore(id, "更新后的依赖不可用");
                foreach (var requirement in entry.Value.Requires)
                    if (files.TryGetValue(requirement.Key, out var dependency) && dependency.Pack.Version < requirement.Value &&
                        current.TryGetValue(requirement.Key, out var previous) && previous.Version >= requirement.Value)
                        Restore(requirement.Key, "版本低于 " + id + " 的依赖要求");
            }
        } while (restored);

        void Restore(string id, string message)
        {
            if (!files.TryGetValue(id, out var next) || !_installedFiles.TryGetValue(id, out var before) ||
                ReferenceEquals(next.Pack, before.Pack)) return;
            files[id] = (next.Path, before.Pack);
            catalog[id] = before.Pack;
            restored = true;
            errors.Add(id + " · " + message + " · 沿用上次有效版本");
        }
    }

    private static bool EnableNewBundledPacks(WorldNaming world)
    {
        // Remember which bundled defaults this world has seen, including packs it later turns off.
        var seen = world.InitializedBundledPacks ?? new List<string>();
        var added = BundledPacks.Except(seen, StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (added.Length == 0) return false;
        world.InitializedBundledPacks = seen.Concat(added).ToList();
        world.EnabledPacks.AddRange(added.Except(world.EnabledPacks, StringComparer.Ordinal));
        return true;
    }

    public static string ExportCombination(string id, long? culture, long? language, WorldNaming draft = null)
    {
        Sync();
        var world = draft ?? _service.ActiveWorld;
        var entity = culture.HasValue ? World.world?.cultures.get(culture.Value) : null;
        var arrangements = entity == null ? null : NativeOnomastics.ExportArrangements(_service, entity, language, world);
        return Export(_service.MakePortablePack(id, culture, language, world, arrangements));
    }

    internal static NamingPack CapturePreset(string id, string label, Culture culture, long? language)
    {
        Sync();
        var world = _service.ActiveWorld;
        var arrangements = NativeOnomastics.ExportArrangements(_service, culture, language, world);
        var types = _service.Types.Where(type => NativeOnomastics.Supports(type.Id)).ToArray();
        var snapshot = _service.MakePortablePack(id, culture.id, language, world, arrangements, types, baseVocabulary: true);
        snapshot.Label = label;
        return snapshot;
    }

    public static string ExportEdits(string id, long? culture, long? language, WorldNaming draft = null)
    {
        Sync();
        var world = draft ?? _service.ActiveWorld;
        var pack = new NamingPack { Format = NamingPack.CurrentFormat, Id = id, Supplemental = true, Priority = 100 };
        string basis;
        List<string> bases;
        if (culture.HasValue && !language.HasValue && world.Cultures.TryGetValue(culture.Value, out var cultureEdits))
        {
            pack.RuleEdits = cultureEdits.Rules;
            basis = cultureEdits.Pack ?? world.CulturePack ?? _service.DefaultPack;
            bases = pack.CultureBases;
        }
        else if (language.HasValue && !culture.HasValue && world.Languages.TryGetValue(language.Value, out var languageEdits))
        {
            pack.LanguageEdits = _service.Inspect(new NameRequest(VanillaNames.Person, 0, language: language), world).LanguageSettings.Pools;
            basis = languageEdits.Pack ?? world.LanguagePack ?? _service.DefaultPack;
            bases = pack.LanguageBases;
        }
        else throw new ArgumentException("请选择有修改的一种文化或语言。");
        if (_service.DescribePackage(basis) is NamingPack source)
        {
            pack.Requires.Add(basis, source.Version);
            bases.Add(basis);
        }
        return Export(pack);
    }

    public static string Export(NamingPack pack)
    {
        Configuration.Validate(pack);
        string path = Path.Combine(_packDirectory, pack.Id.Replace(':', '-') + ".naming.json");
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, NamingJson.WritePack(pack));
            File.Move(temp, path);
        }
        finally { File.Delete(temp); }
        return path;
    }

    internal static WorldNaming GetSavedDraft()
    {
        Sync();
        string json = null;
        _map?.custom_data?.get(DraftKey, out json);
        if (json == null) return _service.GetWorld();
        try
        {
            var saved = NamingJson.Read<WorldNaming>(json);
            if (saved?.Format != 1) return _service.GetWorld();
            saved.AppliedPresets = _service.ActiveWorld.AppliedPresets;
            EnableNewBundledPacks(saved);
            return saved;
        }
        catch (Exception error)
        {
            LogService.LogWarning("Cannot restore ChineseName draft: " + error.Message);
            return _service.GetWorld();
        }
    }

    internal static void SaveDraft(WorldNaming draft)
    {
        Sync();
        if (_map == null) throw new InvalidOperationException("No world is active.");
        _map.custom_data ??= new SaveCustomData();
        if (draft == null) _map.custom_data.removeString(DraftKey);
        else _map.custom_data.set(DraftKey, NamingJson.WriteWorld(draft.WithoutAppliedPresets()));
    }

    internal static bool HasSavedDraft()
    {
        Sync();
        string json = null;
        _map?.custom_data?.get(DraftKey, out json);
        return json != null;
    }

    public static void Apply(WorldNaming draft)
    {
        RequireWorld();
        if (_loadError != null) throw new InvalidOperationException(_loadError);
        var previous = _service.ActiveWorld;
        var next = draft.WithoutProfiles();
        next.InitializedBundledPacks = previous.InitializedBundledPacks;
        next.Cultures = new(draft.Cultures);
        next.Languages = new(draft.Languages);
        next.LanguageParents = new(draft.LanguageParents);

        var header = next.WithoutProfiles();
        foreach (long id in UnavailableCultures) Retain(id, previous.Cultures, next.Cultures, header.Cultures);
        foreach (long id in UnavailableLanguages)
        {
            Retain(id, previous.Languages, next.Languages, header.Languages);
            Retain(id, previous.LanguageParents, next.LanguageParents, header.LanguageParents);
        }
        foreach (var link in next.LanguageParents)
            if (!next.Languages.ContainsKey(link.Key)) header.LanguageParents[link.Key] = link.Value;

        string json = NamingJson.WriteWorld(header);
        var profiles = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in next.Cultures)
            if (!UnavailableCultures.Contains(entry.Key))
                profiles.Add(ProfileKey(CulturePrefix, entry.Key), NamingJson.WriteProfile(entry.Value));
        foreach (var entry in next.Languages)
            if (!UnavailableLanguages.Contains(entry.Key))
                profiles.Add(ProfileKey(LanguagePrefix, entry.Key), WriteLanguage(entry.Value,
                    next.LanguageParents.TryGetValue(entry.Key, out long parent) ? parent : null));

        var stored = _map.custom_data?.custom_data_string;
        var removed = ProfileKeys(_map).Where(key => !profiles.ContainsKey(key) &&
            TryProfileId(key, out bool culture, out long id) && !(culture ? UnavailableCultures : UnavailableLanguages).Contains(id)).ToArray();
        _service.SetWorld(next);
        SaveIfChanged(SaveKey, json);
        foreach (var entry in profiles) SaveIfChanged(entry.Key, entry.Value);
        foreach (string key in removed) _map.custom_data.removeString(key);

        void SaveIfChanged(string key, string content)
        {
            if (stored == null || !stored.TryGetValue(key, out string before) || before != content) Save(_map, content, key);
        }

        static void Retain<T>(long id, Dictionary<long, T> previous, Dictionary<long, T> current, Dictionary<long, T> header)
        {
            if (previous.TryGetValue(id, out var fallback)) header[id] = current[id] = fallback;
            else current.Remove(id);
        }
    }

    internal static WorldNaming LineageWorld()
    {
        Sync();
        return _map != null ? _service.ActiveWorld : null;
    }

    internal static IReadOnlyList<string> StorageWarnings()
    {
        Sync();
        return LoadIssues.ToArray();
    }

    private static void RequireWorld()
    {
        Sync();
        if (_map == null) throw new InvalidOperationException("No world is active.");
    }

    internal static void SetCulture(long id, CultureNaming settings)
    {
        RequireWorld();
        if (UnavailableCultures.Contains(id)) throw new InvalidOperationException("此文化的原始命名设置无法读取，已保留供恢复。");
        var owned = NamingJson.Copy(settings);
        Save(_map, NamingJson.WriteProfile(owned), ProfileKey(CulturePrefix, id));
        _service.SetCulture(id, owned);
    }

    internal static void SetLanguage(long id, LanguageNaming settings, long? parent = null)
    {
        RequireWorld();
        if (UnavailableLanguages.Contains(id)) throw new InvalidOperationException("此语言的原始词库设置无法读取，已保留供恢复。");
        if (!parent.HasValue && _service.ActiveWorld.LanguageParents.TryGetValue(id, out long previous)) parent = previous;
        var owned = NamingJson.Copy(settings);
        Save(_map, WriteLanguage(owned, parent), ProfileKey(LanguagePrefix, id));
        _service.SetLanguage(id, owned, parent);
    }

    internal static void ApplyPackageSelection(WorldNaming world)
    {
        Apply(world);
        if (!HasSavedDraft()) return;
        var draft = GetSavedDraft();
        draft.EnabledPacks = world.EnabledPacks;
        draft.PackPriorities = world.PackPriorities;
        SaveDraft(draft);
    }

    internal static string ApplyCultureStyle(long culture, string pack, NamingPack snapshot = null)
    {
        var world = NamingApi.Current.GetWorld();
        if (snapshot != null && world.AppliedPresets.TryGetValue(pack, out var prior))
        {
            if (PlayerPresetLibrary.SameContent(prior, snapshot)) prior.Label = snapshot.Label;
            else
            {
                snapshot = NamingJson.Copy(snapshot);
                pack = snapshot.Id = PlayerPresetLibrary.NewId();
            }
        }
        if (!world.Cultures.TryGetValue(culture, out var settings))
            world.Cultures[culture] = settings = new CultureNaming();
        settings.Pack = pack;
        if (snapshot != null) ReplaceCapturedArrangements(settings, snapshot);
        if (snapshot != null && !world.AppliedPresets.ContainsKey(pack))
            world.AppliedPresets.Add(pack, snapshot);
        var used = new HashSet<string>(world.Cultures.Values.Select(value => value.Pack), StringComparer.Ordinal);
        foreach (string id in world.AppliedPresets.Keys.Where(id => !used.Contains(id)).ToArray())
            world.AppliedPresets.Remove(id);
        Apply(world);
        if (!HasSavedDraft()) return pack;
        var draft = GetSavedDraft();
        if (!draft.Cultures.TryGetValue(culture, out settings))
            draft.Cultures[culture] = settings = new CultureNaming();
        settings.Pack = pack;
        if (snapshot != null) ReplaceCapturedArrangements(settings, snapshot);
        SaveDraft(draft);
        return pack;
    }

    private static void ReplaceCapturedArrangements(CultureNaming settings, NamingPack snapshot)
    {
        foreach (string type in snapshot.Rules.Keys.Concat(snapshot.Arrangements.Keys).Distinct(StringComparer.Ordinal))
        {
            settings.ResetArrangements(type);
            settings.Cards[type] = new CardNaming();
            settings.Presets.Remove(type);
            foreach (string key in settings.Rules.Keys.Where(key => key.StartsWith(type + "/", StringComparison.Ordinal)).ToArray())
                settings.Rules.Remove(key);
        }
    }

    internal static void ApplyLanguageSettings(long language, Action<LanguageNaming> edit)
    {
        RequireWorld();
        var settings = _service.ActiveWorld.Languages.TryGetValue(language, out var previous)
            ? NamingJson.Copy(previous) : new LanguageNaming();
        edit(settings);
        SetLanguage(language, settings);
        if (!HasSavedDraft()) return;
        var draft = GetSavedDraft();
        draft.Languages[language] = settings;
        SaveDraft(draft);
    }

    private static void Sync()
    {
        var map = World.world?.map_stats;
        if (!ReferenceEquals(map, _map)) Load(map);
    }

    private static void Load(MapStats map)
    {
        _map = map;
        ResetLoadIssues();
        try
        {
            string json = null;
            map?.custom_data?.get(SaveKey, out json);
            if (json != null)
            {
                var saved = NamingJson.Read<WorldNaming>(json);
                bool changed = saved?.Format == 1 && EnableNewBundledPacks(saved);
                string current = changed ? NamingJson.WriteWorld(saved) : null;
                if (saved?.Format == 1) LoadProfiles(map, saved);
                _service.SetWorld(saved);
                if (changed) Save(map, current);
                return;
            }
        }
        catch (Exception error)
        {
            _loadError = "世界的命名包设置无法读取，已保留原始记录。";
            Report(_loadError, error);
        }
        var settings = new WorldNaming { UseSavedVanillaCards = map != null && _loadError == null };
        bool initialized = map != null && EnableNewBundledPacks(settings);
        if (initialized && _loadError == null) Save(map, NamingJson.WriteWorld(settings));
        LoadProfiles(map, settings);
        _service.SetWorld(settings);
    }

    private static IEnumerable<string> ProfileKeys(MapStats map) =>
        (map?.custom_data?.custom_data_string?.Keys ?? Enumerable.Empty<string>()).Where(key =>
            key.StartsWith(CulturePrefix, StringComparison.Ordinal) || key.StartsWith(LanguagePrefix, StringComparison.Ordinal));

    private static string ProfileKey(string prefix, long id) => prefix + id.ToString(CultureInfo.InvariantCulture);

    private static string WriteLanguage(LanguageNaming settings, long? parent) =>
        NamingJson.WriteProfile(new SavedLanguage { Settings = settings, Parent = parent });

    private static bool TryProfileId(string key, out bool culture, out long id)
    {
        culture = key.StartsWith(CulturePrefix, StringComparison.Ordinal);
        string prefix = culture ? CulturePrefix : LanguagePrefix;
        return long.TryParse(key.Substring(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out id) &&
            key == ProfileKey(prefix, id);
    }

    private static void LoadProfiles(MapStats map, WorldNaming saved)
    {
        foreach (string key in ProfileKeys(map))
        {
            if (!TryProfileId(key, out bool culture, out long id)) continue;
            try
            {
                string json = map.custom_data.custom_data_string[key];
                if (culture)
                    saved.Cultures[id] = NamingJson.ReadProfile<CultureNaming>(json) ?? throw new FormatException("Missing culture settings.");
                else
                {
                    var entry = NamingJson.ReadProfile<SavedLanguage>(json);
                    saved.Languages[id] = entry?.Settings ?? throw new FormatException("Missing language settings.");
                    if (entry.Parent.HasValue) saved.LanguageParents[id] = entry.Parent.Value;
                }
            }
            catch (Exception error) when (error is FormatException or Newtonsoft.Json.JsonException)
            {
                (culture ? UnavailableCultures : UnavailableLanguages).Add(id);
                Report((culture ? "文化 " : "语言 ") + id + " 的命名设置无法读取，已保留原始记录。", error);
            }
        }
    }

    private static void Report(string issue, Exception error)
    {
        LoadIssues.Add(issue);
        LogService.LogWarning(issue + " " + error.Message);
    }

    private static void ResetLoadIssues()
    {
        _loadError = null;
        UnavailableCultures.Clear();
        UnavailableLanguages.Clear();
        LoadIssues.Clear();
    }

    private static void Save(MapStats map, string json, string key = SaveKey)
    {
        map.custom_data ??= new SaveCustomData();
        map.custom_data.set(key, json);
    }

    [HarmonyPatch(typeof(MapStats), nameof(MapStats.initNewWorld))]
    internal static class NewWorld
    {
        private static void Prefix(MapStats __instance)
        {
            _map = __instance;
            ResetLoadIssues();
            var settings = new WorldNaming();
            EnableNewBundledPacks(settings);
            _service.SetWorld(settings);
        }

        private static void Postfix(MapStats __instance) => Save(__instance, NamingJson.WriteWorld(_service.ActiveWorld));
    }
}
