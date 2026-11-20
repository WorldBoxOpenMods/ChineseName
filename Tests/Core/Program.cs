using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Chinese_Name.Naming;

static void Check(bool result, string message)
{
    if (!result) throw new InvalidOperationException(message);
}

static NamingPack Read(string json) => NamingJson.Read<NamingPack>(json);

const string type = "tests:person";
const string pool = "tests:given";
var service = new NamingService("tests:base");
service.RegisterType(new NameType(type, "人名", new[] { new InputField("kind", InputKind.Text, true) },
    fixedParts: new[] { "surname" }));
service.InstallPack(Read("""
    {"format":1,"id":"tests:base","rules":{"tests:person":[
        {"id":"person","parts":[{"id":"surname","literal":"李"},{"id":"given","pool":"tests:given"}]}
    ]},"words":[{"id":"tests:green","text":"青"},{"id":"tests:river","text":"川"}],
    "pools":{"tests:given":{"tests:green":1,"tests:river":1}}}
    """));
service.InstallPack(Read("""
    {"format":1,"id":"tests:append","supplemental":true,"requires":{"tests:base":1},
    "words":[{"id":"tests:mist","text":"岚"}],
    "poolPatches":[{"operation":"Append","pool":"tests:given","words":{"tests:green":3,"tests:mist":3}},
                   {"operation":"Append","pool":"tests:given","words":{"tests:green":2}}]}
    """));
service.InstallPack(Read("""
    {"format":1,"id":"tests:missing","supplemental":true,"requires":{"tests:absent":1},
    "poolPatches":[{"operation":"Replace","pool":"tests:given","words":{}}]}
    """));
service.InstallPack(Read("""
    {"format":1,"id":"tests:alternative","rules":{"tests:person":[
        {"id":"person","parts":[{"id":"surname","literal":"赵"},{"id":"given","pool":"tests:given"},{"id":"extra","literal":"子"}]}
    ]},"pools":{"tests:given":{"tests:green":1,"tests:river":1}}}
    """));

NameRequest Request(ulong seed = 42, long? culture = null, long? language = null) =>
    new(type, seed, culture, language, new Dictionary<string, object> { ["kind"] = "sapient" },
        new Dictionary<string, string> { ["surname"] = "欧阳" });
var baseline = service.Generate(Request());
Check(baseline.Success && baseline.Text.StartsWith("欧阳"), "Preserve an inherited two-character surname.");
Check(service.Generate(new NameRequest(type, 0)).Status == GenerationStatus.InvalidInput, "Validate required input.");
Check(service.Generate(new NameRequest("tests:unknown", 0)).Status == GenerationStatus.UnknownType, "Report missing type.");
Check(service.Generate(Request(), _ => false).Status == GenerationStatus.Exhausted, "Bound rejected names.");

var draft = service.GetWorld();
draft.EnabledPacks.Add("tests:append");
draft.EnabledPacks.Add("tests:missing");
var effective = service.Inspect(Request(), draft);
Check(effective.Pools[pool].ToDictionary(word => word.Text, word => word.Weight)
    .All(pair => pair.Value == (pair.Key == "川" ? 1 : 3)), "Merge duplicate references by maximum weight.");
Check(effective.Pools[pool].Length == 3, "Resolve precisely three word IDs.");
Check(effective.Diagnostics.Contains("dependency:tests:missing/tests:absent"), "Report unavailable pack dependencies.");
Check(service.Inspect(Request(), null).Pools[pool].Length == 2, "Preview must never enable an installed pack.");
draft.Languages[9] = new LanguageNaming
{
    Pools = new Dictionary<string, PoolEdit> { [pool] = new() { ExcludeText = new List<string> { "青" } } }
};
Check(!service.Inspect(Request(language: 9), draft).Pools[pool].Any(word => word.Text == "青"),
    "Player exclusions win over pack contributions.");

draft.EnabledPacks.Add("tests:alternative");
draft.CulturePack = "tests:alternative";
draft.Cultures[13] = new CultureNaming
{
    Rules = new Dictionary<string, RuleEdit>
    {
        ["tests:person/person"] = new() { Order = new List<string> { "surname", "given" } }
    }
};
Check(service.Preview(Request(culture: 13), draft).Diagnostics.Contains("pending-part:tests:person/person/extra"),
    "A new output part is pending when the player owns the order.");
service.SetWorld(draft);
var name = service.Generate(Request(culture: 13, language: 9));
var reloaded = NamingJson.Read<WorldNaming>(NamingJson.Write(service.GetWorld()));
service.SetWorld(reloaded);
Check(name.Text == service.Generate(Request(culture: 13, language: 9)).Text,
    "Restart preserves the effective candidate weights and sample.");

