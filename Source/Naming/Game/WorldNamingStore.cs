using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using NeoModLoader.services;

namespace Chinese_Name.Naming.Game;

public static class WorldNamingStore
{
    private const string SaveKey = "chinesename.rules.v1";
    private const string DraftKey = "chinesename.draft.v1";
    private static NamingService _service;
    private static MapStats _map;
    private static string _packDirectory;
    private static string _loadError;
    private static string _bundledSpeciesPack;
    private static readonly HashSet<string> DiskPacks = new(StringComparer.Ordinal);

    public static string PackageDirectory => _packDirectory;

    internal static void Initialize(string modFolder)
    {
        var defaultPack = ReadPack(Path.Combine(modFolder, "Content", "default.naming.json"));
        _service = new NamingService(defaultPack.Id);
        _service.GameRule = NativeOnomastics.Generate;
        foreach (var type in VanillaNames.Types) _service.RegisterType(type);
        _service.InstallPack(defaultPack);
        var species = ReadPack(Path.Combine(modFolder, "Content", "species-defaults.naming.json"));
        _service.RegisterDefaultPack(species);
        _bundledSpeciesPack = species.Id;
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
        var files = new Dictionary<string, NamingPack>(StringComparer.Ordinal);
        var duplicates = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(_packDirectory, "*.naming.json").OrderBy(file => file, StringComparer.Ordinal))
        {
            try
            {
                var pack = ReadPack(file);
                // 此词库现在随模组加载，之前安装的同 ID 文件可以继续留在存档目录。
                if (pack.Id == _bundledSpeciesPack) continue;
                Configuration.Validate(pack);
                if (files.ContainsKey(pack.Id)) duplicates.Add(pack.Id);
                else files.Add(pack.Id, pack);
            }
            catch (Exception error) { errors.Add(Path.GetFileName(file) + " · " + error.Message); }
        }
        foreach (var id in duplicates)
        {
            files.Remove(id);
            errors.Add("重复的命名包 ID · " + id);
        }
        foreach (var id in DiskPacks) _service.UninstallPack(id);
        DiskPacks.Clear();
        foreach (var pack in files.Values)
        {
            try
            {
                _service.InstallPack(pack);
                DiskPacks.Add(pack.Id);
            }
            catch (Exception error) { errors.Add(pack.Id + " · " + error.Message); }
        }
        return errors;
    }

    public static string ExportCombination(string id, long? culture, long? language, WorldNaming draft = null)
    {
        Sync();
        var world = draft ?? _service.ActiveWorld;
        var entity = culture.HasValue ? World.world?.cultures.get(culture.Value) : null;
        var arrangements = entity == null ? null : NativeOnomastics.ExportArrangements(_service, entity, language, world);
        return Export(_service.MakePortablePack(id, culture, language, world, arrangements));
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
            pack.LanguageEdits = languageEdits.Pools;
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
            return saved?.Format == 1 ? saved : _service.GetWorld();
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
        else _map.custom_data.set(DraftKey, NamingJson.Write(draft));
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
        Sync();
        if (_map == null) throw new InvalidOperationException("No world is active.");
        if (_loadError != null) throw new InvalidOperationException(_loadError);
        string json = NamingJson.Write(draft);
        _service.SetWorld(draft);
        _map.custom_data ??= new SaveCustomData();
        _map.custom_data.set(SaveKey, json);
    }

    internal static void Update(Func<WorldNaming, bool> edit)
    {
        Sync();
        if (_map == null || _loadError != null) return;
        var draft = _service.GetWorld();
        if (edit(draft)) Apply(draft);
    }

    private static void Sync()
    {
        var map = World.world?.map_stats;
        if (!ReferenceEquals(map, _map)) Load(map);
    }

    private static void Load(MapStats map)
    {
        _map = map;
        _loadError = null;
        try
        {
            string json = null;
            map?.custom_data?.get(SaveKey, out json);
            if (json != null)
            {
                _service.SetWorld(NamingJson.Read<WorldNaming>(json));
                return;
            }
        }
        catch (Exception error)
        {
            _loadError = "Unable to load the saved naming configuration: " + error.Message;
            LogService.LogWarning(_loadError);
        }
        _service.SetWorld(new WorldNaming { UseSavedVanillaCards = map != null && _loadError == null });
    }

    [HarmonyPatch(typeof(MapStats), nameof(MapStats.initNewWorld))]
    internal static class NewWorld
    {
        private static void Prefix(MapStats __instance)
        {
            _map = __instance;
            _loadError = null;
            _service.SetWorld(new WorldNaming());
        }

        private static void Postfix(MapStats __instance)
        {
            __instance.custom_data ??= new SaveCustomData();
            __instance.custom_data.set(SaveKey, NamingJson.Write(_service.ActiveWorld));
        }
    }
}
