#!/usr/bin/env python3
"""Convert word lists and supported legacy ChineseName templates into one data-only pack."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
from zipfile import ZipFile


ID = re.compile(r"[a-z][a-z0-9_.-]{0,63}")
TOKEN = re.compile(r"\{[^{}]+\}|#[^#]+#|\$[^$]+\$")
WARS = dict(war_conquest="normal", war_spite="spite", war_inspire="inspire",
            war_rebellion="rebellion", war_whisper="whisper_of_war")
FILES = {"cities": "city", "kingdoms": "kingdom", "cultures": "culture", "languages": "language",
         "religions": "religion", "families": "family", "clans": "clan", "armies": "army",
         "alliances": "alliance", "creatures": "person", "other_creatures": "person",
         "mottos": "motto", "books": "book", "wars": "war", "weapons": "item",
         "armor_and_accessory": "item", "reforged_items": "item"}
SPECIES = re.compile(r"[a-zA-Z][a-zA-Z0-9_]{0,63}")
EQUIPMENT = {"sword", "axe", "hammer", "spear", "bow", "staff", "firearm", "helmet", "armor", "boots", "ring", "amulet"}


def digest(text):
    return hashlib.sha256(text.encode()).hexdigest()[:16]


def relevant(path, style):
    for root, label, suffix in (("word_libraries", "default", ".txt"), ("name_generators", style, ".json")):
        if root in path.parts and path.parts[path.parts.index(root) + 1:path.parts.index(root) + 2] == (label,):
            return path.suffix == suffix
    return False


def read_sources(source, style):
    if source.is_dir():
        for file in sorted(source.rglob("*")):
            path = PurePosixPath(file.relative_to(source).as_posix())
            if file.is_file() and relevant(path, style):
                if file.stat().st_size > 1_048_576:
                    raise ValueError(f"File too large: {path}")
                yield path, file.read_bytes().decode("utf-8-sig")
    else:
        with ZipFile(source) as archive:
            for info in sorted(archive.infolist(), key=lambda entry: entry.filename):
                path = PurePosixPath(info.filename)
                if info.is_dir() or not relevant(path, style):
                    continue
                if info.file_size > 1_048_576:
                    raise ValueError(f"File too large: {path}")
                yield path, archive.read(info).decode("utf-8-sig")


def target(file, generator):
    category = FILES.get(file)
    if category is None:
        return None
    if category == "war":
        return (category, {"kind": WARS[generator]}) if generator in WARS else None
    if category == "book":
        species, marker, topic = generator.partition("book_name_")
        if not marker or not topic:
            return None
        when = {"kind": marker + topic}
        if species and SPECIES.fullmatch(species.removesuffix("_")):
            when["species"] = species.removesuffix("_")
        return category, when
    if category == "item":
        if file == "reforged_items" and generator == "reforged_item":
            return category, {"stage": "reforge"}
        kind = generator.removeprefix("reforged_") if file == "reforged_items" else generator.removesuffix("_name")
        return (category, {"equipment": kind, "stage": "reforge" if file == "reforged_items" else "legendary"}) \
            if kind in EQUIPMENT else None
    if category == "motto":
        owner = generator.removesuffix("_mottos")
        return (category, {"kind": owner}) if owner in ("kingdom", "alliance", "clan") else None
    if category == "person":
        if not generator.endswith("_name"):
            return None
        kind = "sapient" if file == "creatures" else "creature"
        return category, {"kind": kind, "species": generator.removesuffix("_name")}
    if category == "family" and generator == "family_name":
        return category, {}
    if category in ("city", "kingdom", "culture", "language", "religion", "family", "clan"):
        suffix = "_" + ("lang" if category == "language" else category)
        species = generator.removesuffix(suffix)
        if generator.endswith(suffix) and SPECIES.fullmatch(species):
            return category, {"species": species}
        return None
    return category, {}


def arrangement(form, category, pools):
    if not isinstance(form, str) or not form:
        return None
    parts = []
    position = 0
    person_ids = iter(("first", "given", "middle"))
    for match in TOKEN.finditer(form):
        if match.start() != position:
            return None
        position = match.end()
        token = match[0]
        value = token[1:-1]
        if token.startswith("{"):
            label, _, semantic = value.partition(":")
            if label not in pools or semantic not in ("", "family_name"):
                return None
            part = {"pool": pools[label]}
            if category == "person" and semantic == "family_name":
                part["id"] = "surname"
        elif token.startswith("#"):
            if len(value.encode("utf-16-le")) // 2 > 128 or any(ord(ch) < 32 for ch in value):
                return None
            part = {"literal": value}
        elif category == "war" and value in ("attacker", "defender", "kingdom"):
            part = {"input": "origin" if value == "kingdom" else value}
        elif category == "family" and value == "founder_family_name":
            part = {"input": "founder-surname"}
        else:
            return None
        parts.append(part)
    if position != len(form) or not 1 <= len(parts) <= 32:
        return None

    # Uniform separators can be carried by one rule rather than consuming the named personal slots.
    rule = {}
    if category == "person":
        if len(parts) > 1:
            between = [part.get("literal") for part in parts[1::2]]
            if len(parts) % 2 == 1 and between and between[0] in ("·", "-", " ") and len(set(between)) == 1:
                parts = parts[::2]
                rule.update(chineseSeparator=between[0], originalSeparator=between[0])
        if not any(part.get("id") == "surname" for part in parts):
            parts.insert(0, {"id": "surname", "literal": ""})
        for part in parts:
            if "id" not in part:
                part["id"] = next(person_ids, None)
        if any(not part["id"] for part in parts) or len({part["id"] for part in parts}) != len(parts):
            return None
    else:
        for index, part in enumerate(parts):
            part["id"] = ("name", "suffix")[index] if index < 2 else "part-" + str(index + 1)
    return {**rule, "parts": parts}


def convert(sources, pack_id, name):
    pack = dict(format=1, id=pack_id, label=name, supplemental=True, rules={}, words=[], pools={},
                poolLabels={}, poolAliases={})
    prefix = pack_id.split(":")[0] + ":legacy-"
    counts = Counter(path.stem.casefold() for path, _ in sources if "word_libraries" in path.parts and path.suffix == ".txt")
    by_label = {}
    seen_words = set()
    for path, content in sources:
        if "word_libraries" not in path.parts or path.suffix != ".txt":
            continue
        relative = "/".join(path.parts[path.parts.index("word_libraries") + 1:])
        ref = prefix + digest(pack_id + "/" + relative)
        label = path.stem[:64]
        terms = dict.fromkeys(line.strip() for line in content.splitlines())
        members = {}
        for word in terms:
            if not word or len(word.encode("utf-16-le")) // 2 > 128 or any(ord(ch) < 32 or 127 <= ord(ch) < 160 for ch in word):
                continue
            # A spelling can belong to multiple pools. Keep one entry and preserve each membership.
            word_id = prefix + "w-" + digest(pack_id + "/" + word)
            members[word_id] = 1
            if word_id not in seen_words:
                seen_words.add(word_id)
                pack["words"].append({"id": word_id, "text": word})
            if len(members) == 4096:
                break
        if not members:
            continue
        pack["pools"][ref] = members
        pack["poolLabels"][ref] = label
        if counts[path.stem.casefold()] == 1:
            by_label[path.stem] = ref
            if label == path.stem and label.isalpha():
                pack["poolAliases"][label] = ref

    missed = []
    for path, content in sources:
        if "name_generators" not in path.parts or path.suffix != ".json":
            continue
        for generator in json.loads(content):
            key = generator["id"]
            context = target(path.stem, key)
            if context is None:
                missed.append(key)
                continue
            category, when = context
            variants = sorted(generator.get("templates", []), key=lambda entry: -entry.get("weight", 1))
            variants.append(generator.get("default_template") or {})
            options = [rule for variant in variants
                       if (rule := arrangement(variant.get("format"), category, by_label)) is not None]
            if not options:
                missed.append(key)
                continue
            rule_id = "legacy-" + digest(pack_id + "/" + path.stem + "/" + key)
            pack["rules"].setdefault("worldbox:" + category, []).append(
                {"id": rule_id, "priority": 100 + len(when), "when": when, **options[0]})
    return pack, missed


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help="Old ChineseName directory or ZIP")
    parser.add_argument("output", type=Path, help="New .naming.json path")
    parser.add_argument("--id", required=True, help="Unique publisher:package ID")
    parser.add_argument("--label", default="旧版中文名词表")
    parser.add_argument("--style", default="default", help="Subfolder under the original name_generators")
    args = parser.parse_args()
    if len(args.id.split(":")) != 2 or not all(ID.fullmatch(part) for part in args.id.split(":")):
        parser.error("--id must be a qualified publisher:package ID")
    if not args.output.name.endswith(".naming.json"):
        parser.error("output must end with .naming.json")
    try:
        pack, missed = convert(list(read_sources(args.source, args.style)), args.id, args.label)
        if not pack["pools"]:
            raise ValueError("No old word lists found under word_libraries/default")
        data = json.dumps(pack, ensure_ascii=False, indent=2) + "\n"
        # Use compact JSON only when necessary to fit the same reader used by the game.
        if len(data.encode("utf-16-le")) // 2 > 4_000_000:
            data = json.dumps(pack, ensure_ascii=False, separators=(",", ":")) + "\n"
        if len(data.encode("utf-16-le")) // 2 > 4_000_000 or len(data.encode()) > 16_000_000:
            raise ValueError("The converted pack still exceeds the naming JSON size limit")
        with args.output.open("x", encoding="utf-8") as output:
            output.write(data)
    except (OSError, ValueError) as error:
        parser.exit(1, str(error) + "\n")
    print(json.dumps({"pools": len(pack["pools"]), "rules": sum(map(len, pack["rules"].values())),
                      "skippedGenerators": missed}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