var spell = new NamingService();
spell.RegisterProvider("tests:fail", _ => throw new InvalidOperationException("test extension failure"));
spell.RegisterType(new NameType("tests:spell", "法术"), Read("""
    {"format":1,"id":"tests:spell-defaults","supplemental":true,"rules":{"tests:spell":[
        {"id":"detailed","priority":10,"parts":[{"id":"description","provider":"tests:fail"}]},
        {"id":"fallback","parts":[{"id":"description","literal":"炎刃诀"}]}
    ]}}
    """));
var spellName = spell.Generate(new NameRequest("tests:spell", 42));
Check(spellName.Text == "炎刃诀" && spellName.Diagnostics.Contains("provider:tests:fail:InvalidOperationException"),
    "A failing extension provider must leave deterministic fallbacks available.");

var bundled = NamingJson.Read<NamingPack>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "default.naming.json")));
Configuration.Validate(bundled);

var lexicons = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Lexicons"), "*.naming.json")
    .Select(file => Read(File.ReadAllText(file))).ToArray();
Check(lexicons.Length != 0, "Optional lexicon fixtures must be present.");
var catalog = new NamingService(bundled.Id);
const string city = "worldbox:city";
var context = new[] { "species", "kind", "culture-species", "founder-surname", "attacker", "defender",
    "origin", "station", "topic", "material", "equipment", "stage", "plot" }.Select(id => new InputField(id, InputKind.Text)).ToArray();
catalog.RegisterType(new NameType("worldbox:person", "姓名", context, fixedParts: new[] { "surname" }));
var societyCategories = new[] { "city", "kingdom", "culture", "language", "family", "clan", "war", "alliance", "army", "book", "motto", "item" };
foreach (string category in societyCategories.Concat(new[] { "religion", "subspecies", "plot" }))
    catalog.RegisterType(new NameType("worldbox:" + category, category, context));
catalog.InstallPack(bundled);
var languagePlot = catalog.Generate(new NameRequest("worldbox:plot", 7, inputs: new Dictionary<string, object>
{
    ["plot"] = "language-divergence"
}));
Check(languagePlot.Success && languagePlot.Text is "语言分化" or "乡音分支", "A named plot should retain its actual action.");
var unknownPlot = catalog.Generate(new NameRequest("worldbox:plot", 7, inputs: new Dictionary<string, object>
{
    ["plot"] = "custom-action"
}));
Check(unknownPlot.Success && unknownPlot.Text is "筹议" or "密议" or "合谋", "An unknown plot should have a Chinese fallback.");
var cityRequest = new NameRequest(city, 7);
string baselineCity = catalog.Generate(cityRequest).Text;
var enabledLexicons = catalog.GetWorld();
foreach (var lexicon in lexicons)
{
    Check(lexicon.Supplemental && lexicon.Rules.Count == 0 && lexicon.PoolPatches.Count == 0,
        "Optional vocabulary must compose without replacing the existing rules or vocabulary.");
    catalog.InstallPack(lexicon);
    enabledLexicons.EnabledPacks.Add(lexicon.Id);
}
catalog.SetWorld(enabledLexicons);
Check(catalog.Generate(cityRequest).Text == baselineCity, "Enabling vocabulary changed an existing rule.");
var expanded = catalog.Inspect(cityRequest, null);
foreach (var lexicon in lexicons)
{
    foreach (var alias in lexicon.PoolAliases)
    {
        Check(expanded.ResolvePoolToken(alias.Key, out bool ambiguous) == alias.Value && !ambiguous &&
              expanded.ResolvePoolToken(alias.Value.Replace(':', '/'), out _) == alias.Value,
            "An optional alias collides or cannot be addressed explicitly: " + alias.Key);
    }
    foreach (var poolEntry in lexicon.Pools)
    {
        var candidates = expanded.GetPool(poolEntry.Key).Select(word => word.Id).ToHashSet();
        Check(candidates.SetEquals(poolEntry.Value.Keys), "An optional pool lost its candidates: " + poolEntry.Key);
        var groups = lexicon.WordGroups.Values.Where(group => group.Pool == poolEntry.Key).ToArray();
        Check(groups.Length >= 2 && groups.All(group => group.Words.Count > 1 && group.Words.Count < candidates.Count &&
              group.Words.All(candidates.Contains)), "Invalid language groups for " + poolEntry.Key);
    }
}

var namingSets = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "NamingSets"), "*.naming.json")
    .Select(file => Read(File.ReadAllText(file))).ToArray();
Check(namingSets.Count(pack => pack.Id.StartsWith("chinesename:world-", StringComparison.Ordinal)) >= 38 &&
      namingSets.Any(pack => pack.Id == "chinesename:species-set"), "The regional and species catalogs must be generated.");
