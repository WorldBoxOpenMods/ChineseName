using System;
using System.Collections.Generic;
using System.Linq;

namespace Chinese_Name.Naming.Game;

public sealed partial class OnomasticsEditor
{
    private (long Culture, string Type)? _displayedParts;
    private const int PreviewDraws = 41;
    private const int PreviewNames = 12;

    private void RefreshPartPreview(NamingService service, NameType type, Culture culture, PartsPage page, Rule rule, CardNaming choice)
    {
        var key = (culture.id, type.Id);
        if (_displayedParts != key)
        {
            _displayedParts = key;
            page.PreviewRevision++;
        }
        if (page.PreviewRevision == page.RenderedRevision) return;
        page.RenderedRevision = page.PreviewRevision;
        var world = PartsWorld(type, culture, rule, choice);
        var inputs = new Dictionary<string, object>();
        foreach (var field in type.Fields.Values)
        {
            string text = SampleValue(page, rule, field);
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (field.Kind == InputKind.Integer && int.TryParse(text, out int number)) inputs[field.Id] = number;
            else if (field.Kind == InputKind.Flag && bool.TryParse(text, out bool flag)) inputs[field.Id] = flag;
            else inputs[field.Id] = text;
        }
        string surname = rule.Inheritance switch
        {
            SurnameOrder.Father => page.FatherSurname ?? page.MotherSurname,
            SurnameOrder.Mother => page.MotherSurname ?? page.FatherSurname,
            SurnameOrder.Random => (page.Seed & 1) == 0 ? page.FatherSurname ?? page.MotherSurname : page.MotherSurname ?? page.FatherSurname,
            _ => null
        };
        if (UsesCards(choice, rule) && choice.SourceRule == null)
            surname = choice.Inheritance switch
            {
                SurnameOrder.Father => page.FatherSurname ?? page.MotherSurname,
                SurnameOrder.Mother => page.MotherSurname ?? page.FatherSurname,
                SurnameOrder.Random => surname,
                _ => null
            };
        var fixedParts = surname == null ? null : new Dictionary<string, string> { [VanillaNames.Surname] = surname };
        var request = new NameRequest(type.Id, page.Seed, culture.id, page.ReferenceLanguage, inputs, fixedParts);
        page.PreviewSamples.Clear();
        using var draws = service.PreviewRule(request, world, rule.Id, PreviewDraws).GetEnumerator();
        draws.MoveNext();
        page.Result = draws.Current;
        if (!IsPreviewName(page.Result)) return;
        page.PreviewSamples.Add(page.Result);
        var seen = new HashSet<string>(StringComparer.Ordinal) { page.Result.Text };
        while (seen.Count < PreviewNames && draws.MoveNext())
        {
            var result = draws.Current;
            if (IsPreviewName(result) && seen.Add(result.Text)) page.PreviewSamples.Add(result);
        }
    }

    private static bool IsPreviewName(NameResult result) =>
        result?.Success == true && !result.Diagnostics.Contains("中文规则回退");

    private static string SampleValue(PartsPage page, Rule rule, InputField field)
    {
        if (page.Samples.TryGetValue(field.Id, out string value) || rule.When.TryGetValue(field.Id, out value)) return value;
        return field.Example == null ? "" : NameEngine.Value(field.Example);
    }

    private WorldNaming PartsWorld(NameType type, Culture culture, Rule rule, CardNaming choice)
    {
        var world = NamingApi.Current.GetWorld();
        if (!world.Cultures.TryGetValue(culture.id, out var settings)) world.Cultures[culture.id] = settings = new CultureNaming();
        var draft = DraftCulture(culture.id);
        string key = RuleKey(type, rule);
        if (draft.Rules.TryGetValue(key, out var edit)) settings.Rules[key] = NamingJson.Copy(edit);
        else settings.Rules.Remove(key);
        settings.Pack = draft.Pack;
        if (choice != null)
            settings.Cards[type.Id] = NamingJson.Copy(choice);
        else settings.Cards.Remove(type.Id);
        if (settings.Cards.TryGetValue(type.Id, out var cards) && cards.Enabled)
        {
            if (!UsesCards(cards, rule)) cards.Enabled = false;
            else
            {
                cards.Inheritance = rule.Inheritance;
                cards.DraftTemplate = NativeNamingTab.DraftTemplate(culture.id, type.Id) ?? cards.DraftTemplate;
            }
        }
        return world;
    }

    private void ApplyParts(NameType type, Culture culture, PartsPage page, Rule rule, CardNaming choice)
    {
        var service = NamingApi.Current;
        string key = RuleKey(type, rule);
        var world = PartsWorld(type, culture, rule, choice);
        var effective = service.Inspect(new NameRequest(type.Id, page.Seed, culture.id, page.ReferenceLanguage), world);
        if (effective.Diagnostics.Any(issue => (issue.StartsWith("invalid-edit:", StringComparison.Ordinal) ||
                issue.StartsWith("missing-part:", StringComparison.Ordinal)) && issue.IndexOf(key, StringComparison.Ordinal) >= 0))
            throw new InvalidOperationException("请修正当前规则的部件设置。");
        var settings = world.Cultures[culture.id];
        settings.Cards.TryGetValue(type.Id, out var applied);
        if (applied?.Enabled == true)
        {
            if (applied.SourceRule != null && applied.SourceRule != rule.Id)
                throw new InvalidOperationException("请先切换到卡片绑定的规则再应用。");
            if (page.Result == null || !page.Result.Success || page.Result.Diagnostics.Contains("中文规则回退"))
                throw new InvalidOperationException("请修正字形排列后再应用。");
            NativeOnomastics.Apply(culture, type.Id, applied, () => WorldNamingStore.Apply(world));
            DraftCulture(culture.id).Cards[type.Id] = NamingJson.Copy(applied);
        }
        else
        {
            if (applied != null) applied.DraftTemplate = null;
            WorldNamingStore.Apply(world);
            if (applied != null) DraftCulture(culture.id).Cards[type.Id] = NamingJson.Copy(applied);
        }
        if (WorldNamingStore.HasSavedDraft()) SavePartsDraft();
    }

    private void SavePartsDraft()
    {
        foreach (var item in NativeNamingTab.ChangedDrafts())
        {
            var culture = World.world.cultures.get(item.Culture);
            if (culture == null) continue;
            var settings = DraftCulture(item.Culture);
            if (!settings.Cards.TryGetValue(item.Type, out var choice))
                settings.Cards[item.Type] = choice = NativeOnomastics.Selection(_draft, culture, item.Type) ?? new CardNaming();
            choice.DraftTemplate = item.Template;
        }
        WorldNamingStore.SaveDraft(_draft);
    }
}
