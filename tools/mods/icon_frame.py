#!/usr/bin/env python3
import argparse
import os

from PIL import Image

GRID = 40
SCALE = 2
BAND = 4
PALETTE_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "resurrect-64.hex")


def load_palette():
    with open(PALETTE_FILE, encoding="utf-8") as f:
        return [tuple(int(line[i:i + 2], 16) for i in (0, 2, 4)) for line in (l.strip() for l in f) if line]


PALETTE = load_palette()


def hexc(code):
    color = tuple(int(code[i:i + 2], 16) for i in (0, 2, 4))
    if color not in PALETTE:
        raise ValueError(f"#{code} is not in {os.path.basename(PALETTE_FILE)}")
    return color + (255,)


def snap(color):
    r, g, b = color[:3]
    best = min(PALETTE, key=lambda p: (p[0] - r) ** 2 * 0.3 + (p[1] - g) ** 2 * 0.59 + (p[2] - b) ** 2 * 0.11)
    return best + ((color[3],) if len(color) > 3 else ())


OUTLINE = hexc("2e222f")
INNER = hexc("3e3546")
WOOD_DEEP = hexc("6e2727")
WOOD_DARK = hexc("7a3045")
WOOD = hexc("9e4539")
WOOD_LIGHT = hexc("cd683d")
WOOD_SHINE = hexc("e6904e")
STEM = hexc("165a4c")
LEAF_DARK = hexc("239063")
LEAF = hexc("1ebc73")
LEAF_LIGHT = hexc("91db69")
LEAF_SHINE = hexc("cddf6c")
PETAL = hexc("ffffff")
PETAL_SHADE = hexc("c7dcd0")
POLLEN = hexc("f9c22b")
POTATO_LIGHT = hexc("fdcbb0")
POTATO_MID = hexc("ab947a")
POTATO_SHADE = hexc("966c6c")
POTATO_DARK = hexc("694f62")
SOIL = hexc("4c3e24")
SOIL_LIGHT = hexc("676633")

LAST = GRID - 1
UP_LEAF = [".hl.", "lggd"]
DOWN_LEAF = ["lggd", ".gd."]

VINES = [
    ("top", range(14, 38), [(16, "up"), (19, "down"), (23, "up"), (26, "down"), (30, "up"), (33, "down"), (35, "up")], [(28, 2), (36, 2)]),
    ("right", range(3, 27), [(6, "up"), (9, "down"), (13, "up"), (16, "down"), (20, "up"), (24, "down")], [(4, 2), (11, 2)]),
    ("bottom", range(4, 28), [(25, "up"), (22, "down"), (18, "up"), (14, "down"), (10, "up"), (6, "down")], [(12, 2), (4, 2)]),
    ("left", range(15, 37), [(34, "up"), (30, "down"), (26, "up"), (22, "down"), (18, "up")], [(35, 2), (28, 2)]),
]

SOCKET = (33.5, 33.5)
SOCKET_RADIUS = 6.2

POTATO = [
    "...lG...",
    "..gsGg..",
    "...s....",
    ".oooooo.",
    "oLLMMMSo",
    "oLMeMMSo",
    "oMMMMeSo",
    ".oSSSSo.",
]


def frame_image():
    img = Image.new("RGBA", (GRID, GRID), (0, 0, 0, 0))
    px = img.load()
    wood(px)
    socket(px)
    vines(px)
    potato(px)
    return img


def wood(px):
    for y in range(GRID):
        for x in range(GRID):
            edge = min(x, y, LAST - x, LAST - y)
            if edge >= BAND:
                continue
            corner = min(x, LAST - x) + min(y, LAST - y)
            if corner == 0:
                continue
            horizontal = min(y, LAST - y) <= min(x, LAST - x)
            along = x if horizontal else y
            if edge == 0 or corner == 1:
                color = OUTLINE
            elif edge == 3:
                color = INNER
            elif edge == 1:
                lit = (y == 1 and horizontal) or (x == 1 and not horizontal)
                color = WOOD_SHINE if lit else WOOD_DARK
            else:
                lit = (y == 2 and horizontal) or (x == 2 and not horizontal)
                grain = (along * 5 + (0 if horizontal else 3)) % 13 in (0, 1)
                color = WOOD_LIGHT if lit and not grain else (WOOD_DARK if grain else WOOD)
            if min(x, LAST - x) == min(y, LAST - y) and 0 < edge < 3:
                color = WOOD_DEEP
            px[x, y] = color


def socket(px):
    cx, cy = SOCKET
    r = SOCKET_RADIUS
    for y in range(GRID):
        for x in range(GRID):
            d = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5
            if d > r + 0.5:
                continue
            if d > r - 0.5:
                px[x, y] = OUTLINE
            elif d > r - 1.5:
                px[x, y] = WOOD_SHINE if x + y < cx + cy - 3 else (WOOD_LIGHT if x + y < cx + cy + 2 else WOOD_DARK)
            elif d > r - 2.5:
                px[x, y] = INNER
            else:
                px[x, y] = SOIL_LIGHT if y < cy - 1 else SOIL


def at(side, along, depth):
    if side == "top":
        return along, depth
    if side == "bottom":
        return along, LAST - depth
    if side == "left":
        return depth, along
    return LAST - depth, along


