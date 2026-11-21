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

var cache = new NamingService("tests:base");
cache.RegisterType(service.Types.Single());
cache.InstallPack(service.GetPackage("tests:base"));
var hot = Request(culture: 1, language: 1);
var cold = Request(culture: 2, language: 2);
var hotNames = cache.Inspect(hot, null);
var coldNames = cache.Inspect(cold, null);
for (int id = 3; id <= 515; id++)
{
    cache.Inspect(Request(culture: id, language: id), null);
    cache.Inspect(hot, null);
}
Check(ReferenceEquals(hotNames, cache.Inspect(hot, null)) && !ReferenceEquals(coldNames, cache.Inspect(cold, null)),
    "Cache pressure should evict the oldest context and retain recently used ones.");
var beforeScopeEdit = cache.ActiveWorld;
coldNames = cache.Inspect(cold, null);
cache.SetCulture(2, new CultureNaming { Parent = 1 });
Check(!ReferenceEquals(beforeScopeEdit, cache.ActiveWorld) && !beforeScopeEdit.Cultures.ContainsKey(2) &&
    !ReferenceEquals(coldNames, cache.Inspect(cold, null)) && ReferenceEquals(hotNames, cache.Inspect(hot, null)),
    "A culture edit should refresh that culture and leave other resolutions usable.");
cache.SetLanguage(40, new LanguageNaming(), parent: 7);
cache.SetLanguage(42, new LanguageNaming { BiasOrigin = 40 }, parent: 40);
var childRequest = Request(language: 42);
var childNames = cache.Inspect(childRequest, null);
cache.SetLanguage(40, new LanguageNaming(), parent: 7);
Check(ReferenceEquals(childNames, cache.Inspect(childRequest, null)), "Editing a parent's words should preserve the child's snapshot.");
cache.SetLanguage(40, new LanguageNaming(), parent: 8);
Check(!ReferenceEquals(childNames, cache.Inspect(childRequest, null)) && ReferenceEquals(hotNames, cache.Inspect(hot, null)),
    "Changing a bias origin's parent should refresh exactly the dependent contexts.");
const string rejectedContext = "tests:rejected-context", brokenContext = "tests:broken-context";
cache.RegisterType(new NameType(rejectedContext, "无效情境"));
cache.RegisterType(new NameType(brokenContext, "异常情境"));
cache.RegisterContext(rejectedContext, _ => false);
cache.RegisterContext(brokenContext, _ => throw new InvalidOperationException("test callback"));
Check(cache.Generate(new NameRequest(rejectedContext, 0)).Status == GenerationStatus.InvalidInput &&
    cache.Generate(new NameRequest(brokenContext, 0)).Status == GenerationStatus.ContextError &&
    cache.Preview(new NameRequest(brokenContext, 0)).Status == GenerationStatus.ContextError && cache.Generate(hot).Success,
    "A throwing context should be distinguishable from invalid input without interfering with other types.");

const string inheritedType = "tests:inheritance";
var inheritedNames = new NamingService("tests:inheritance");
inheritedNames.RegisterType(new NameType(inheritedType, "姓氏", new[] { new InputField("kind", InputKind.Text) },
    fixedParts: new[] { "surname" }));
inheritedNames.InstallPack(Read("""
    {"format":1,"id":"tests:inheritance","rules":{"tests:inheritance":[
        {"id":"omit","priority":3,"when":{"kind":"no-surname"},"parts":[
            {"id":"surname","literal":"赵","optional":true,"chance":0},{"id":"given","literal":"青"}]},
        {"id":"maternal","priority":2,"inheritance":"Mother","parts":[
            {"id":"surname","literal":"赵"},{"id":"given","pool":"tests:empty"}]},
        {"id":"paternal","priority":1,"inheritance":"Father","parts":[
            {"id":"surname","literal":"赵"},{"id":"given","literal":"青"}]}
    ]}}
    """));
var parents = new NameRequest(inheritedType, 0).WithParentSurnames("欧阳", "李");
var inheritedName = inheritedNames.Generate(parents);
Check(inheritedName.Text == "欧阳青" && inheritedName.RuleId == "paternal" && inheritedName.Sources["surname"].Kind == "inherited",
    "An earlier failed arrangement decided the inherited surname.");
