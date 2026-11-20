using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeoModLoader.services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Chinese_Name.Naming.Game;

internal static class PlayerPresetLibrary
{
    internal const int NameLimit = 32;
    private const int Format = 1;
    private const string Prefix = "local-preset:p";
    private const string Extension = ".preset.json";
    private static Dictionary<string, Entry> _entries;
    private static string DirectoryPath => Path.Combine(Application.persistentDataPath, "ChineseName", "Presets");
    internal static string ExportDirectory => Path.Combine(Application.persistentDataPath, "ChineseName", "PresetExports");

    internal static IEnumerable<PlayerNamingPreset> Entries => Library().Values.Select(entry => entry.Preset).OrderBy(item => item.Snapshot.Label,
        StringComparer.Ordinal).ThenBy(item => item.Snapshot.Id, StringComparer.Ordinal);

    private sealed class Entry
    {
        internal Entry(PlayerNamingPreset preset, string path)
        {
            Preset = preset;
            Files = new List<string> { path };
        }

        internal PlayerNamingPreset Preset;
        internal readonly List<string> Files;
    }

    internal static string NewId() => Prefix + Guid.NewGuid().ToString("N");

    internal static PlayerNamingPreset Save(string name, Culture culture, long? language)
    {
        var label = UniqueName(ValidName(name));
        return Add(new PlayerNamingPreset
        {
            Format = Format, Snapshot = WorldNamingStore.CapturePreset(NewId(), label, culture, language)
        });
    }

    internal static PlayerNamingPreset Rename(string id, string name)
    {
        var entry = GetEntry(id);
        string label = UniqueName(ValidName(name), id);
        if (label == entry.Preset.Snapshot.Label) return entry.Preset;
        var renamed = NamingJson.Copy(entry.Preset);
        renamed.Snapshot.Label = label;
        Write(entry.Files[0], NamingJson.WritePreset(renamed), replace: true);
        entry.Preset = renamed;
        return renamed;
    }

    internal static void Delete(string id)
    {
        var entry = GetEntry(id);
        // Remove duplicate files first so the visible entry survives an incomplete deletion.
        for (int index = entry.Files.Count - 1; index >= 0; index--)
            File.Delete(entry.Files[index]);
        Library().Remove(id);
    }

    private static Entry GetEntry(string id) => Library().TryGetValue(id, out var entry)
        ? entry : throw new InvalidOperationException("该预设已不在玩家库中。");

    private static string ValidName(string name)
    {
        name = name.Trim();
        if (!Ids.Text(name, NameLimit) || string.IsNullOrWhiteSpace(name) || name.Any(c => c is '<' or '>'))
            throw new FormatException("请输入 1 至 32 个字符的预设名称。");
        return name;
    }

    internal static PlayerNamingPreset Parse(string text)
    {
        try
        {
            var preset = NamingJson.Read<PlayerNamingPreset>(text);
            Validate(preset);
            return preset;
        }
        catch (Exception error) when (error is FormatException or JsonException or ArgumentException or NullReferenceException)
        {
            throw new FormatException("剪贴板中没有可导入的命名预设。", error);
        }
    }

    internal static PlayerNamingPreset Import(PlayerNamingPreset preset)
    {
        var library = Library();
        var snapshot = preset.Snapshot;
        var content = SnapshotContent(snapshot);
        var existing = library.Values.FirstOrDefault(entry => JToken.DeepEquals(SnapshotContent(entry.Preset.Snapshot), content));
        if (existing != null) return existing.Preset;
        var copy = NamingJson.Copy(preset);
        if (library.ContainsKey(snapshot.Id)) copy.Snapshot.Id = NewId();
        copy.Snapshot.Label = UniqueName(snapshot.Label);
        return Add(copy);
    }

    internal static bool SameContent(NamingPack left, NamingPack right) =>
        JToken.DeepEquals(SnapshotContent(left), SnapshotContent(right));

    private static JObject SnapshotContent(NamingPack snapshot)
    {
        var content = JObject.Parse(NamingJson.WritePack(snapshot));
        content.Remove("id");
        content.Remove("label");
        return content;
    }

    internal static string Export(PlayerNamingPreset preset)
    {
        string text = NamingJson.WritePreset(preset);
        string path = PathFor(ExportDirectory, preset.Snapshot.Id);
        Write(path, text, replace: File.Exists(path));
        GUIUtility.systemCopyBuffer = text;
        return path;
    }

