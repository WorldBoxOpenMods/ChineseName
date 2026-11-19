#!/usr/bin/env python3
"""Create independently installable archives from the release allowlist."""
import argparse
from pathlib import Path
from shutil import copyfile
from zipfile import ZIP_DEFLATED, ZipFile, ZipInfo

ROOT = Path(__file__).resolve().parents[1]


def build(output, folder, source, entries):
    with ZipFile(output, "w", compression=ZIP_DEFLATED) as archive:
        for entry in entries:
            path = source / entry
            files = sorted(candidate for candidate in path.rglob("*") if candidate.is_file()) if path.is_dir() else [path]
            for candidate in files:
                info = ZipInfo(folder + "/" + candidate.relative_to(source).as_posix())
                info.compress_type = ZIP_DEFLATED
                info.external_attr = 0o644 << 16
                archive.writestr(info, candidate.read_bytes())


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts" / "release")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    build(args.output / "ChineseName.zip", "ChineseName", ROOT,
          ("Source", "Content", "Locales", "GameResources", "mod.json", "icon.png", "LICENSE", "README.md",
           "Doc/screenshots", "Doc/命名框架重构/实现与验证.md", "Examples/Packages", "Tools/import_legacy_pack.py"))
    build(args.output / "SpellNamesExample.zip", "SpellNamesExample", ROOT / "Examples/SpellNames",
          ("Source", "Content", "mod.json"))
    packs = sorted((ROOT / "Examples/Packages").glob("*.naming.json"))
    if packs:
        build(args.output / "LexiconVariants.zip", "ChineseNameLexicons", ROOT,
              (*(pack.relative_to(ROOT) for pack in packs), "Examples/Packages/README.md", "LICENSE"))
    for pack in packs:
        copyfile(pack, args.output / pack.name)
