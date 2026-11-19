#!/usr/bin/env python3
"""Build the bundled species defaults and the optional naming packs."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import tomllib


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "ChineseNamePackages" / "lexicon_variants"
CATALOGS = SOURCE.parent
DEST = ROOT / "Examples" / "Packages"
SPECIES_DEFAULTS = ROOT / "Content" / "species-defaults.naming.json"
LOCAL_ID = re.compile(r"[a-z][a-z0-9_.-]{0,63}")
REGIONAL_POOL_LABELS = {"given": "给名", "surname": "姓氏", "city": "城市名", "realm": "国号词根"}
CONTEXT_POOL_LABELS = {"war-ending": "战事称谓", "alliance-ending": "盟约称谓", "army-ending": "军号后缀",
                       "book-ending": "书名后缀", "artifact": "器物名号", "motto": "格言", "creed": "宗教名"}


def local_id(text):
    if not LOCAL_ID.fullmatch(text):
        raise ValueError(f"Invalid identifier: {text}")
    return text


def make_pack(source):
    with source.open("rb") as file:
        return compile_lexicon(source.stem, tomllib.load(file))


def compile_lexicon(key, content):
    key = local_id(key)
    pack = {
        "format": 1,
        "id": "chinesename:" + key,
        "label": content["label"],
        "supplemental": True,
        "words": [],
        "pools": {},
        "poolLabels": {},
        "poolAliases": {},
        "wordGroups": {},
    }
    words = {}
    for pool_key, entry in content["pools"].items():
        pool = "chinesename:" + local_id(f"{key}.{pool_key}")
        aliases = [entry["label"], *entry.get("aliases", [])]
        pack["poolLabels"][pool] = aliases[0]
        for alias in aliases:
            if not alias or len(alias) > 64 or not all(letter.isalpha() and ord(letter) < 0x10000 for letter in alias):
                raise ValueError(f"{pool}: invalid alias {alias!r}")
            if alias in pack["poolAliases"]:
                raise ValueError(f"{pool}: duplicate alias {alias}")
            pack["poolAliases"][alias] = pool

        members = {}
        groups = entry["groups"]
        if len(groups) < 2:
            raise ValueError(f"{pool}: add two contrasting groups for language preferences")
        for group_key, entries in groups.items():
            ids = []
            terms = entries.split() if isinstance(entries, str) else entries
            for entry in dict.fromkeys(terms):
                term, delimiter, chinese = entry.partition("=")
                values = [term, chinese] if delimiter else [term]
                if any(not value.strip() or len(value.encode("utf-16-le")) // 2 > 128 or
                       any(char in value for char in "<>|;:") for value in values):
                    raise ValueError(f"{pool}: invalid term {entry!r}")
                identity = (term, chinese)
                if identity not in words:
                    digest = hashlib.sha256(entry.encode()).hexdigest()[:20]
                    words[identity] = "chinesename:" + local_id(f"{key}.w-{digest}")
                ids.append(words[identity])
                members[words[identity]] = 1
            if len(ids) < 2:
                raise ValueError(f"{pool}: empty or singleton group {group_key}")
            pack["wordGroups"][local_id(f"{pool_key}.{group_key}")] = {"pool": pool, "words": ids}
        if len(members) > 4096 or any(len(group["words"]) == len(members)
            for group in pack["wordGroups"].values() if group["pool"] == pool):
            raise ValueError(f"{pool}: too large or some group covers the entire pool")
        pack["pools"][pool] = members
    pack["words"] = [{"id": word_id, "text": term, **({"chinese": chinese} if chinese else {})}
                     for (term, chinese), word_id in words.items()]
    return pack


def regional_lexicon(catalog):
    pools = {}
    for region, profile in catalog["regions"].items():
        for role, suffix in REGIONAL_POOL_LABELS.items():
            if role not in profile:
                continue
            pools[f"{region}.{role}"] = {"label": profile["label"] + suffix, "groups": {
                group: terms.split("|") for group, terms in profile[role].items()
            }}
    for register, profile in catalog["registers"].items():
        for role, terms in profile["pools"].items():
            pools[f"{register}.{role}"] = {"label": profile["label"] + CONTEXT_POOL_LABELS[role],
                                           "groups": dict(zip(("first", "second"), terms, strict=True))}
    return compile_lexicon("world-styles-lexicon", {"label": "世界地域译名词库", "pools": pools})


def species_lexicon(catalog):
    pools = {}
    for theme, profile in catalog["themes"].items():
        for role, label, groups in (("given", "个体名", ("marks", "habits")),
                                    ("place", "地名词根", ("homeland", "frontier")),
                                    ("motto", "格言", ("community", "journey"))):
            pools[f"{theme}.{role}"] = {"label": profile["label"] + label,
                                       "groups": dict(zip(groups, profile[role], strict=True))}
    shared = catalog["affairs"]
    for role, terms in shared["pools"].items():
        pools[role] = {"label": shared["label"] + CONTEXT_POOL_LABELS[role],
                       "groups": dict(zip(("first", "second"), terms, strict=True))}
    return compile_lexicon("species-lexicon", {"label": "物种风格词库", "pools": pools})


class StyleSets:
    def __init__(self, lexicons, bundled):
        self.lexicons = lexicons
        self.bundled = bundled
    def new_set(self, stem, label, vocabulary=None):
        lexicon = self.lexicons[(vocabulary or stem) + "-lexicon"]
        return {
            "format": 1,
            "id": "chinesename:" + stem + "-set",
            "label": label,
            "requires": {self.bundled["id"]: 1, lexicon["id"]: 1},
            "rules": {},
        }

    def add_profile(self, pack, categories, *, species=None, separator=None, profile=None,
                    conditions=None, members=None, prefixes=None, priority=None):
        for category, components in categories.items():
            rule = {
                "id": pack["id"].split(":")[1] + ("." + (profile or species) if profile or species else ""),
                "priority": priority if priority is not None else (50 if species else 40),
                "useNativeWordGroups": True,
                "writing": "Chinese",
                "parts": [{"id": role, **component} for role, component in components.items()],
            }
            criteria = ({"kind": "sapient"} if category == "person" else {}) if conditions is None else dict(conditions)
            if species:
                criteria["culture-species"] = species
            if criteria:
                rule["when"] = criteria
            if members:
                rule["whenOneOf"] = members
            if prefixes:
                rule["whenStartsWith"] = prefixes
            if category == "person" and separator:
                chinese, original = (separator, " ") if isinstance(separator, str) else separator
                rule.update(chineseSeparator=chinese, originalSeparator=original)
            if category == "person":
                rule["inheritance"] = "Father"
            pack["rules"].setdefault("worldbox:" + category, []).append(rule)

    def pool(self, source, key):
        pack = self.bundled if source == "classic" else self.lexicons[source + "-lexicon"]
        namespace = "chinesename:" if source == "classic" else pack["id"] + "."
        pool_id = namespace + key
        if pool_id not in pack["pools"]:
            raise ValueError(f"Unknown pool in naming sets: {pool_id}")
        return {"pool": pool_id}

    def named(self, component, suffix=None):
        return {"name": component} if suffix is None else {"name": component, "suffix": {"literal": suffix}}

    def input_field(self, field, fallback=None):
        return {"input": field} if fallback is None else {"input": field, "fallbackPool": fallback["pool"]}

    def family(self, suffix, fallback):
        return {"founder-surname": self.input_field("founder-surname", fallback),
                "suffix": {"literal": suffix}}

    def contextual_pool(self, field, prefix, fallback):
        return {"poolInput": field, "pool": "chinesename:" + prefix, "fallbackPool": fallback["pool"]}

    def affairs(self, style, country, city, motto, prefix=""):
        def themed(key):
            return self.pool(style, prefix + key)
        return {
            "war": {"attacker": self.input_field("attacker"), "bridge": {"literal": "—"},
                    "defender": self.input_field("defender"), "suffix": themed("war-ending")},
            "alliance": {"origin": self.input_field("origin", country), "suffix": themed("alliance-ending")},
            "army": {"station": self.input_field("station", city), "suffix": themed("army-ending")},
            "book": {"topic": self.contextual_pool("topic", "book-", self.pool("classic", "book-name")),
                     "suffix": themed("book-ending")},
            "motto": self.named(motto),
            "item": {"name": themed("artifact"),
                     "material": self.contextual_pool("material", "equipment-material", self.pool("classic", "equipment-material")),
                     "equipment": self.contextual_pool("equipment", "equipment-kind", self.pool("classic", "equipment-kind"))},
        }

    def original(self):
        guofeng = self.new_set("guofeng", "国风")
        self.add_profile(guofeng, {
            "person": {"surname": self.pool("guofeng", "single-surname"), "given": self.pool("guofeng", "given")},
            "city": {"name": self.pool("guofeng", "town-root"), "suffix": self.pool("guofeng", "town-ending")},
            "kingdom": {"name": self.pool("guofeng", "country-double"), "suffix": self.pool("guofeng", "polity-ending")},
            "culture": self.named(self.pool("guofeng", "tradition"), "之风"),
            "language": self.named(self.pool("guofeng", "speech"), "语"),
            "family": self.family("一门", self.pool("guofeng", "hall")),
            "clan": self.named(self.pool("guofeng", "emblem"), "氏族"),
            **self.affairs("guofeng", self.pool("guofeng", "country-double"), self.pool("guofeng", "town-root"), self.pool("guofeng", "saying")),
        })
        wafuu = self.new_set("wafuu", "和风")
        self.add_profile(wafuu, {
            "person": {"surname": self.pool("wafuu", "surname"), "given": self.pool("wafuu", "given")},
            "city": self.named(self.pool("wafuu", "city-root"), "城"),
            "kingdom": self.named(self.pool("wafuu", "province"), "国"),
            "culture": self.named(self.pool("wafuu", "province"), "风俗"),
            "language": self.named(self.pool("wafuu", "province"), "语"),
            "family": self.family("家", self.pool("wafuu", "surname")),
            "clan": self.named(self.pool("wafuu", "clan-root"), "一门"),
            **self.affairs("wafuu", self.pool("wafuu", "province"), self.pool("wafuu", "city-root"), self.pool("wafuu", "saying")),
        })
        fantasy = self.new_set("fantasy-race", "西幻")
        self.add_profile(fantasy, {
            "person": {"given": self.pool("classic", "western-given"), "surname": self.pool("classic", "western-surname")},
            "city": self.named(self.pool("classic", "western-city-places")),
            "kingdom": self.named(self.pool("classic", "western-kingdom-countries")),
            "culture": self.named(self.pool("classic", "western-culture")),
            "language": self.named(self.pool("classic", "western-language")),
            "family": self.family("家族", self.pool("classic", "western-surname")),
            "clan": self.named(self.pool("classic", "western-clan")),
            **self.affairs("fantasy-race", self.pool("classic", "western-kingdom-countries"),
                      self.pool("classic", "western-city-places"), self.pool("classic", "western-motto")),
        }, separator="·")
        for species, city_suffix, country_suffix in (("elf", "庭", "王庭"), ("dwarf", "堡", "王国"), ("orc", "寨", "部族")):
            given = self.pool("fantasy-race", species + "-person")
            house = self.pool("fantasy-race", species + "-house")
            place = self.pool("fantasy-race", species + "-place")
            self.add_profile(fantasy, {
                "person": {"given": given, "surname": house},
                "city": self.named(place, city_suffix),
                "kingdom": self.named(house, country_suffix),
                "culture": self.named(place, "传统"),
                "language": self.named(self.pool("fantasy-race", species + "-language"), "语"),
                "family": self.family("家族", house),
                "clan": self.named(house, "部族" if species == "orc" else "氏族"),
                **self.affairs("fantasy-race", house, place, self.pool("fantasy-race", "frontier-oaths")),
            }, species=species, separator="·")
        return (guofeng, wafuu, fantasy)

    def regional(self, catalog):
        for region, profile in catalog["regions"].items():
            pack = self.new_set("world-" + region, profile["label"], "world-styles")
            known = {role: self.pool("world-styles", f"{region}.{role}") for role in REGIONAL_POOL_LABELS if role in profile}
            given, city, realm = known["given"], known["city"], known["realm"]
            surname = known.get("surname")
            register = profile["register"]
            realm_ending = catalog["registers"][register]["realm_ending"]
            motto = self.pool("world-styles", register + ".motto")
            creed = self.pool("world-styles", register + ".creed")
            person = {"given": given}
            separator = None
            if surname:
                if profile.get("name_order") == "surname-first":
                    person = {"surname": surname, "given": given}
                    separator = ("", profile.get("original_separator", " "))
                else:
                    person["surname"] = surname
                    separator = "·"
            self.add_profile(pack, {
                "person": person,
                "city": self.named(city),
                "kingdom": self.named(realm, realm_ending),
                "culture": self.named(realm, "传统"),
                "language": self.named(realm, profile["language"]),
                "family": self.family("家族", surname or realm),
                "clan": self.named(surname or realm, "氏族"),
                "religion": self.named(creed),
                **self.affairs("world-styles", realm, city, motto, register + "."),
            }, separator=separator)
            yield pack

    def species(self, catalog, *, defaults=False):
        pack = self.new_set("species", "物种风格")
        for theme, profile in catalog["themes"].items():
            if defaults and theme == "human":
                continue
            given = self.pool("species", theme + ".given")
            place = self.pool("species", theme + ".place")
            motto = self.pool("species", theme + ".motto")
            creed = self.pool("species", "creed")
            society = {
                "person": {"surname": place, "given": given},
                "city": self.named(place, profile.get("city_suffix", "城")),
                "kingdom": self.named(place, profile.get("realm_suffix", "国")),
                "culture": self.named(place, "传统"),
                "language": self.named(place, "语"),
                "family": self.family("家族", place),
                "clan": self.named(place, "氏族"),
                "religion": {"name": place, "suffix": creed},
                **self.affairs("species", place, place, motto),
            }
            society["item"]["name"] = given
            individual = {"person": {"given": given}}
            boat = theme in ("fishing", "merchant", "transport")
            if boat:
                individual["person"]["first"] = {"literal": profile["label"]}
            subspecies = {"subspecies": {
                "name": place,
                "habitat": self.contextual_pool("kind", "subspecies-", self.pool("classic", "subspecies-name")),
                "suffix": self.pool("classic", "subspecies-ending")
            }}
            if theme == "human":
                self.add_profile(pack, society, profile="common", priority=35)

            def add_match(profile_id, **match):
                self.add_profile(pack, individual, profile=profile_id + ".individual", priority=64,
                                 conditions={"kind": "boat" if boat else "creature"}, **match)
                if not boat:
                    self.add_profile(pack, subspecies, profile=profile_id, priority=64, **match)

            members = profile.get("members", "").split()
            if members:
                self.add_profile(pack, society, profile=theme, priority=65, members={"culture-species": members})
                add_match(theme, members={"species": members})
            for index, prefix in enumerate(profile.get("prefixes", "").split(), 1):
                profile_id = f"{theme}.prefix-{index}"
                if not boat:
                    self.add_profile(pack, society, profile=profile_id, priority=63, prefixes={"culture-species": prefix})
                add_match(profile_id, prefixes={"species": prefix})
        return pack


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    bundled = json.loads((ROOT / "Content/default.naming.json").read_text(encoding="utf-8"))
    aliases = {alias.casefold() for alias in bundled["poolAliases"]}
    sources = sorted(SOURCE.glob("*.toml"))
    if not sources:
        parser.error(f"No lexicon sources in {SOURCE}")
    lexicons = {source.stem: make_pack(source) for source in sources}
    regional = tomllib.loads((CATALOGS / "world_styles.toml").read_text(encoding="utf-8"))
    species = tomllib.loads((CATALOGS / "species_styles.toml").read_text(encoding="utf-8"))
    lexicons["world-styles-lexicon"] = regional_lexicon(regional)
    lexicons["species-lexicon"] = species_lexicon(species)
    for name, pack in lexicons.items():
        current = {alias.casefold() for alias in pack["poolAliases"]}
        conflict = aliases.intersection(current)
        if conflict:
            parser.error("Duplicate optional alias: " + ", ".join(sorted(conflict)))
        aliases.update(current)
        write(name, pack, args.check)
        print(f"{pack['label']} · {len(pack['pools'])} pools, {len(pack['words'])} words, {len(pack['wordGroups'])} groups")
    styles = StyleSets(lexicons, bundled)
    species_defaults = {
        **lexicons["species-lexicon"],
        "label": "内置物种风格",
        "cultureBases": [bundled["id"]],
        "fallbackPools": True,
        "rules": styles.species(species, defaults=True)["rules"],
    }
    write_file(SPECIES_DEFAULTS, species_defaults, args.check)
    for pack in (*styles.original(), *styles.regional(regional), styles.species(species)):
        write(pack["id"].split(":")[1], pack, args.check)
        print(f"{pack['label']} · {len(pack['rules'])} naming categories")


def write(name, pack, check):
    write_file(DEST / (name + ".naming.json"), pack, check)


def write_file(path, pack, check):
    data = json.dumps(pack, ensure_ascii=False, indent=2) + "\n"
    if check:
        if not path.exists() or path.read_text(encoding="utf-8") != data:
            raise SystemExit(f"Regenerate {path.relative_to(ROOT)} with Tools/variant_packs.py")
    else:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(data, encoding="utf-8")


if __name__ == "__main__":
    main()
