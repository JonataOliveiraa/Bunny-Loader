import os
import re
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "cstexdata"))
from screen import read_png

TOLERANCE = 24
SCREEN = re.compile(r"cstprim screen (\d+)x(\d+)")
CELL = re.compile(r"cstprim cell (\w+) (\d+),(\d+) (\d+),(\d+),(\d+)")
WORLD = re.compile(r"cstprim worldcell (\w+) ([\d.]+),([\d.]+) (\d+),(\d+),(\d+)")


def main(shot, log):
    width, height, channels, rows = read_png(shot)
    text = open(log, encoding="utf-8", errors="replace").read()
    screen = SCREEN.search(text)
    cells = CELL.findall(text)
    if not screen or not cells:
        print("no 'cstprim screen' or 'cstprim cell' line in the log")
        return 1

    fx, fy = width / int(screen.group(1)), height / int(screen.group(2))
    points = [(name, int(int(x) * fx), int(int(y) * fy), r, g, b) for name, x, y, r, g, b in cells]
    points += [(name, int(float(x) * width), int(float(y) * height), r, g, b) for name, x, y, r, g, b in WORLD.findall(text)]
    bad = 0
    for name, px, py, r, g, b in points:
        got = tuple(rows[py][px * channels:px * channels + 3])
        want = (int(r), int(g), int(b))
        ok = all(abs(a - c) <= TOLERANCE for a, c in zip(got, want))
        bad += not ok
        print(f"{name} at {px},{py}: {got}, expected {want} {'ok' if ok else 'FAIL'}")
    print("screen OK" if bad == 0 else f"screen FAIL: {bad} of {len(points)} cells differ")
    return 0 if bad == 0 else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1], sys.argv[2]))
