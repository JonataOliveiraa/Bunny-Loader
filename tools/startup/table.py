import glob
import re
import statistics
import sys
from datetime import datetime


def stamp(line):
    return datetime.strptime("2026-" + line[:18], "%Y-%m-%d %H:%M:%S.%f").timestamp()


def parse(path):
    lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
    first = {}
    keys = {
        "content": "startup: game content loaded",
        "mods0": "loading mods from",
        "mods1": "probe:",
        "splash": "startup: splash over",
        "ready": "mod content: ready",
        "title": "startup: title on screen",
    }
    for line in lines:
        for key, text in keys.items():
            if key not in first and text in line and (key != "mods1" or re.search(r"probe: \d+ mod", line)):
                first[key] = line
    if "title" not in first:
        return None
    t = {k: stamp(v) for k, v in first.items()}
    return {
        "title": float(re.search(r"at ([\d.]+) s", first["title"]).group(1)),
        "game": float(re.search(r"at ([\d.]+) s", first["content"]).group(1)),
        "loadmod": t["mods1"] - t["mods0"],
        "init": t["splash"] - t["mods1"],
        "native": t["ready"] - t["splash"],
        "ready": t["title"] - t["ready"],
    }


for pattern in sys.argv[1:]:
    rows = [r for r in map(parse, sorted(glob.glob(pattern))) if r]
    print(pattern, f"n={len(rows)}")
    for key in ("title", "game", "loadmod", "init", "native", "ready"):
        values = [r[key] for r in rows]
        print(f"  {key:8s} median {statistics.median(values):6.1f}  {[round(v, 1) for v in values]}")