Check(inheritedNames.Preview(parents.WithParentSurnames("", null)).Text == "赵青" &&
    inheritedNames.Generate(new NameRequest(inheritedType, 0, fixedParts: new Dictionary<string, string> { ["surname"] = "" })
        .WithParentSurnames("欧阳", "李")).Text == "青", "An unknown parent and an explicit absence of a surname were conflated.");
Check(inheritedNames.Generate(new NameRequest(inheritedType, 0, inputs: new Dictionary<string, object> { ["kind"] = "no-surname" })
    .WithParentSurnames("欧阳", "李")).Text == "青", "Inheritance added a surname that the winning rule omitted.");

const string orderedType = "tests:ordered-search";
var ordered = new NamingService("tests:ordered-base");
ordered.RegisterType(new NameType(orderedType, "顺序试验", new[] { new InputField("place", InputKind.Text) }));
ordered.InstallPack(Read("""
    {"format":1,"id":"tests:ordered-base","rules":{"tests:ordered-search":[
        {"id":"one","priority":6,"parts":[{"id":"name","pool":"tests:absent"}]},
        {"id":"two","priority":5,"parts":[{"id":"name","pool":"tests:empty"}]},
        {"id":"three","priority":4,"parts":[{"id":"name","input":"place"}]},
        {"id":"four","priority":3,"parts":[{"id":"name","literal":"退回"}]},
        {"id":"five","priority":2,"parts":[{"id":"name","literal":"澄江府"}]},
        {"id":"six","priority":1,"parts":[{"id":"name","literal":"松岭城"}]}
    ]},"pools":{"tests:empty":{}}}
    """));
var tried = ordered.Generate(new NameRequest(orderedType, 1), text => text != "退回");
Check(tried.RuleId == "five" && tried.Text == "澄江府" &&
    tried.Failures.Select(failure => failure.Reason).SequenceEqual(new[] { NameFailureReason.MissingVocabulary,
        NameFailureReason.EmptyVocabulary, NameFailureReason.MissingInput, NameFailureReason.Rejected }),
    "The fifth eligible rule was skipped or earlier failures were lost.");

service.RegisterCultureCondition(type, "kind", "类别", new Dictionary<string, string> { ["sapient"] = "生物" });
service.RegisterCultureCondition(type, "kind", "其他模组的类别", new Dictionary<string, string> { ["spirit"] = "灵体" });
Check(service.Conditions(type)["kind"].Label == "类别" &&
      service.Conditions(type)["kind"].Values.Keys.ToHashSet().SetEquals(new[] { "sapient", "spirit" }),
    "Another mod should be able to extend a registered condition field without replacing its original choices.");
var legacyArrangement = NamingJson.Read<ConditionalArrangement>("""
    {"id":"arrangement-old","when":{"kind":["sapient"]}}
    """);
Check(service.Matches(legacyArrangement, Request()) &&
      service.Matches(NamingJson.Copy(legacyArrangement), Request()),
    "The previous arrangement condition must still match after a JSON round trip.");
legacyArrangement.Disabled = true;
Check(!service.Matches(NamingJson.Copy(legacyArrangement), Request()),
    "An arrangement must stay disabled after a JSON round trip.");

const string logicType = "tests:logic";
var logic = new NameType(logicType, "条件", new[]
{
    new InputField("kind", InputKind.Text, true),
    new InputField("town", InputKind.Text, example: "西城"),
    new InputField("size", InputKind.Integer, example: 3)
});
service.RegisterType(logic);
var grouping = NamingJson.Read<ConditionalArrangement>("""
    {"expression":{"children":[
        {"field":"kind","values":["normal"]},
        {"any":true,"children":[
            {"field":"town","test":"Prefix","values":["临"]},
            {"field":"town","test":"Suffix","values":["港"]}]},
        {"negated":true,"children":[
            {"field":"town","test":"Contains","values":["南岭"]},
            {"field":"size","test":"Less","values":["5"]}],"any":true}]}}
    """);
NameRequest Logic(string kind, string town, int size) =>
    new(logicType, 0, inputs: new Dictionary<string, object> { ["kind"] = kind, ["town"] = town, ["size"] = size });
