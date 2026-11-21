#!/usr/bin/env python3
"""ChineseName's isolated WorldBox and DebugToolbox runner."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import sys


REPO = Path(__file__).resolve().parents[1]
MOD = "ChineseName"
ENTRIES = ("Source", "Content", "Locales", "GameResources", "Examples/Packages",
           "mod.json", "icon.png", "LICENSE")
EXAMPLE = REPO / "Examples/SpellNames"
EXONYMS = REPO / "Addons/CultureExonyms"
HISTORY = REPO / "Addons/HistoryEvents"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reference", type=Path, default=REPO.parent / "Cultiway")
    parser.add_argument("--name", default="default")
    parser.add_argument("--spell-example", action="store_true")
    parser.add_argument("--culture-exonyms", action="store_true")
    parser.add_argument("--history-events", action="store_true")
    parser.add_argument("--cultiway", choices=("with-chinesename", "alone"))
    parser.add_argument("--root", type=Path, default=Path.home() / ".local/state/chinesename-dev")
    actions = parser.add_subparsers(dest="action", required=True)
    setup = actions.add_parser("setup")
    setup.add_argument("--game", type=Path, default=REPO.parents[1])
    setup.add_argument("--toolbox", type=Path, default=REPO.parent / "DebugToolbox")
    setup.add_argument("--software-rendering", action="store_true")
    actions.add_parser("start").add_argument("--timeout", type=int, default=180)
    actions.add_parser("run").add_argument("script", type=Path)
    actions.add_parser("status")
    actions.add_parser("stop")
    args = parser.parse_args()
    if (args.culture_exonyms or args.history_events) and args.cultiway == "alone":
        parser.error("ChineseName add-ons require ChineseName in the snapshot")

    spec = importlib.util.spec_from_file_location(
        "worldbox_dev", args.reference / ".agents/skills/develop/scripts/dev.py")
    dev = importlib.util.module_from_spec(spec)
    # Import the reference tooling without placing caches in the adjacent repository.
    sys.dont_write_bytecode = True
    spec.loader.exec_module(dev)
    cultiway_entries = dev.MOD_ENTRIES
    cultiway = args.reference.resolve()

    def mod_inputs(folder):
        entries = cultiway_entries if folder.name == "Cultiway" or folder == cultiway else ENTRIES
        files = []
        for entry in entries:
            path = folder / entry
            files.extend(path.rglob("*") if path.is_dir() else [path])
        return dev.file_digest(files, folder)

    dev.mod_inputs = mod_inputs

    class Environment(dev.Environment):
        def __init__(self):
            self.worktree = REPO
            self.root = args.root.expanduser().resolve()
            self.name = args.name
            if not self.name or any(c not in "abcdefghijklmnopqrstuvwxyz0123456789-" for c in self.name):
                raise dev.DevError("Use a lowercase environment name")
            if self.root.is_relative_to(REPO.parents[1]) or REPO.is_relative_to(self.root):
                raise dev.DevError("The runtime must be outside the game and repository")
            if self.root == (Path.home() / ".local/state/cultiway-dev").resolve():
                raise dev.DevError("Use a dedicated ChineseName runtime root")
            self.id = hashlib.sha256(str(REPO).encode()).hexdigest()[:12] + "-" + self.name
            self.directory = self.root / "environments" / self.id
            if self.directory.resolve() != self.directory:
                raise dev.DevError("The environment directory cannot be redirected")
            self.record = self.directory / "environment.json"
            self.game = self.directory / "game"
            self.profile = self.directory / "profile"

        def check_process(self, state):
            launch = state.get("launch")
            if not launch or dev.process_identity(launch["pid"]) is None:
                return False
            process = Path("/proc") / str(launch["pid"])
            marker = ("CHINESENAME_DEV_RUN=" + launch["id"]).encode()
            if (dev.process_identity(launch["pid"]) != launch["processStart"] or
                    (process / "exe").resolve() != self.game / "worldbox" or
                    marker not in (process / "environ").read_bytes().split(b"\0")):
                raise dev.DevError("The recorded process does not belong to this environment")
            return True

        def snapshot(self, state):
            before = dev.mod_inputs(REPO) if args.cultiway != "alone" else {"sha256": None, "files": {}}
            cultiway_inputs = dev.mod_inputs(cultiway) if args.cultiway else None
            mods = self.game / "Mods"
            example = dev.mod_inputs(EXAMPLE) if args.spell_example else None
            exonyms = dev.mod_inputs(EXONYMS) if args.culture_exonyms else None
            history = dev.mod_inputs(HISTORY) if args.history_events else None
            toolbox = Path(state["toolboxSource"])
            names = [name for name in dev.git(toolbox, "ls-files", "-z").split("\0")
                     if name and (toolbox / name).is_file() and not name.startswith("Client/")]
            names += [path.relative_to(toolbox).as_posix() for path in (toolbox / "Assemblies").glob("*.dll")]
            names = sorted(set(names))
            toolbox_inputs = dev.file_digest([toolbox / name for name in names], toolbox)
            if (state.get("inputs", {}).get("sha256") == before["sha256"] and
                    state.get("toolboxInputs") == toolbox_inputs and
                    state.get("cultiwayMode") == args.cultiway and
                    state.get("cultiwayInputs") == cultiway_inputs and
                    (not args.cultiway or dev.mod_inputs(mods / "Cultiway") == cultiway_inputs) and
                    state.get("spellInputs") == example and
                    (not args.spell_example or dev.mod_inputs(mods / "SpellNamesExample") == example) and
                    state.get("exonymInputs") == exonyms and
                    (not args.culture_exonyms or dev.mod_inputs(mods / "CultureExonyms") == exonyms) and
                    state.get("historyInputs") == history and
                    (not args.history_events or dev.mod_inputs(mods / "HistoryEvents") == history) and
                    (args.cultiway == "alone" or dev.mod_inputs(mods / MOD) == before) and
                    dev.file_digest([mods / "DebugToolbox" / name for name in names],
                                    mods / "DebugToolbox") == toolbox_inputs):
                return before["sha256"]
            dev.remove_owned(mods, self.directory)
            if args.cultiway != "alone":
                for name in ENTRIES:
                    if (REPO / name).exists():
                        dev.copy_path(REPO / name, mods / MOD / name)
            if args.cultiway:
                for name in cultiway_entries:
                    if (cultiway / name).exists():
                        dev.copy_path(cultiway / name, mods / "Cultiway" / name)
                if dev.mod_inputs(mods / "Cultiway") != cultiway_inputs or dev.mod_inputs(cultiway) != cultiway_inputs:
                    raise dev.DevError("Cultiway inputs changed while staging the run")
            for name in names:
                dev.copy_path(toolbox / name, mods / "DebugToolbox" / name)
            if args.spell_example:
                dev.copy_path(EXAMPLE, mods / "SpellNamesExample")
                if dev.mod_inputs(mods / "SpellNamesExample") != example:
                    raise dev.DevError("Spell example inputs changed while staging the run")
            if args.culture_exonyms:
                dev.copy_path(EXONYMS, mods / "CultureExonyms")
                if dev.mod_inputs(mods / "CultureExonyms") != exonyms or dev.mod_inputs(EXONYMS) != exonyms:
                    raise dev.DevError("Culture exonym inputs changed while staging the run")
            if args.history_events:
                dev.copy_path(HISTORY, mods / "HistoryEvents")
                if dev.mod_inputs(mods / "HistoryEvents") != history or dev.mod_inputs(HISTORY) != history:
                    raise dev.DevError("History event inputs changed while staging the run")
            if args.cultiway != "alone" and (dev.mod_inputs(mods / MOD) != before or dev.mod_inputs(REPO) != before):
                raise dev.DevError("Mod inputs changed while staging the run")
            cache = self.game / "worldbox_Data/StreamingAssets/mods/NML"
            dev.remove_owned(cache / "CompiledMods", self.directory)
            for name in ("mod_compile_records.json", "disabled_mods.txt"):
                (cache / name).unlink(missing_ok=True)
            state["toolboxInputs"] = toolbox_inputs
            state["spellInputs"] = example
            state["exonymInputs"] = exonyms
            state["historyInputs"] = history
            state["cultiwayMode"] = args.cultiway
            state["cultiwayInputs"] = cultiway_inputs
            state["inputs"] = dict(before, commit=dev.git(REPO, "rev-parse", "HEAD"))
            return before["sha256"]

        def child_environment(self, state):
            env = super().child_environment(state)
            env = {key: value for key, value in env.items() if not key.startswith("CULTIWAY_")}
            run = self.directory / "runs" / state["launch"]["id"]
            return dict(env, CHINESENAME_DEV_RUN=state["launch"]["id"],
                        CHINESENAME_DEV_OUTPUT=str(run / "output"),
                        XDG_DATA_HOME=str(self.profile / "data"),
                        XDG_CACHE_HOME=str(self.profile / "cache"))

        def probe(self, state):
            code = """return new {
                pid = System.Diagnostics.Process.GetCurrentProcess().Id,
                run = Environment.GetEnvironmentVariable("CHINESENAME_DEV_RUN"),
                data = UnityEngine.Application.persistentDataPath,
                game = UnityEngine.Application.dataPath,
                mods = NeoModLoader.WorldBoxMod.LoadedMods.Select(m => m.GetDeclaration().UID).ToArray(),
                folders = NeoModLoader.WorldBoxMod.LoadedMods.ToDictionary(m => m.GetDeclaration().UID,
                    m => m.GetDeclaration().FolderPath),
                ready = !SmoothLoader.isLoading(),
                version = UnityEngine.Application.version };"""
            result = self.rpc(state, "eval", code)["Result"]
            if (result["pid"] != state["launch"]["pid"] or result["run"] != state["launch"]["id"] or
                    Path(result["game"]).resolve() != self.game / "worldbox_Data" or
                    not Path(result["data"]).resolve().is_relative_to(self.profile)):
                raise dev.DevError("The Unity process or profile is outside this environment")
            loaded = set(result["mods"])
            expected = {"INMNY_DEBUGTOOLBOX": "DebugToolbox"}
            if state.get("cultiwayMode") != "alone":
                expected["CHINESENAME"] = MOD
            if state.get("cultiwayMode"):
                expected["CULTIWAY"] = "Cultiway"
            if state.get("spellInputs"):
                expected["CHINESENAME_SPELLNAMESEXAMPLE"] = "SpellNamesExample"
            if state.get("exonymInputs"):
                expected["CHINESENAME_CULTUREEXONYMS"] = "CultureExonyms"
            if state.get("historyInputs"):
                expected["CHINESENAME_HISTORYEVENTS"] = "HistoryEvents"
            if loaded != set(expected):
                raise dev.DevError("Unexpected mods in the isolated snapshot: " + ", ".join(sorted(loaded)))
            for uid, folder in expected.items():
                if Path(result["folders"][uid]).resolve() != self.game / "Mods" / folder:
                    raise dev.DevError(uid + " has not loaded from the isolated snapshot")
            return result

        def require_current(self, state):
            if state.get("spellInputs") and state["spellInputs"] != dev.mod_inputs(EXAMPLE):
                raise dev.DevError("The spell example changed. Stop and restart this environment.")
            if state.get("exonymInputs") and state["exonymInputs"] != dev.mod_inputs(EXONYMS):
                raise dev.DevError("Culture exonyms changed. Stop and restart this environment.")
            if state.get("historyInputs") and state["historyInputs"] != dev.mod_inputs(HISTORY):
                raise dev.DevError("History events changed. Stop and restart this environment.")
            if state.get("cultiwayInputs") and state["cultiwayInputs"] != dev.mod_inputs(cultiway):
                raise dev.DevError("Cultiway inputs changed. Stop and restart this environment.")
            if state.get("cultiwayMode") == "alone":
                identity = self.probe(state)
                if not identity["ready"]:
                    raise dev.DevError("The isolated game is still loading")
                return identity
            return super().require_current(state)

    os.umask(0o077)
    try:
        env = Environment()
        with dev.locked(env.root / "locks" / (env.id + ".lock")):
            if args.action == "setup":
                # The shared setup writes a Cultiway-specific props file to its worktree.
                # Redirect that one file into the private runtime.
                env.directory.mkdir(parents=True, exist_ok=True)
                env.worktree = env.directory / "setup-worktree"
                env.worktree.mkdir(exist_ok=True)
                original_git = dev.git
                dev.git = lambda path, *command: original_git(REPO, *command)
                try:
                    result = env.setup(args.game, args.toolbox, args.software_rendering)
                finally:
                    dev.git = original_git
                    env.worktree = REPO
                state = dev.read_json(env.record)
                state["worktree"] = str(REPO)
                env.save(state)
                result = env.status(state)
            elif args.action == "start":
                result = env.start(args.timeout)
            elif args.action == "run":
                result = env.execute([str(args.script)], script=True)
            else:
                result = getattr(env, args.action)()
        print(json.dumps(result, ensure_ascii=False, indent=2))
    except (dev.DevError, OSError) as error:
        sys.exit(str(error))


if __name__ == "__main__":
    main()
