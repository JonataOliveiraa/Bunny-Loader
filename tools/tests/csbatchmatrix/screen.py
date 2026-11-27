import os
import re
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "cstexdata"))
from screen import read_png

TOLERANCE = 24
SCREEN = re.compile(r"csbatchmatrix screen ([-\d.]+),([-\d.]+) ([-\d.]+),([-\d.]+)")
CELL = re.compile(r"csbatchmatrix (cell|absent) (\w+) (\d+),(\d+) (\d+),(\d+),(\d+)")


def main(shot, log):
    width, height, channels, rows = read_png(shot)
    text = open(log, encoding="utf-8", errors="replace").read()
    screen = SCREEN.search(text)
    cells = CELL.findall(text)
    if not screen or not cells:
        print("no 'csbatchmatrix screen' or 'csbatchmatrix cell' line in the log")
        return 1

    left, top, right, bottom = (float(v) for v in screen.groups())
    bad = 0
    for kind, name, x, y, r, g, b in cells:
        px = int((int(x) - left) / (right - left) * width)
        py = int((int(y) - top) / (bottom - top) * height)
        got = tuple(rows[py][px * channels:px * channels + 3])
        want = (int(r), int(g), int(b))
        same = all(abs(a - c) <= TOLERANCE for a, c in zip(got, want))
        ok = same if kind == "cell" else not same
        bad += not ok
        print(f"{kind} {name} at {px},{py}: {got}, {'expected' if kind == 'cell' else 'must not be'} {want} {'ok' if ok else 'FAIL'}")

    print("screen OK" if bad == 0 else f"screen FAIL: {bad} of {len(cells)} cells")
    return 0 if bad == 0 else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1], sys.argv[2]))