def inside(x, y):
    return 0 <= x < GRID and 0 <= y < GRID and min(x, y, LAST - x, LAST - y) <= BAND


def wave(i):
    return (2, 2, 1, 1, 2, 3, 3, 2)[i % 8]


def vines(px):
    for side, span, leaves, flowers in VINES:
        span = list(span)
        for i, along in enumerate(span):
            x, y = at(side, along, wave(i))
            px[x, y] = STEM
            if i + 1 < len(span) and wave(i + 1) != wave(i):
                bx, by = at(side, span[i + 1], wave(i))
                px[bx, by] = STEM
        for along, place in leaves:
            if along not in span:
                continue
            depth = wave(span.index(along))
            leaf(px, side, along, depth, place)
        for along, depth in flowers:
            flower(px, *at(side, along, depth))


def leaf(px, side, along, depth, place):
    colors = {"h": LEAF_SHINE, "l": LEAF_LIGHT, "g": LEAF, "d": LEAF_DARK}
    shape = UP_LEAF if place == "up" else DOWN_LEAF
    rows = [(depth - 2 + r, row) for r, row in enumerate(shape)] if place == "up" else [(depth + 1 + r, row) for r, row in enumerate(shape)]
    flip = side in ("bottom", "left")
    for d, row in rows:
        if d < 0 or d > BAND:
            continue
        for c, key in enumerate(row):
            if key not in colors:
                continue
            offset = c - 1
            x, y = at(side, along + (-offset if flip else offset), d)
            if inside(x, y):
                px[x, y] = colors[key]


def flower(px, cx, cy):
    for ox, oy, color in ((0, -1, PETAL), (-1, 0, PETAL), (1, 0, PETAL), (0, 1, PETAL_SHADE)):
        x, y = cx + ox, cy + oy
        if inside(x, y):
            px[x, y] = color
    px[cx, cy] = POLLEN


def potato(px):
    colors = {"o": OUTLINE, "L": POTATO_LIGHT, "M": POTATO_MID, "S": POTATO_SHADE, "e": POTATO_DARK,
              "g": LEAF, "G": LEAF_LIGHT, "l": LEAF_SHINE, "s": STEM}
    ox = int(SOCKET[0]) - len(POTATO[0]) // 2 + 1
    oy = int(SOCKET[1]) - len(POTATO) // 2 + 1
    for row, line in enumerate(POTATO):
        for col, key in enumerate(line):
            x, y = ox + col, oy + row
            if key in colors and 0 <= x < GRID and 0 <= y < GRID:
                px[x, y] = colors[key]


def apply(art, frame):
    if art.size != (GRID, GRID):
        if art.size == (GRID - 2 * BAND, GRID - 2 * BAND):
            canvas = Image.new("RGBA", (GRID, GRID), (0, 0, 0, 0))
            canvas.paste(art, (BAND, BAND))
            art = canvas
        else:
            raise SystemExit(f"art must be {GRID}x{GRID} or {GRID - 2 * BAND}x{GRID - 2 * BAND} pixels, got {art.size[0]}x{art.size[1]}")
    icon = Image.new("RGBA", (GRID, GRID), (0, 0, 0, 0))
    mask = Image.new("L", (GRID, GRID), 0)
    mp = mask.load()
    for y in range(GRID):
        for x in range(GRID):
            if min(x, LAST - x) + min(y, LAST - y) > 0:
                mp[x, y] = 255
    icon.paste(art, (0, 0), mask)
    icon.alpha_composite(frame)
    return icon


def off_palette(img):
    return sorted({p[:3] for p in img.getdata() if len(p) < 4 or p[3] > 0} - set(PALETTE))


def main():
    parser = argparse.ArgumentParser(description="Frame shared by the icons of our mods: 40x40 art grid in the Resurrect 64 palette, icon.png at 2x and icon_small.png at 1x.")
    parser.add_argument("art", nargs="?", help=f"{GRID}x{GRID} art (or {GRID - 2 * BAND}x{GRID - 2 * BAND}, placed inside the frame)")
    parser.add_argument("out", nargs="?", help="mod folder that receives icon.png and icon_small.png")
    parser.add_argument("--frame", help="also write the bare frame to this PNG")
    args = parser.parse_args()
    frame = frame_image()
    if args.frame:
        frame.save(args.frame)
        frame.resize((GRID * 8, GRID * 8), Image.NEAREST).save(os.path.splitext(args.frame)[0] + "@8x.png")
    if not args.art:
        return
    icon = apply(Image.open(args.art).convert("RGBA"), frame)
    stray = off_palette(icon)
    if stray:
        print(f"warning: {len(stray)} colors outside the palette, e.g. {['#%02x%02x%02x' % c for c in stray[:4]]}")
    out = args.out or os.path.dirname(os.path.abspath(args.art))
    icon.resize((GRID * SCALE, GRID * SCALE), Image.NEAREST).save(os.path.join(out, "icon.png"))
    icon.save(os.path.join(out, "icon_small.png"))
    print(f"{out}: icon.png {GRID * SCALE}x{GRID * SCALE}, icon_small.png {GRID}x{GRID}")


if __name__ == "__main__":
    main()
