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
        var request = PartPreviewRequest(type, culture, page, rule);
        CollectPreview(service, request, PartsWorld(type, culture, rule, choice), page);
    }

    private static NameRequest PartPreviewRequest(NameType type, Culture culture, PartsPage page, Rule rule) =>
        PreviewRequest(type, culture, page, PreviewInputs(type, culture, page, rule));

    private static Dictionary<string, object> PreviewInputs(NameType type, Culture culture, PartsPage page, Rule rule = null)
    {
        var inputs = new Dictionary<string, object>();
        foreach (var field in type.Fields.Values)
        {
            if (NamingApi.Current.IsScopedInput(type.Id, field.Id)) continue;
            string text = SampleValue(page, rule, field);
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (ArrangementExamples.Input(field, text) is { } value) inputs[field.Id] = value;
        }
        WorldNamingInputs.AddInputs(type.Id, null, null, culture.id, page.ReferenceLanguage, null, inputs, preview: true);
        return inputs;
    }

    private static NameRequest PreviewRequest(NameType type, Culture culture, PartsPage page, Dictionary<string, object> inputs) =>
        new NameRequest(type.Id, page.Seed, culture.id, page.ReferenceLanguage, inputs)
            .WithParentSurnames(page.FatherSurname, page.MotherSurname);

    private static void CollectPreview(NamingService service, NameRequest request, WorldNaming world, PartsPage page)
    {
        page.PreviewSamples.Clear();
        using var draws = service.PreviewSamples(request, world, PreviewDraws).GetEnumerator();
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

    private static bool IsPreviewName(NameResult result) => result?.Success == true;

    private static string SampleValue(PartsPage page, Rule rule, InputField field)
    {
        if (page.Samples.TryGetValue(field.Id, out string value) || rule?.When.TryGetValue(field.Id, out value) == true) return value;
        return field.Example == null ? "" : NameEngine.Value(field.Example);
    }

    private WorldNaming PartsWorld(NameType type, Culture culture, Rule rule, CardNaming choice)
    {
        var world = NamingApi.Current.ActiveWorld;
        var settings = world.Cultures.TryGetValue(culture.id, out var previous) ? NamingJson.Copy(previous) : new CultureNaming();
        var draft = DraftCulture(culture.id);
        string key = RuleKey(type, rule);
        if (draft.Rules.TryGetValue(key, out var edit)) settings.Rules[key] = NamingJson.Copy(edit);
        else settings.Rules.Remove(key);
        settings.DefaultArrangementEdits = NamingJson.Copy(draft.DefaultArrangementEdits);
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
                string saved = cards.DraftTemplate ?? cards.Template;
                using var stored = saved == null ? null : NativeNamingArrangement.ReadTemplate(saved);
                if (saved == null || stored != null)
                    cards.DraftTemplate = NativeNamingTab.DraftTemplate(culture.id, type.Id) ?? cards.DraftTemplate;
            }
        }
        return world.WithCulture(culture.id, settings);
    }

    private void ApplyParts(NameType type, Culture culture, PartsPage page, Rule rule, CardNaming choice)
    {
        var service = NamingApi.Current;
        string key = RuleKey(type, rule);
        var world = PartsWorld(type, culture, rule, choice);
        var request = service.PrepareContext(PartPreviewRequest(type, culture, page, rule)) ??
            throw new InvalidOperationException("当前预览情境无效。");
        var effective = service.Inspect(request, world);
        if (effective.Diagnostics.Any(issue => (issue.StartsWith("invalid-edit:", StringComparison.Ordinal) ||
                issue.StartsWith("missing-part:", StringComparison.Ordinal)) && issue.IndexOf(key, StringComparison.Ordinal) >= 0))
            throw new InvalidOperationException("请修正当前规则的部件设置。");
        var settings = world.Cultures[culture.id];
        settings.Cards.TryGetValue(type.Id, out var applied);
        if (applied?.Enabled == true)
        {
            if (applied.SourceRule != null && applied.SourceRule != rule.Id)
                throw new InvalidOperationException("请先切换到卡片绑定的规则再应用。");
            var validation = NativeOnomastics.ValidateDefault(request, effective, culture, applied);
            if (!validation.Success)
            {
                var detail = validation.Diagnostics.FirstOrDefault(issue => issue.StartsWith("词库字形", StringComparison.Ordinal));
                throw new InvalidOperationException(detail ?? PreviewFallback(validation) ?? "请修正字形排列后再应用。");
            }
            var live = service.ActiveWorld;
            var legacy = NativeOnomastics.Selection(live, culture, type.Id);
            using var cards = NativeOnomastics.Read(culture, type.Id, applied);
            var editedRule = effective.AvailableRules.First(candidate => candidate.Id == rule.Id);
            string editKey = CultureNaming.DefaultArrangementKey(type.Id, effective.ArrangementBasis, rule.Id);
            settings.DefaultArrangementEdits.TryGetValue(editKey, out var previous);
            var edit = legacy?.Enabled == true ? null :
                NativeNamingArrangement.CompareDefault(cards, type.Id, editedRule, effective, previous);
            if (edit == null)
            {
                NativeOnomastics.Apply(culture, type.Id, applied, () => WorldNamingStore.SetCulture(culture.id, settings));
                DraftCulture(culture.id).Cards[type.Id] = NamingJson.Copy(applied);
            }
            else
            {
                var draft = DraftCulture(culture.id);
                if (edit.IsEmpty) settings.DefaultArrangementEdits.Remove(editKey);
                else settings.DefaultArrangementEdits[editKey] = edit;
                settings.Cards[type.Id] = new CardNaming();
                WorldNamingStore.SetCulture(culture.id, settings);
                draft.Cards = NamingJson.Copy(settings.Cards);
                draft.DefaultArrangementEdits = NamingJson.Copy(settings.DefaultArrangementEdits);
            }
        }
        else
        {
            if (applied != null) applied.DraftTemplate = null;
            WorldNamingStore.SetCulture(culture.id, settings);
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
