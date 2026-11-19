#!/usr/bin/env python3
"""Run a glyph editor scenario with real mouse and keyboard input on an isolated X display."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("script", type=Path)
    parser.add_argument("--display", required=True, help="Xvfb display used by the isolated game")
    parser.add_argument("--name", default="parts-editor")
    parser.add_argument("--cultiway", choices=("with-chinesename", "alone"))
    args = parser.parse_args()
    env = {**os.environ, "DISPLAY": args.display}
    worktree = Path(__file__).resolve().parents[1]
    with tempfile.TemporaryDirectory(prefix="chinesename-glyph-input-") as folder:
        exchange = Path(folder)
        script = exchange / args.script.name
        script.write_text(args.script.read_text().replace("__EXCHANGE__", str(exchange)))
        window = subprocess.check_output(
            ["xdotool", "search", "--onlyvisible", "--name", "^WorldBox$"], text=True, env=env).splitlines()
        if len(window) != 1:
            raise SystemExit("Expected just one WorldBox window on the isolated display.")
        window = window[0]
        subprocess.run(["xdotool", "windowfocus", window], check=True, env=env)
        result = exchange / "response.json"
        command = [sys.executable, "Tools/game.py", "--name", args.name]
        if args.cultiway:
            command += ["--cultiway", args.cultiway]
        with result.open("w") as log, subprocess.Popen(
            [*command, "run", str(script)],
            cwd=worktree, stdout=log, stderr=subprocess.STDOUT,
        ) as game:
            seq = 1
            deadline = time.monotonic() + 100
            try:
                while game.poll() is None and time.monotonic() < deadline:
                    path = exchange / f"{seq}.mouse"
                    if not path.exists():
                        time.sleep(.04)
                        continue
                    action, *coords = path.read_text().split()
                    def move(x, y):
                        return ["mousemove", "--window", window, str(x), str(y)]
                    if action == "click":
                        events = move(*coords) + ["sleep", ".08", "mousedown", "1", "sleep", ".12", "mouseup", "1"]
                    elif action == "right":
                        events = move(*coords) + ["sleep", ".08", "click", "3"]
                    elif action == "paste":
                        events = ["key", "--clearmodifiers", "ctrl+a", "ctrl+v"]
                    elif action == "enter":
                        events = ["key", "Return"]
                    elif action == "tab":
                        events = ["keydown", "Tab", "sleep", ".12", "keyup", "Tab"]
                    elif action == "shift-tab":
                        events = ["keydown", "Shift_L", "keydown", "Tab", "sleep", ".12", "keyup", "Tab", "keyup", "Shift_L"]
                    elif action == "drag":
                        x1, y1, x2, y2 = map(int, coords)
                        events = move(x1, y1) + ["sleep", ".1", "mousedown", "1", "sleep", ".28"]
                        for step in range(1, 8):
                            events += move(round(x1 + (x2-x1)*step/7), round(y1 + (y2-y1)*step/7)) + ["sleep", ".07"]
                        events += ["sleep", ".28", "mouseup", "1"]
                    elif action == "move":
                        events = move(*coords)
                    else:
                        raise ValueError(action)
                    subprocess.run(["xdotool", *events], env=env, check=True, timeout=6)
                    path.with_suffix(".mouse.done").touch()
                    seq += 1
                if game.poll() is None:
                    raise TimeoutError("WorldBox did not finish the interaction script.")
            finally:
                if game.poll() is None:
                    game.terminate()
                    game.wait(timeout=5)
        response = result.read_text()
        print(response, end="")
        if game.returncode:
            return game.returncode
        if any(item["Level"] == "error" for item in json.loads(response)["result"]["Events"]):
            return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