foreach (var style in namingSets)
{
    var expected = societyCategories.Prepend("person");
    if (style.Id == "chinesename:species-set") expected = expected.Concat(new[] { "religion", "subspecies" });
    else if (style.Id.StartsWith("chinesename:world-", StringComparison.Ordinal)) expected = expected.Append("religion");
    Check(!style.Supplemental && style.Rules.Keys.ToHashSet().SetEquals(expected.Select(type => "worldbox:" + type)) &&
          style.Words.Count == 0, "A style must bind every covered category and reuse installed vocabulary.");
    catalog.InstallPack(style);
    var bindings = NamingJson.Copy(enabledLexicons);
    bindings.EnabledPacks.Add(style.Id);
    Check(catalog.Preview(cityRequest, bindings).Text == baselineCity, "Enabling an unselected naming set changed the defaults.");
    bindings.CulturePack = style.Id;
    foreach (var category in style.Rules)
    foreach (var rule in category.Value)
    {
        Check(rule.UseNativeWordGroups, "The set must expose the vocabulary as native word groups.");
        var inputs = rule.When.ToDictionary(pair => pair.Key, pair => (object)pair.Value);
        foreach (var match in rule.WhenOneOf) inputs[match.Key] = match.Value[0];
        foreach (var match in rule.WhenStartsWith) inputs[match.Key] = match.Value + "example";
        if (category.Key == "worldbox:war") { inputs["attacker"] = "临川国"; inputs["defender"] = "松岭国"; }
        if (category.Key == "worldbox:book") inputs["topic"] = "history";
        if (category.Key == "worldbox:item") { inputs["material"] = "iron"; inputs["equipment"] = "sword"; }
        var request = new NameRequest(category.Key, 7, inputs: inputs);
        var result = catalog.Preview(request, bindings);
        Check(result.Success && rule.Parts.Where(part => part.Pool != null || part.FallbackPool != null).All(part =>
            result.Sources.TryGetValue(part.Id, out var source) && source.Reference == (part.PoolInput == null
                ? part.Pool ?? part.FallbackPool : inputs.TryGetValue(part.PoolInput, out object suffix)
                    ? NameEngine.PoolPrefix(part.Pool) + suffix : part.FallbackPool)),
            "The set does not resolve its declared vocabulary: " + category.Key + "/" + rule.Id);
    }
    var dependency = style.Requires.Keys.Single(id => id != bundled.Id);
    bindings.EnabledPacks.Remove(dependency);
    Check(catalog.Preview(cityRequest, bindings).Text == baselineCity, "A missing lexicon must restore the fallback.");
}

var regionNames = catalog.GetWorld();
const string british = "chinesename:world-british-set", korean = "chinesename:world-korean-set";
regionNames.EnabledPacks.AddRange(new[] { british, korean });
var identity = new Dictionary<string, object> { ["kind"] = "sapient" };
foreach (var (set, source, given, surname, original, localized) in new[] {
    (british, "british", "John", "Smith", "John Smith", "约翰·史密斯"),
    (korean, "korean", "민준", "김", "김민준", "金敏俊")
})
{
    regionNames.CulturePack = set;
    var worldWords = lexicons.Single(pack => pack.Id == "chinesename:world-styles-lexicon");
    var language = regionNames.Languages[17] = new LanguageNaming();
    foreach (var (role, word) in new[] { ("given", given), ("surname", surname) })
    {
        string group = worldWords.Id + "." + source + "." + role;
        var member = worldWords.Words.Single(entry => entry.Text == word && worldWords.Pools[group].ContainsKey(entry.Id));
        language.Pools[group] = new PoolEdit { ExcludeIds = worldWords.Pools[group].Keys.Where(id => id != member.Id).ToList() };
    }
    Check(catalog.Preview(new NameRequest("worldbox:person", 0, language: 17, inputs: identity), regionNames).Text == localized &&
        catalog.Preview(new NameRequest("worldbox:person", 0, language: 17, inputs: identity, writing: NameWriting.Original), regionNames).Text == original,
        "The regional name order or bilingual rendering changed.");
}

var timer = Stopwatch.StartNew();
int samples = 0;
for (ulong seed = 1; seed <= 10000; seed++)
    if (service.Generate(Request(seed, 13, 9)).Success) samples++;
timer.Stop();
Check(samples == 10000, "Every accepted sample must be non-empty.");
Console.WriteLine($"Core assertions passed. {bundled.Rules.Count} bundled types, {bundled.Words.Count} word IDs. " +
                  $"{lexicons.Length} optional packs with {lexicons.Sum(pack => pack.Pools.Count)} validated pools. " +
                  $"{namingSets.Length} naming sets. " +
                  $"{samples} cached generations in {timer.Elapsed.TotalMilliseconds:F1} ms.");