Check(service.Matches(grouping, Logic("normal", "临安", 5)) &&
      service.Matches(grouping, Logic("normal", "石港", 6)) &&
      !service.Matches(grouping, Logic("other", "石港", 5)) &&
      !service.Matches(grouping, Logic("normal", "石南岭港", 5)) &&
      !service.Matches(grouping, Logic("normal", "石港", 4)),
    "Parentheses and negation must preserve their scope over repeated fields and typed comparisons.");
var groupedTokens = ArrangementSyntax.Tokens(grouping.Expression);
var fromLine = new ConditionalArrangement { Expression = ArrangementSyntax.Parse(groupedTokens) };
Check(service.Matches(fromLine, Logic("normal", "石港", 5)) &&
      !service.Matches(fromLine, Logic("normal", "南岭港", 5)) &&
      ArrangementSyntax.Parse(groupedTokens.Take(groupedTokens.Count - 1).ToArray()) == null,
    "The editable token line must preserve grouping and reject incomplete input.");
var example = new Dictionary<string, object> { ["kind"] = "other", ["town"] = "西城", ["size"] = 3 };
Check(ArrangementExamples.TryApply(service, logic, grouping, example) &&
      service.Matches(grouping, new NameRequest(logicType, 0, inputs: example)),
    "The preview scenario must satisfy the same grouped condition as generation.");
const string scenarioType = "tests:scenario";
var scenarioService = new NamingService("tests:scenario");
var scenario = new NameType(scenarioType, "器物", new[]
{
    new InputField("form", InputKind.Text, example: "ball"), new InputField("stem", InputKind.Text, required: true)
});
scenarioService.RegisterType(scenario);
scenarioService.RegisterContext(scenarioType, inputs =>
{
    if (!inputs.TryGetValue("form", out var form)) return false;
    if (Equals(form, "ball")) inputs["stem"] = "器珠";
    else if (Equals(form, "blade")) inputs["stem"] = "器刃";
    else return false;
    return true;
});
scenarioService.InstallPack(Read("""
    {"format":1,"id":"tests:scenario","rules":{"tests:scenario":[
        {"id":"blade","when":{"form":"blade"},"parts":[{"id":"name","input":"stem"}]}
    ]}}
    """));
var blade = NamingJson.Read<ConditionalArrangement>("""
    {"expression":{"children":[{"field":"form","values":["blade"]},{"field":"stem","values":["器刃"]}]}}
    """);
var rawExample = new Dictionary<string, object> { ["form"] = "ball" };
Check(ArrangementExamples.TryApply(scenarioService, scenario, blade, rawExample) &&
    scenarioService.Preview(new NameRequest(scenarioType, 0, inputs: rawExample)).Text == "器刃" &&
    scenarioService.Generate(new NameRequest(scenarioType, 0, inputs: new Dictionary<string, object> { ["form"] = "blade" })).Text == "器刃" &&
    scenarioService.Generate(new NameRequest(scenarioType, 0)).Status == GenerationStatus.InvalidInput,
    "Derived fields must be available to condition preview and runtime, and invalid combinations must report invalid input.");
var extension = new NamingCategoryExtension(scenarioType);
extension.Input(new InputField("alternate-stem", InputKind.Text, example: "水刃"));
extension.Presence("alternate-stem", "异名", "有异名", "无异名");
extension.InputGlyph("tests:alternate-stem", '\uE550', "alternate-stem", "异名", "ui/Icons/iconBooks");
scenarioService.ExtendCategory(extension);
var extendedType = scenarioService.Types.Single(type => type.Id == scenarioType);
var extendedRequest = new NameRequest(scenarioType, 1,
    inputs: new Dictionary<string, object> { ["form"] = "blade", ["alternate-stem"] = "水刃" });
var alternate = NamingJson.Read<ConditionalArrangement>("""
    {"expression":{"children":[{"field":"alternate-stem","test":"Registered","values":["present"]}]}}
    """);
Check(extendedType.Fields.ContainsKey("alternate-stem") && extendedType.Fields.ContainsKey("stem") &&
    scenarioService.CultureGlyphs["tests:alternate-stem"].ShortCode == '\uE550' &&
    scenarioService.Matches(alternate, extendedRequest) && scenarioService.Generate(extendedRequest).Text == "器刃",
    "Extending a category must expose new inputs and keep existing context preparation and rules.");
