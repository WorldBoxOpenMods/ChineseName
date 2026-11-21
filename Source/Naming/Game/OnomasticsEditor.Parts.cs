using System;
using System.Collections.Generic;
using System.Linq;

namespace Chinese_Name.Naming.Game;

public sealed partial class OnomasticsEditor
{
    private readonly Dictionary<(long Culture, string Type), PartsPage> _partEditor = new();

    private sealed class PartsPage
    {
        internal EffectiveNames Effective;
        internal long? ReferenceLanguage;
        internal float? LanguagePosition;
        internal string FatherSurname;
        internal string MotherSurname;
        internal ulong Seed;
        internal string Message;
        internal readonly Dictionary<string, string> Samples = new();
        internal NameResult Result;
        internal readonly List<NameResult> PreviewSamples = new();
        internal int PreviewRevision;
        internal int RenderedRevision = -1;
    }

    private PartsPage Page(Culture culture, NameType type)
    {
        var key = (culture.id, type.Id);
        if (_partEditor.TryGetValue(key, out var page)) return page;
        var selected = SelectedUnit.unit;
        var language = type.Id == NativeOnomastics.City && SelectedMetas.selected_city is { } city && city.culture == culture
            ? city.language : selected?.culture == culture ? selected.language : null;
        page = new PartsPage { ReferenceLanguage = language?.id ??
            culture.units.FirstOrDefault(actor => actor.language != null)?.language.id };
        var example = selected?.culture == culture && selected.isSapient() ? selected : culture.units.FirstOrDefault(actor => actor.isSapient());
        page.Samples["species"] = example?.asset.id ?? culture.data.original_actor_asset ?? "human";
        if (type.Fields.ContainsKey(VanillaNames.CultureSpecies) && culture.data.original_actor_asset is string origin)
            page.Samples[VanillaNames.CultureSpecies] = origin;
        if (type.Fields.ContainsKey("origin") && type.Id is ("worldbox:clan" or "worldbox:culture" or
                "worldbox:language" or "worldbox:religion" or NativeOnomastics.City) && example != null)
        {
            var sample = VanillaNames.Request(type.Id, example, 0);
            if (sample.Inputs.TryGetValue("origin", out var name) && name is string source) page.Samples["origin"] = source;
        }
        if (type.Id == NativeOnomastics.City && SelectedMetas.selected_city is { } namingCity && namingCity.culture == culture)
            foreach (var input in VanillaNames.CityInputs(namingCity))
                if (input.Value is string value) page.Samples[input.Key] = value;
        string exampleKind = NativeOnomastics.Meta(type.Id).HasValue ? "sapient" :
            NamingApi.Current.ExampleKind(type.Id);
        if (exampleKind != null) page.Samples["kind"] = exampleKind;
        if (type.Id == VanillaNames.Person)
        {
            foreach (var actor in culture.units.Where(actor => actor.isSapient()))
            {
                if (VanillaNames.SurnameOf(actor) is not { Length: > 0 } surname) continue;
                if (actor.data.sex == ActorSex.Male) page.FatherSurname ??= surname;
                if (actor.data.sex == ActorSex.Female) page.MotherSurname ??= surname;
                if (page.FatherSurname != null && page.MotherSurname != null) break;
            }
            if (selected?.culture == culture && VanillaNames.SurnameOf(selected) is { Length: > 0 } preferred)
            {
                if (selected.data.sex == ActorSex.Male) page.FatherSurname = preferred;
                if (selected.data.sex == ActorSex.Female) page.MotherSurname = preferred;
            }
        }
        _partEditor.Add(key, page);
        return page;
    }

    private CultureNaming DraftCulture(long id)
    {
        if (!_draft.Cultures.TryGetValue(id, out var settings)) _draft.Cultures[id] = settings = new CultureNaming();
        return settings;
    }

    private static string RuleKey(NameType type, Rule rule) => type.Id + "/" + rule.Id;

    private static bool UsesCards(CardNaming choice, Rule rule) =>
        choice?.Enabled == true && rule != null && (choice.SourceRule == null || choice.SourceRule == rule.Id);

    private Rule SelectedRule(NamingService service, NameType type, Culture culture, PartsPage page)
    {
        var sample = PreviewRequest(type, culture, page, PreviewInputs(type, page));
        var request = service.PrepareContext(sample) ?? sample;
        page.Effective = service.Inspect(request, _draft);
        var effective = page.Effective;
        string cardRule = NativeOnomastics.Selection(_draft, culture, type.Id) is { Enabled: true } cards ? cards.SourceRule : null;
        return effective.AvailableRules.FirstOrDefault(item => item.Id == cardRule) ??
            NativeOnomastics.MatchedRule(request, effective) ??
            effective.Rules.FirstOrDefault() ?? effective.AvailableRules.FirstOrDefault();
    }

}
