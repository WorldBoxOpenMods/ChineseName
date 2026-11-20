using System;
using System.Collections.Generic;
using System.Linq;

namespace Chinese_Name.Naming.Game;

internal static class WorldNamingReassignment
{
    internal static void ApplyStyle(string pack)
    {
        var service = NamingApi.Current;
        var world = service.GetWorld();
        if (pack != null && (!service.ActivePacks(world).TryGetValue(pack, out var selected) || selected.Supplemental))
            throw new InvalidOperationException("这个命名预设当前不可用。");
        Reset(world, pack);
        WorldNamingStore.Apply(world);
        if (!WorldNamingStore.HasSavedDraft()) return;
        var draft = WorldNamingStore.GetSavedDraft();
        Reset(draft, pack);
        WorldNamingStore.SaveDraft(draft);
    }

    private static void Reset(WorldNaming world, string pack)
    {
        world.UseSavedVanillaCards = false;
        world.CulturePack = pack;
        world.AppliedPresets.Clear();
        foreach (var culture in world.Cultures.Values)
        {
            culture.Pack = null;
            ClearVanilla(culture.Cards);
            ClearVanilla(culture.Rules);
            ClearVanilla(culture.Presets);
            ClearVanilla(culture.Variants);
        }
    }

    private static void ClearVanilla<T>(Dictionary<string, T> entries)
    {
        foreach (string key in entries.Keys.Where(key => key.StartsWith("worldbox:", StringComparison.Ordinal)).ToArray())
            entries.Remove(key);
    }

    internal static (int Changed, int Failed) RenameExisting(bool includeWorld)
    {
        var world = World.world;
        int changed = includeWorld ? IndependentNames.AssignWorldNames(world.map_stats) : 0;
        int failed = 0;
        void Record(NanoObject subject, string before, bool success)
        {
            if (!success) failed++;
            else if (before != subject.name) changed++;
        }
        void Assign(NanoObject subject, BaseSystemData data, Func<string> generate)
        {
            if (data.custom_name) return;
            string before = data.name;
            string after = generate();
            if (after != null && before != after) subject.setName(after);
            Record(subject, before, after != null);
        }
        string Generate(string type, BaseSystemData data, Actor actor = null, Culture culture = null,
            Language language = null, string species = null)
        {
            var inputs = new Dictionary<string, object>();
            if (species != null) inputs["species"] = inputs[VanillaNames.CultureSpecies] = species;
            return VanillaNames.Generate(type, actor, IndependentNames.Seed(data.id),
                culture: culture?.id ?? actor?.culture?.id ?? (data.name_culture_id >= 0 ? data.name_culture_id : null),
                language: language?.id, inputs: inputs);
        }
        Actor Member(long id, IEnumerable<Actor> members) => world.units.get(id) ?? members.FirstOrDefault();

        foreach (var actor in world.units)
        {
            if (actor.data.custom_name) continue;
            string before = actor.data.name;
            string marker = actor.asset.id == "angle" ? SpecialNames.AngleMarker(before) : null;
            if (marker != null) Assign(actor, actor.data, () => marker);
            else Record(actor, before, VanillaNames.TryNamePerson(actor));
        }
        foreach (var species in world.subspecies.OrderBy(entry => entry.id))
        {
            if (species.data.custom_name || species.name == SpecialNames.VoidElves) continue;
            string before = species.name;
            if (before == SpecialNames.VanillaVoidElves) Assign(species, species.data, () => SpecialNames.VoidElves);
            else Record(species, before, IndependentNames.SubspeciesName.TryAssign(species, species.getActorAsset(), species.data.biome_variant));
        }
        foreach (var city in world.cities)
            Assign(city, city.data, () => Generate("worldbox:city", city.data, city.leader ?? world.units.get(city.data.founder_id),
                city.culture, city.language, city.data.original_actor_asset));
        foreach (var kingdom in world.kingdoms)
            Assign(kingdom, kingdom.data, () => Generate("worldbox:kingdom", kingdom.data, kingdom.king,
                kingdom.culture, kingdom.language, kingdom.data.original_actor_asset));
        foreach (var culture in world.cultures)
            Assign(culture, culture.data, () => Generate("worldbox:culture", culture.data,
                Member(culture.data.creator_id, culture.units), culture, species: culture.data.original_actor_asset));
        foreach (var language in world.languages)
            Assign(language, language.data, () => Generate("worldbox:language", language.data,
                Member(language.data.creator_id, language.units), language: language));
        foreach (var religion in world.religions)
            Assign(religion, religion.data, () => Generate("worldbox:religion", religion.data,
                Member(religion.data.creator_id, religion.units)));
        foreach (var clan in world.clans)
            Assign(clan, clan.data, () => Generate("worldbox:clan", clan.data, clan.getChief() ??
                Member(clan.data.founder_actor_id, clan.units), species: clan.data.original_actor_asset));
        foreach (var family in world.families)
            Assign(family, family.data, () => Generate("worldbox:family", family.data, family.getFounderFirst() ??
                family.getFounderSecond() ?? family.units.FirstOrDefault()));
        foreach (var alliance in world.alliances)
            Assign(alliance, alliance.data, () => IndependentNames.FromKingdom("worldbox:alliance",
                world.kingdoms.get(alliance.data.founder_kingdom_id), alliance.id));
        foreach (var war in world.wars)
            Assign(war, war.data, () => IndependentNames.GenerateWar(war.getMainAttacker(), war.getMainDefender(), war.getAsset(), war.id));
        foreach (var army in world.armies.OrderBy(entry => entry.id))
            if (!army.data.custom_name) Record(army, army.name, IndependentNames.ArmyName.TryAssign(army, army.getKingdom()));
        foreach (var plot in world.plots)
            Assign(plot, plot.data, () => IndependentNames.GeneratePlot(plot, plot.getAuthor(), plot.getAsset()));
        foreach (var book in world.books)
            if (book.getAsset()?.name_template is string template && template.StartsWith("book_name_", StringComparison.Ordinal))
                Assign(book, book.data, () => IndependentNames.GenerateBook(template, book, world.units.get(book.data.author_id),
                    book.data.culture_id >= 0 ? book.data.culture_id : null, book.data.language_id >= 0 ? book.data.language_id : null));
        foreach (var item in world.items)
            if (!string.IsNullOrEmpty(item.data.name))
                Assign(item, item.data, () => IndependentNames.GenerateExistingItem(item));
        return (changed, failed);
    }
}