var duplicate = new NamingCategoryExtension(scenarioType);
duplicate.Input(new InputField("other-source", InputKind.Text));
duplicate.Presence("other-source", "其他来源", "有", "无");
duplicate.InputGlyph("tests:other-source", '\uE550', "other-source", "其他来源", "ui/Icons/iconBooks");
bool rejectedDuplicate = false;
try { scenarioService.ExtendCategory(duplicate); }
catch (ArgumentException) { rejectedDuplicate = true; }
Check(rejectedDuplicate && !scenarioService.Types.Single(type => type.Id == scenarioType).Fields.ContainsKey("other-source") &&
    !scenarioService.Conditions(scenarioType).ContainsKey("other-source"),
    "Reject a glyph collision before changing the target category.");

var overlay = new NamingPack
{
    Format = 1, Id = "tests:scenario-overlay", Supplemental = true, CultureBases = { "tests:scenario" },
    Arrangements = { [scenarioType] = new() { new ConditionalArrangement
    {
        Id = "alternate", Expression = alternate.Expression, Cards = new CardNaming { Enabled = true, Template = "0|0:异名" }
    } } }
};
Check(scenarioService.Inspect(extendedRequest, null).Arrangements.Count == 0, "The optional arrangement leaked before install.");
scenarioService.RegisterDefaultPack(overlay);
var extendedNames = scenarioService.Inspect(extendedRequest, null);
Check(extendedNames.Arrangements.Single().Id == "alternate" &&
    extendedNames.ArrangementOwners["alternate"].Key == "tests:scenario-overlay/alternate",
    "A supplemental arrangement must keep its owner's identity when it augments the selected preset.");
var singleClause = new ConditionalArrangement
{
    Expression = new ArrangementCondition { Field = "alternate-stem", Test = ArrangementTest.Present }
};
Check(scenarioService.Matches(NamingJson.Copy(singleClause), extendedRequest),
    "A standalone clause must survive a save without becoming an empty condition group.");
var legacyClauses = NamingJson.Read<ConditionalArrangement>("""
    {"conditions":{"kind":{"values":["normal"]},"size":{"test":"Present"}}}
    """);
Check(service.Matches(NamingJson.Copy(legacyClauses), Logic("normal", "西城", 3)) &&
      !service.Matches(legacyClauses, Logic("other", "西城", 3)),
    "The former flat list must still require all its conditions.");
var deepest = new ArrangementCondition { Field = "kind", Values = new() { "normal" } };
for (int depth = 0; depth < ArrangementCondition.MaxDepth; depth++)
    deepest = new ArrangementCondition { Children = new() { deepest } };
var deepPack = new NamingPack
{
    Format = 1, Id = "tests:grouped", Arrangements =
    {
        [logicType] = new() { new ConditionalArrangement
        {
            Id = "deep", Expression = deepest, Cards = new CardNaming { Enabled = true, Template = "0|0:关" }
        } }
    }
};
var deepWorld = new WorldNaming { AppliedPresets = { [deepPack.Id] = deepPack } };
var restoredPack = NamingJson.Read<WorldNaming>(NamingJson.Write(deepWorld)).AppliedPresets[deepPack.Id];
service.InstallPack(restoredPack);
Check(service.Matches(restoredPack.Arrangements[logicType].Single(), Logic("normal", "西城", 3)),
    "The deepest supported expression must survive preset validation and the world JSON depth limit.");

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

const string cityType = "worldbox:city";
var places = Read(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "place-history.naming.json")));
var history = new NamingService(bundled.Id);
history.RegisterType(new NameType(cityType, "城市", new[] { new InputField("landform", InputKind.Text) }));
const string historyType = "worldbox:book";
history.RegisterType(new NameType(historyType, "书籍", new[] { "topic", "author-city", "historical-event", "historical-event-kind" }
    .Select(field => new InputField(field, InputKind.Text))));
