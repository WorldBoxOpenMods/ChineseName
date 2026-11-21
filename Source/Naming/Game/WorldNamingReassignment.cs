using System;
using System.Collections.Generic;
using System.Linq;

namespace Chinese_Name.Naming.Game;

internal static class WorldNamingReassignment
{
    internal static void ApplyStyle(string pack)
    {
        ApplyStyle(PrepareStyle(NamingApi.Current, pack), pack);
    }

    private static WorldNaming PrepareStyle(NamingService service, string pack)
    {
        var world = service.GetWorld();
        if (pack != null && (!service.ActivePacks(world).TryGetValue(pack, out var selected) || selected.Supplemental))
            throw new InvalidOperationException("这个命名预设当前不可用。");
        Reset(world, pack);
        return world;
    }

    private static void ApplyStyle(WorldNaming world, string pack)
    {
        WorldNamingStore.Apply(world);
        NativeNamingTab.ForgetDrafts((_, type) => IsVanilla(type));
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
            ClearVanilla(culture.Arrangements);
            ClearVanilla(culture.ArrangementEdits);
            ClearVanilla(culture.DefaultArrangementEdits);
            ClearVanilla(culture.Rules);
            ClearVanilla(culture.Presets);
            ClearVanilla(culture.Variants);
            ClearVanilla(culture.ArrangementVariants);
        }
    }

    private static void ClearVanilla<T>(Dictionary<string, T> entries)
    {
        foreach (string key in entries.Keys.Where(IsVanilla).ToArray())
            entries.Remove(key);
    }

    private static bool IsVanilla(string type) => type.StartsWith("worldbox:", StringComparison.Ordinal);

    private static bool NeedsRepair(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        bool latin = false;
        for (int index = 0; index < name.Length; index++)
        {
            if (!char.IsLetter(name, index)) continue;
            if (name[index] is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z' or
                >= '\u00C0' and <= '\u024F' or >= '\u1E00' and <= '\u1EFF' or
                >= '\uFF21' and <= '\uFF3A' or >= '\uFF41' and <= '\uFF5A')) return false;
            latin = true;
        }
        return latin;
    }

    private static bool ContainsHan(string name)
    {
        for (int index = 0; index < name.Length; index++)
        {
            char character = name[index];
            if (character is >= '\u3400' and <= '\u9FFF' or >= '\uF900' and <= '\uFAFF') return true;
            if (char.IsSurrogatePair(name, index) && char.ConvertToUtf32(name, index) is >= 0x20000 and <= 0x323AF)
                return true;
        }
        return false;
    }

    private static bool CanRename(BaseSystemData data, bool repairOnly) =>
        !data.custom_name && (!repairOnly || NeedsRepair(data.name));

    internal sealed class Plan
    {
        private readonly MapStats _map;
        private readonly WorldNaming _active;
        private readonly WorldNaming _draft;
        private readonly string _pack;
        private readonly Dictionary<string, NamingPack> _catalog;
        private readonly Dictionary<long, KingdomNames.Calculated> _countryNames = new();
        private readonly (Kingdom Kingdom, string Before, string Short, bool Custom, NameRequest Request)[] _countries;
        private readonly (Alliance Alliance, string Before, bool Custom, NameRequest Request, string After)[] _alliances;
        private readonly Change<City>[] _cities;
        private readonly Change<Actor>[] _actors;
        private readonly Change<War>[] _wars;
        private readonly List<OtherChanges> _others = new();
        private readonly int _manual;

        private sealed class OtherChanges(Func<bool> stale, Func<int> apply, int changed, int failed)
        {
            internal readonly Func<bool> Stale = stale;
            internal readonly Func<int> Apply = apply;
            internal readonly int Changed = changed, Failed = failed;
        }

        private sealed class Change<T>(T subject, string before, NameRequest request, NameResult result, string literal)
            where T : NanoObject
        {
            internal T Subject { get; } = subject;
            internal long Id { get; } = subject.id;
            internal string Before { get; } = before;
            internal NameRequest Request { get; } = request;
            internal NameResult Result { get; } = result;
            internal string After { get; } = literal ?? (result?.Success == true ? result.Text : null);
        }

        internal Plan(string pack, bool includeWorld, bool repairOnly)
        {
            var world = World.world;
            var service = NamingApi.Current;
            _map = world.map_stats;
            _active = service.ActiveWorld;
            _draft = PrepareStyle(service, pack);
            _pack = pack;
            _catalog = service.PackageCatalog();
            Func<string, bool> accept = repairOnly ? ContainsHan : null;
            var effective = new Dictionary<(string, long?, long?), EffectiveNames>();

            NameResult Generate(NameRequest request) => Draw(request, accept);
            NameResult Draw(NameRequest request, Func<string, bool> allowed)
            {
                var key = (request.Type, request.Culture, request.Language);
                if (!effective.TryGetValue(key, out var names)) effective[key] = names = service.Inspect(request, _draft);
                return service.GenerateForWorld(request, _draft, allowed, names);
            }

            _countries = world.kingdoms.OrderBy(kingdom => kingdom.id).Select(kingdom =>
            {
                bool eligible = CanRename(kingdom.data, repairOnly);
                var request = eligible ? CountryRequest(kingdom) : null;
                if (eligible && KingdomNames.Calculate(request, Generate) is { } name)
                    _countryNames.Add(kingdom.id, name);
                return (kingdom, kingdom.name, KingdomNames.Short(kingdom), kingdom.data.custom_name, request);
            }).ToArray();
            _alliances = world.alliances.OrderBy(alliance => alliance.id).Select(alliance =>
            {
                var request = CanRename(alliance.data, repairOnly) ? AllianceNames.Request(alliance, projected: _countryNames) : null;
                return (alliance, alliance.name, alliance.data.custom_name, request,
                    request == null ? null : AllianceNames.Calculate(request, Generate, accept));
            }).ToArray();
            int manual = 0;
            Change<T>[] Capture<T>(IEnumerable<T> subjects, Func<T, BaseSystemData> dataFor,
                Func<T, NameRequest> requestFor, Func<T, string> literalFor = null,
                Func<T, string, bool> unique = null, Action<T, string> reserve = null) where T : NanoObject
            {
                var changes = new List<Change<T>>();
                foreach (T subject in subjects)
                {
                    var data = dataFor(subject);
                    if (data.custom_name) manual++;
                    if (!CanRename(data, repairOnly)) continue;
                    var request = requestFor(subject);
                    string literal = literalFor?.Invoke(subject);
                    Func<string, bool> allowed = unique == null ? accept :
                        candidate => unique(subject, candidate) && (accept == null || accept(candidate));
                    var result = request == null || literal != null ? null : Draw(request, allowed);
                    var change = new Change<T>(subject, data.name, request, result, literal);
                    changes.Add(change);
                    if (change.After != null) reserve?.Invoke(subject, change.After);
                }
                return changes.ToArray();
            }

            _cities = Capture(world.cities, city => city.data, CityRequest);
            _actors = Capture(world.units, actor => actor.data, VanillaNames.PersonRequest,
                actor => actor.asset.id == "angle" ? SpecialNames.AngleMarker(actor.data.name) : null);
            _wars = Capture(world.wars, war => war.data, WarRequest);
            if (includeWorld && !repairOnly)
            {
                PlanTitle("worldbox:world", () => _map.name, name => _map.name = name);
                PlanTitle("worldbox:player", () => _map.player_name, name => _map.player_name = name);
            }

            var priorSpecies = world.subspecies.ToLookup(species => species.name, StringComparer.OrdinalIgnoreCase);
            var reservedSpecies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var renamedSpecies = new HashSet<long>();
            PlanObjects(() => world.subspecies.Where(species => species.name != SpecialNames.VoidElves).OrderBy(species => species.id),
                species => species.data, species => IndependentNames.SubspeciesName.Request(species, species.getActorAsset(), species.data.biome_variant),
                species => species.name == SpecialNames.VanillaVoidElves ? SpecialNames.VoidElves : null,
                (species, candidate) => !reservedSpecies.Contains(candidate) &&
                    !priorSpecies[candidate].Any(other => other != species && !renamedSpecies.Contains(other.id)),
                (species, name) => { reservedSpecies.Add(name); renamedSpecies.Add(species.id); });

            PlanObjects(() => world.cultures, culture => culture.data, culture =>
                SocietyRequest("worldbox:culture", culture, culture.data, Member(culture.data.creator_id, culture.units),
                    culture, species: culture.data.original_actor_asset));
            PlanObjects(() => world.languages, language => language.data, language =>
                SocietyRequest("worldbox:language", language, language.data, Member(language.data.creator_id, language.units), language: language));
            PlanObjects(() => world.religions, religion => religion.data, religion =>
                SocietyRequest("worldbox:religion", religion, religion.data, Member(religion.data.creator_id, religion.units)));
            PlanObjects(() => world.clans, clan => clan.data, clan =>
                SocietyRequest("worldbox:clan", clan, clan.data, clan.getChief() ?? Member(clan.data.founder_actor_id, clan.units),
                    species: clan.data.original_actor_asset));
            PlanObjects(() => world.families, family => family.data, family =>
                SocietyRequest("worldbox:family", family, family.data, family.getFounderFirst() ?? family.getFounderSecond() ??
                    family.units.FirstOrDefault()));

            var armyNames = world.armies.ToLookup(army => (army.getKingdom()?.id, army.name));
            var reservedArmies = new HashSet<(long?, string)>();
            var renamedArmies = new HashSet<long>();
            var cityNames = _cities.Where(city => city.After != null).ToDictionary(city => city.Id, city => city.After);
            PlanObjects(() => world.armies.OrderBy(army => army.id), army => army.data, ArmyRequest,
                army => army.getKingdom() == null ? IndependentNames.ArmyName.Unassigned : null,
                (army, candidate) => !reservedArmies.Contains((army.getKingdom()?.id, candidate)) &&
                    !armyNames[(army.getKingdom()?.id, candidate)].Any(other => other != army && !renamedArmies.Contains(other.id)),
                (army, name) => { reservedArmies.Add((army.getKingdom()?.id, name)); renamedArmies.Add(army.id); },
                item => IndependentNames.ArmyName.Save(item.Subject, item.Subject.getKingdom(), item.After));

            PlanObjects(() => world.plots, plot => plot.data, plot => IndependentNames.PlotRequest(plot, plot.getAuthor(), plot.getAsset()));
            PlanObjects(() => world.books.Where(book => book.getAsset()?.name_template?.StartsWith("book_name_", StringComparison.Ordinal) == true),
                book => book.data, book => IndependentNames.BookRequest(book.getAsset().name_template, book, world.units.get(book.data.author_id),
                    book.data.culture_id >= 0 ? book.data.culture_id : null, book.data.language_id >= 0 ? book.data.language_id : null));
            PlanObjects(() => world.items.Where(item => !string.IsNullOrEmpty(item.data.name)),
                item => item.data, IndependentNames.ExistingItemRequest);
            _manual = manual + _countries.Count(item => item.Custom) + _alliances.Count(item => item.Custom);

            Actor Member(long id, IEnumerable<Actor> members) => world.units.get(id) ?? members.FirstOrDefault();

            NameRequest ArmyRequest(Army army)
            {
                var kingdom = army.getKingdom();
                var city = army.getCity();
                var station = city != null && cityNames.TryGetValue(city.id, out string name) ? name : city?.name;
                return IndependentNames.ArmyName.Request(army, kingdom, station, ProjectedName(kingdom));
            }

            void PlanTitle(string type, Func<string> read, Action<string> write)
            {
                var request = IndependentNames.WorldRequest(type, _map);
                string before = read(), planned = Generate(request).Text;
                int changed = planned != null && planned != before ? 1 : 0;
                _others.Add(new OtherChanges(
                    () => read() != before || !SameContext(request, IndependentNames.WorldRequest(type, _map)),
                    () => { if (changed != 0) write(planned); return changed; },
                    changed, planned == null ? 1 : 0));
            }

            void PlanObjects<T>(Func<IEnumerable<T>> scope, Func<T, BaseSystemData> dataFor, Func<T, NameRequest> requestFor,
                Func<T, string> literalFor = null, Func<T, string, bool> unique = null, Action<T, string> reserve = null,
                Action<Change<T>> save = null) where T : NanoObject
            {
                var all = scope().Select(subject =>
                {
                    var data = dataFor(subject);
                    return (Subject: subject, Before: data.name, Manual: data.custom_name);
                }).ToArray();
                var changes = Capture(all.Select(item => item.Subject), dataFor, requestFor, literalFor, unique, reserve);
                _others.Add(new OtherChanges(
                    () => !scope().SequenceEqual(all.Select(item => item.Subject)) ||
                        all.Any(item => dataFor(item.Subject).name != item.Before || dataFor(item.Subject).custom_name != item.Manual) ||
                        changes.Any(change => !SameContext(change.Request, requestFor(change.Subject))),
                    () => Write(changes, save ?? (change => { if (change.Before != change.After) change.Subject.setName(change.After); })),
                    changes.Count(change => change.After != null && change.After != change.Before),
                    changes.Count(change => change.After == null)));
            }
        }

        private static NameRequest SocietyRequest(string type, NanoObject subject, BaseSystemData data, Actor actor = null,
            Culture culture = null, Language language = null, string species = null)
        {
            var inputs = new Dictionary<string, object>();
            if (species != null) inputs["species"] = inputs[VanillaNames.CultureSpecies] = species;
            return VanillaNames.Request(type, actor, unchecked((ulong)IndependentNames.Seed(data.id)),
                culture: culture?.id ?? actor?.culture?.id ?? (data.name_culture_id >= 0 ? data.name_culture_id : null),
                language: language?.id, inputs: inputs, subject: subject);
        }

        private string ProjectedName(Kingdom kingdom) => kingdom != null && _countryNames.TryGetValue(kingdom.id, out var name)
            ? name.Text : kingdom?.name;

        private NameRequest CityRequest(City city) => VanillaNames.CityRequest(city,
            city.leader ?? World.world.units.get(city.data.founder_id), IndependentNames.Seed(city.id), ProjectedName);

        private NameRequest WarRequest(War war) => IndependentNames.WarRequest(war.getMainAttacker(), war.getMainDefender(),
            war.getAsset(), war.id, war, ProjectedName);

        private static NameRequest CountryRequest(Kingdom kingdom) =>
            KingdomNames.Request(kingdom, kingdom.king, IndependentNames.Seed(kingdom.id),
                kingdom.culture?.id ?? kingdom.king?.culture?.id ?? (kingdom.data.name_culture_id >= 0 ? kingdom.data.name_culture_id : null),
                kingdom.language?.id);

        internal IEnumerable<(string Before, string Name, string Short)> Countries => _countries
            .Where(item => _countryNames.TryGetValue(item.Kingdom.id, out var name) && (item.Before != name.Text || item.Short != name.Short))
            .Select(item => (item.Before, _countryNames[item.Kingdom.id].Text, _countryNames[item.Kingdom.id].Short));

        internal IEnumerable<(string Before, string Name)> Alliances => _alliances
            .Where(item => item.After != null && item.After != item.Before).Select(item => (item.Before, item.After));
        internal IEnumerable<(string Before, string Name)> Cities => Changes(_cities);
        internal IEnumerable<(string Before, string Name)> Actors => Changes(_actors);
        internal IEnumerable<(string Before, string Name)> Wars => Changes(_wars);

        private static IEnumerable<(string Before, string Name)> Changes<T>(IEnumerable<Change<T>> items) where T : NanoObject =>
            items.Where(item => item.After != null && item.After != item.Before).Select(item => (item.Before, item.After));

        internal int Others => _others.Sum(group => group.Changed);

        internal int Failed => _countries.Count(item => item.Request != null && !_countryNames.ContainsKey(item.Kingdom.id)) +
            _alliances.Count(item => item.Request != null && item.After == null) + _cities.Count(item => item.After == null) +
            _actors.Count(item => item.After == null) + _wars.Count(item => item.After == null) + _others.Sum(group => group.Failed);
        internal int Manual => _manual;

        internal (int Changed, int Failed) Apply()
        {
            var world = World.world;
            var service = NamingApi.Current;
            var catalog = service.PackageCatalog();
            if (!ReferenceEquals(world.map_stats, _map) || !ReferenceEquals(service.ActiveWorld, _active) ||
                catalog.Count != _catalog.Count || _catalog.Any(item => !catalog.TryGetValue(item.Key, out var pack) || !ReferenceEquals(pack, item.Value)) ||
                world.kingdoms.Count != _countries.Length || world.alliances.Count != _alliances.Length ||
                _countries.Any(item => world.kingdoms.get(item.Kingdom.id) != item.Kingdom || item.Kingdom.name != item.Before ||
                    item.Kingdom.data.custom_name != item.Custom || KingdomNames.Short(item.Kingdom) != item.Short ||
                    item.Request != null && !SameContext(item.Request, CountryRequest(item.Kingdom))) ||
                _alliances.Any(item => world.alliances.get(item.Alliance.id) != item.Alliance || item.Alliance.name != item.Before ||
                    item.Alliance.data.custom_name != item.Custom || item.Request != null &&
                    !SameContext(item.Request, AllianceNames.Request(item.Alliance, projected: _countryNames))) ||
                Stale(_cities, id => world.cities.get(id), city => city.data, CityRequest) ||
                Stale(_actors, id => world.units.get(id), actor => actor.data, VanillaNames.PersonRequest) ||
                Stale(_wars, id => world.wars.get(id), war => war.data, WarRequest) ||
                _others.Any(group => group.Stale()))
                throw new InvalidOperationException("预演对象或命名规则已有变化，请重新预演。");

            ApplyStyle(_draft, _pack);
            int changed = 0;
            foreach (var item in _countries)
                if (_countryNames.TryGetValue(item.Kingdom.id, out var name))
                {
                    KingdomNames.Save(item.Kingdom, name);
                    if (item.Before == name.Text) continue;
                    item.Kingdom.setName(name.Text);
                    changed++;
                }
            foreach (var item in _alliances)
                if (item.After != null && item.After != item.Before)
                {
                    item.Alliance.setName(item.After);
                    changed++;
                }
            changed += Write(_cities, item =>
            {
                PlaceNames.SaveCityName(item.Subject, item.Result);
                if (item.Before != item.After) item.Subject.setName(item.After);
            });
            changed += Write(_actors, item =>
            {
                if (item.Result != null) VanillaNames.SavePerson(item.Subject, item.Request, item.Result);
                else if (item.Before != item.After) item.Subject.setName(item.After);
            });
            changed += Write(_wars, item =>
            {
                if (item.Before != item.After) item.Subject.setName(item.After);
            });
            foreach (var group in _others) changed += group.Apply();
            return (changed, Failed);
        }

        private static int Write<T>(IEnumerable<Change<T>> changes, Action<Change<T>> save) where T : NanoObject
        {
            int changed = 0;
            foreach (var item in changes)
            {
                if (item.After == null) continue;
                save(item);
                if (item.Before != item.After) changed++;
            }
            return changed;
        }

        private static bool Stale<T>(IEnumerable<Change<T>> changes, Func<long, T> find, Func<T, BaseSystemData> dataFor,
            Func<T, NameRequest> requestFor) where T : NanoObject
        {
            foreach (var item in changes)
            {
                if (!ReferenceEquals(find(item.Id), item.Subject)) return true;
                var data = dataFor(item.Subject);
                if (data.custom_name || data.name != item.Before || !SameContext(item.Request, requestFor(item.Subject))) return true;
            }
            return false;
        }

        private static bool SameContext(NameRequest before, NameRequest after) => before == after ||
            before != null && after != null && before.Type == after.Type && before.Seed == after.Seed &&
            before.Culture == after.Culture && before.Language == after.Language && before.Writing == after.Writing &&
            before.FatherSurname == after.FatherSurname && before.MotherSurname == after.MotherSurname &&
            before.Inputs.SequenceEqual(after.Inputs) && before.FixedParts.SequenceEqual(after.FixedParts);
    }

}