    internal static string[] Missing(PlayerNamingPreset preset)
    {
        var service = NamingApi.Current;
        var types = service.Types.ToDictionary(type => type.Id, StringComparer.Ordinal);
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var entry in preset.Snapshot.Rules)
        {
            if (!types.TryGetValue(entry.Key, out var type))
            {
                missing.Add(entry.Key);
                continue;
            }
            foreach (var rule in entry.Value)
            {
                if (rule.NativeCards is { } cards)
                {
                    using var native = NativeNamingArrangement.ReadPackaged(entry.Key, cards);
                    if (native == null) missing.Add(type.Label + "字形");
                }
                foreach (var part in rule.Parts)
                    if (part.Provider != null && !service.HasProvider(part.Provider)) missing.Add(part.Provider);
            }
        }
        return missing.ToArray();
    }

    private static PlayerNamingPreset Add(PlayerNamingPreset preset)
    {
        Validate(preset);
        var library = Library();
        string path = PathFor(DirectoryPath, preset.Snapshot.Id);
        Write(path, NamingJson.WritePreset(preset), replace: false);
        library.Add(preset.Snapshot.Id, new Entry(preset, path));
        return preset;
    }

    private static void Validate(PlayerNamingPreset preset)
    {
        var pack = preset?.Snapshot;
        if (preset?.Format != Format || pack?.Id == null ||
            !pack.Id.StartsWith(Prefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(pack.Id.Substring(Prefix.Length), "N", out _) ||
            !Ids.Text(pack.Label, NameLimit) || string.IsNullOrWhiteSpace(pack.Label) || pack.Label.Any(c => c is '<' or '>') ||
            pack.Supplemental || !pack.UseAsLanguageDefault || pack.Requires.Count != 0 || pack.Rules.Count == 0 ||
            pack.PoolPatches.Count != 0 || pack.RuleEdits.Count != 0 || pack.LanguageEdits.Count != 0)
            throw new FormatException("命名预设格式无效。");
        Configuration.Validate(pack);
        var words = new HashSet<string>(pack.Words.Select(word => word.Id), StringComparer.Ordinal);
        if (pack.Pools.Values.Any(pool => pool.Keys.Any(id => !words.Contains(id))) ||
            pack.PoolAliases.Values.Any(pool => !pack.Pools.ContainsKey(pool)) ||
            pack.WordGroups.Values.Any(group => !pack.Pools.TryGetValue(group.Pool, out var pool) ||
                !group.Words.All(pool.ContainsKey)))
            throw new FormatException("命名预设内的词库引用无效。");
    }

    private static Dictionary<string, Entry> Library()
    {
        if (_entries != null) return _entries;
        Directory.CreateDirectory(DirectoryPath);
        var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (string file in Directory.EnumerateFiles(DirectoryPath, "*" + Extension).OrderBy(path => path, StringComparer.Ordinal))
        {
            try
            {
                if (new FileInfo(file).Length > 16_000_000) throw new FormatException("命名预设超过大小上限。");
                var preset = Parse(File.ReadAllText(file));
                if (entries.TryGetValue(preset.Snapshot.Id, out var entry)) entry.Files.Add(file);
                else entries.Add(preset.Snapshot.Id, new Entry(preset, file));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException)
            {
                LogService.LogWarning(Path.GetFileName(file) + " · " + error.Message);
            }
        }
        return _entries = entries;
    }

    private static string UniqueName(string name, string exceptId = null)
    {
        var names = new HashSet<string>(Library().Values.Select(entry => entry.Preset.Snapshot)
            .Where(snapshot => snapshot.Id != exceptId).Select(snapshot => snapshot.Label), StringComparer.OrdinalIgnoreCase);
        string candidate = name;
        for (int i = 2; names.Contains(candidate); i++)
        {
            string suffix = "（" + i + "）";
            int length = Math.Min(name.Length, NameLimit - suffix.Length);
            if (length < name.Length && char.IsHighSurrogate(name[length - 1])) length--;
            candidate = name.Substring(0, length) + suffix;
        }
        return candidate;
    }

    private static string PathFor(string directory, string id) => Path.Combine(directory, id.Substring(Prefix.Length) + Extension);

    private static void Write(string path, string text, bool replace)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, text);
            if (replace) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally { File.Delete(temporary); }
    }
}