history.InstallPack(bundled);
history.InstallPack(places);
var timeline = new WorldNaming { EnabledPacks = new List<string> { places.Id } };
timeline.Cultures[1] = new CultureNaming { Pack = places.Id };
string BookArrangement(string topic, string eventKind = null)
{
    var inputs = new Dictionary<string, object> { ["topic"] = topic, ["author-city"] = "澄江" };
    if (eventKind != null)
    {
        inputs["historical-event"] = "古事";
        inputs["historical-event-kind"] = eventKind;
    }
    var request = new NameRequest(historyType, 0, culture: 1, inputs: inputs);
    return history.Inspect(request, timeline).Arrangements.FirstOrDefault(value => history.Matches(value, request))?.Id;
}
Check(BookArrangement("history", "war") == "city-annals", "Incident arrangements leaked into the base preset.");
var incidentPack = Read(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "history-events.naming.json")));
history.RegisterDefaultPack(incidentPack);
foreach (string eventKind in new[] { "war", "rebellion", "migration", "founding" })
    Check(BookArrangement("history", eventKind) == eventKind + "-annals", "The local annals eclipsed an incident: " + eventKind);
Check(BookArrangement("history") == "city-annals" && BookArrangement("fable", "war") == null,
    "A non-historical book or a book without an event used an incident arrangement.");
timeline.Cultures[1].ArrangementEdits[historyType] = new ArrangementEdits
{
    Changes = { [places.Id + "/rebellion-annals"] = new ArrangementEdit { Template = "\uE12A0|0:实录" } }
};
var incidentNames = history.Inspect(new NameRequest(historyType, 0, culture: 1), timeline);
Check(incidentNames.Arrangements.Single(row => row.Id == "rebellion-annals").Cards.Template == "\uE12A0|0:实录",
    "Moving the incident arrangement into the addon lost its existing culture edit.");
foreach (var (id, parent, landform, arrangement) in new[]
{
    (2L, 1L, "coast", "harbor-towns"), (3L, 2L, "island", "island-towns"),
    (4L, 1L, "river-valley", "river-crossings"), (5L, 1L, "mountain", "mountain-passes")
})
{
    var child = timeline.Cultures[id] = NamingJson.Copy(timeline.Cultures[parent]);
    Check(history.EvolveCulture(child, timeline, (ulong)id, landform), "A culture did not adopt its local arrangement.");
    var request = new NameRequest(cityType, 1, culture: id,
        inputs: new Dictionary<string, object> { ["landform"] = landform });
    Check(history.Inspect(request, timeline).Arrangements.First(item => history.Matches(item, request)).Id == arrangement,
        "The chosen arrangement does not match its local terrain.");
    Check(!history.EvolveCulture(child, timeline, (ulong)id, landform), "Repeated splits duplicated the same arrangement.");
}
var port = new NameRequest(cityType, 1, culture: 3, inputs: new Dictionary<string, object> { ["landform"] = "coast" });
Check(history.Inspect(port, NamingJson.Copy(timeline)).Arrangements.First(item => history.Matches(item, port)).Id == "harbor-towns"
    && timeline.Cultures[2].ArrangementVariants[cityType].SequenceEqual(new[] { places.Id + "/harbor-towns" })
    && timeline.Cultures[1].ArrangementVariants.Count == 0,
    "The island branch lost its inherited coast arrangement or changed an ancestor.");
var previousCoast = new CultureNaming { Pack = places.Id,
    Variants = new Dictionary<string, string> { [cityType] = places.Id + "/harbor-towns" } };
Check(history.EvolveCulture(previousCoast, timeline, 6, "island") &&
    previousCoast.ArrangementVariants[cityType].SequenceEqual(new[] { places.Id + "/island-towns", places.Id + "/harbor-towns" }),
    "The previous single-variant save must keep its coastal arrangement after a later split.");
var editedCity = NamingJson.Copy(timeline.Cultures[2]);
editedCity.Arrangements[cityType] = NamingJson.Copy(places.Arrangements[cityType]);
Check(!history.EvolveCulture(editedCity, timeline, 7, "island") &&
    NamingJson.Write(editedCity.Arrangements[cityType]) == NamingJson.Write(places.Arrangements[cityType]),
    "An island variant superseded a hand-edited arrangement.");

var arranging = new NamingService(bundled.Id);
arranging.RegisterType(new NameType(cityType, "城市", new[] { new InputField("landform", InputKind.Text) }));
arranging.InstallPack(bundled);
ConditionalArrangement Starter(string id, string landform, string text) => new()
{
    Id = id, Cards = new CardNaming { Enabled = true, Template = "0|0:" + text },
    Expression = new ArrangementCondition
    {
        Children = new() { new() { Field = "landform", Test = ArrangementTest.Equal, Values = new() { landform } } }
    }
};
var changingPack = new NamingPack { Format = 1, Id = "tests:changing-style" };
changingPack.Arrangements[cityType] = new() { Starter("river", "river-valley", "澄江"), Starter("port", "coast", "潮口"),
    Starter("removed", "plain", "平原") };
arranging.InstallPack(changingPack);
var settings = new WorldNaming { EnabledPacks = new() { changingPack.Id } };
var customCity = settings.Cultures[38] = new CultureNaming { Pack = changingPack.Id };
var requestCity = new NameRequest(cityType, 0, culture: 38);
var before = arranging.Inspect(requestCity, settings);
var changes = new ArrangementEdits();
var changed = before.Arrangements.Take(2).Select(NamingJson.Copy).ToList();
changed[0].Cards.Template = "0|0:澄江府";
changed[1].Disabled = true;
changed.Reverse();
changed.Add(Starter("personal", "mountain", "云关"));
changes.Update(before, changed, reorder: true);
customCity.ArrangementEdits[cityType] = changes;
settings = NamingJson.Copy(settings);
arranging.UninstallPack(changingPack.Id);
Check(arranging.Inspect(requestCity, settings).Arrangements.Single().Id == "personal",
    "Uninstalling the preset removed a local arrangement or treated an override as a local arrangement.");
changingPack.Arrangements[cityType] = new() { Starter("river", "basin", "澄溪"), Starter("island", "island", "海邑"),
    Starter("port", "coast", "沧港"), Starter("removed", "plain", "绿洲") };
arranging.InstallPack(changingPack);
var after = arranging.Inspect(requestCity, settings).Arrangements;
Check(after.Select(item => item.Id).SequenceEqual(new[] { "island", "port", "river", "personal" }) &&
    after[1].Disabled && after[1].Cards.Template == "0|0:沧港" &&
    after[2].Cards.Template == "0|0:澄江府" && after[2].Expression.Children.Single().Values.Single() == "basin",
    "A preset update froze unedited fields, restored a deletion, or lost relative order.");
var reedNames = new NamingPack { Format = 1, Id = "tests:reed-names", Supplemental = true };
reedNames.CultureVariants["coast"] = new CultureVariant { Type = cityType, Arrangement = Starter("river", "coast", "苇港") };
arranging.InstallPack(reedNames);
settings.EnabledPacks.Add(reedNames.Id);
settings.Cultures[38].ArrangementVariants[cityType] = new() { reedNames.Id + "/coast" };
var fromReeds = arranging.Inspect(requestCity, settings).Arrangements.Single(item => item.Id == "river");
Check(fromReeds.Cards.Template == "0|0:苇港", "An edit intended for a base row leaked into a different pack's row with the same ID.");
settings.EnabledPacks.Remove(reedNames.Id);
Check(arranging.Inspect(requestCity, settings).Arrangements.Single(item => item.Id == "river").Cards.Template == "0|0:澄江府",
    "Removing the variant did not restore the base row's own edit.");
var preserved = NamingJson.Read<ArrangementEdit>("""{"template":"0|0:旧整条"}""");
Check(preserved.Template == "0|0:旧整条" && preserved.Layout == null, "Previous full-template edits must remain readable.");

var originalLayout = new ArrangementLayout(new[] { "one", "two" }, new() { ["one"] = "溪", ["two"] = "府" });
var editedLayout = new ArrangementLayout(new[] { "two", "one" }, new() { ["one"] = "澄江", ["two"] = "府" });
var semanticEdit = originalLayout.Changes(editedLayout, null, new Dictionary<string, string> { ["one"] = "root", ["two"] = "suffix" });
var newLayout = new ArrangementLayout(new[] { "two", "one" }, new() { ["one"] = "驿", ["two"] = "涧" });
var newRoles = new Dictionary<string, string> { ["two"] = "river", ["one"] = "suffix" };
var unresolved = new HashSet<string>();
var dormant = newLayout.Apply(semanticEdit, newRoles, unresolved: part => unresolved.Add(part));
Check(unresolved.SetEquals(new[] { "root" }) && dormant.Glyphs.SequenceEqual(newLayout.Glyphs) && dormant.WordGroup("one") == "驿",
    "An unmapped semantic edit should preserve the new default slots and order.");
var partRenames = new Dictionary<string, string> { ["root"] = "river" };
var rebound = newLayout.Apply(semanticEdit, newRoles, partRenames);
Check(rebound.Glyphs.SequenceEqual(new[] { "one", "two" }) && rebound.WordGroup("two") == "澄江" && rebound.WordGroup("one") == "驿",
    "A renamed part should carry its text and place in the edited order to its new slot.");
var resetGroup = new ArrangementLayout(rebound.Glyphs, newLayout.Groups);
var retainedOrder = newLayout.Changes(resetGroup, semanticEdit, newRoles, partRenames);
Check(retainedOrder.PartGroups.Count == 0 && retainedOrder.GlyphParts.Count == 2,
    "Resetting a migrated word group should remove the old override without resetting the order.");
Check(!PackageMigrations.Valid(new Dictionary<string, string> { ["first"] = "second", ["second"] = "first" }, Ids.Local),
    "A declared migration cycle should be rejected.");
settings.Cultures[38].Arrangements[cityType] = new() { Starter("legacy", "coast", "旧名") };
Check(arranging.Inspect(requestCity, settings).Arrangements.Single().Id == "legacy",
    "An older full-list override must take precedence.");

var lexicons = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Lexicons"), "*.naming.json")
    .Select(file => Read(File.ReadAllText(file))).ToArray();
Check(lexicons.Length != 0, "Optional lexicon fixtures must be present.");
var catalog = new NamingService(bundled.Id);
const string city = "worldbox:city";
var context = new[] { "species", "kind", "culture-species", "founder-surname", "attacker", "defender",
    "origin", "founder-short", "partner-short", "station", "topic", "material", "equipment", "stage", "plot", "landform" }
    .Select(id => new InputField(id, InputKind.Text)).ToArray();
catalog.RegisterType(new NameType("worldbox:person", "姓名", context, fixedParts: new[] { "surname" }));
var societyCategories = new[] { "city", "kingdom", "culture", "language", "family", "clan", "war", "alliance", "army", "book", "motto", "item" };
foreach (string category in societyCategories.Concat(new[] { "religion", "subspecies", "plot" }))
    catalog.RegisterType(new NameType("worldbox:" + category, category, context));
catalog.InstallPack(bundled);
const string allianceType = "worldbox:alliance";
var namedAlliance = catalog.Generate(new NameRequest(allianceType, 7, inputs: new Dictionary<string, object>
{
    ["kind"] = "founding-pair", ["founder-short"] = "临川", ["partner-short"] = "松岭"
}));
Check(namedAlliance.Success && namedAlliance.Text.StartsWith("临川松岭"), "An alliance should name both founding kingdoms.");
Check(catalog.Generate(new NameRequest(allianceType, 7)).Success, "An alliance without context should still have a name.");
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

foreach (var (packageName, count) in new[] { ("wafuu-geography", 3), ("fantasy-geography", 12) })
{
    var geography = Read(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Geography", packageName + ".naming.json")));
    catalog.InstallPack(geography);
    var bindings = NamingJson.Copy(enabledLexicons);
    string basis = geography.CultureBases.Single();
    bindings.EnabledPacks.AddRange(new[] { basis, geography.Id });
    Check(geography.CultureVariants.Count == count && geography.WordGroups.Count == count, "Missing regional content: " + packageName);
    foreach (var (key, variant) in geography.CultureVariants)
    foreach (string landform in variant.Landforms)
    {
        string species = variant.Species.SingleOrDefault() ?? "human";
        var culture = bindings.Cultures[1] = new CultureNaming { Pack = basis };
        var request = new NameRequest(cityType, 7, culture: 1, inputs: new Dictionary<string, object>
        {
            ["landform"] = landform, ["culture-species"] = species
        });
        Check(catalog.EvolveCulture(culture, bindings, 7, landform, species), "Missing eligible geography variant: " + key);
        var regional = catalog.Inspect(request, bindings);
        var group = geography.WordGroups[key];
        var candidates = regional.GetPool(group.Pool).Select(word => word.Id).ToHashSet();
        Check(regional.Arrangements.First(item => catalog.Matches(item, request)).Id == key &&
            group.Landforms.SequenceEqual(variant.Landforms) && group.Words.Count < candidates.Count &&
            group.Words.All(candidates.Contains), "A geographic arrangement does not match its culture or vocabulary: " + key);
    }
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
