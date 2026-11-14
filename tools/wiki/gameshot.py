#!/usr/bin/env python3
"""Crops, marks and frames a game or app screenshot for the wiki.

Usage:
  python tools/wiki/gameshot.py <in.png | adb:SERIAL> <out.webp|out.png>
         [--crop X,Y,W,H] [--zoom N] [--width W] [--mark X,Y,W,H] ... [--lossless]

--crop and --mark use the pixels of the source screenshot. Each --mark draws a
yellow box with the next number on it, matching the numbers in the text;
end it with @l, @t, @r, @b (outside) or @tl, @tr, @bl, @br (inside) to place the number.
"""
import argparse
import io
import os
import subprocess
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import codeshot

OUTLINE = (0x13, 0x16, 0x25)
FRAME = (0x4E, 0x52, 0x75)


def box(spec):
    x, y, w, h = (int(v) for v in spec.split(","))
    return x, y, w, h


def grab(source):
    if source.startswith("adb:"):
        data = subprocess.run(["adb", "-s", source[4:], "exec-out", "screencap", "-p"],
                              check=True, capture_output=True).stdout
        return Image.open(io.BytesIO(data)).convert("RGB")
    return Image.open(source).convert("RGB")


def frame(image, unit):
    w, h = image.size
    cw, ch = w // unit + 4, h // unit + 4
    small = Image.new("RGBA", (cw, ch), OUTLINE + (255,))
    ImageDraw.Draw(small).rectangle([1, 1, cw - 2, ch - 2], fill=FRAME + (255,))
    small.putalpha(codeshot.stepped_mask(cw, ch))
    framed = small.resize((cw * unit, ch * unit), Image.NEAREST)
    inner = (2 * unit + (cw * unit - 4 * unit - w) // 2, 2 * unit + (ch * unit - 4 * unit - h) // 2)
    framed.paste(image, inner)
    return framed


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("source")
    parser.add_argument("out")
    parser.add_argument("--crop")
    parser.add_argument("--zoom", type=int, default=1)
    parser.add_argument("--width", type=int)
    parser.add_argument("--mark", action="append", default=[])
    parser.add_argument("--lossless", action="store_true")
    args = parser.parse_args()

    image = grab(args.source)
    sides = [m.partition("@")[2] for m in args.mark]
    marks = [box(m.partition("@")[0]) for m in args.mark]
    if args.crop:
        x, y, w, h = box(args.crop)
        image = image.crop((x, y, x + w, y + h))
        marks = [(mx - x, my - y, mw, mh) for mx, my, mw, mh in marks]
    factor = float(args.zoom)
    if args.zoom > 1:
        image = image.resize((image.width * args.zoom, image.height * args.zoom), Image.NEAREST)
    if args.width and image.width > args.width:
        ratio = args.width / image.width
        image = image.resize((args.width, round(image.height * ratio)), Image.LANCZOS)
        factor *= ratio
    unit = 4
    draw = ImageDraw.Draw(image)
    boxes = [(round(mx * factor), round(my * factor), round((mx + mw) * factor), round((my + mh) * factor))
             for mx, my, mw, mh in marks]
    for x0, y0, x1, y1 in boxes:
        draw.rectangle([x0 - unit, y0 - unit, x1 + unit, y1 + unit], outline=codeshot.INK, width=unit)
        draw.rectangle([x0, y0, x1, y1], outline=codeshot.YELLOW, width=unit)
    taken = [(x0 - unit, y0 - unit, x1 + unit, y1 + unit) for x0, y0, x1, y1 in boxes]
    for number, (x0, y0, x1, y1) in enumerate(boxes, start=1):
        badge = codeshot.pixel_badge(number, 9 * unit, codeshot.title_font(11 * 2))
        size, gap = badge.width, 3 * unit
        middle_y, middle_x = (y0 + y1 - size) // 2, (x0 + x1 - size) // 2
        spots = {"l": (x0 - gap - size, middle_y), "t": (x0, y0 - gap - size), "r": (x1 + gap, middle_y),
                 "b": (x0, y1 + gap), "tl": (x0 + gap, y0 + gap), "tr": (x1 - gap - size, y0 + gap),
                 "bl": (x0 + gap, y1 - gap - size), "br": (x1 - gap - size, y1 - gap - size)}
        side = sides[number - 1]
        if side in spots:
            bx, by = spots[side]
            taken.append((bx, by, bx + size, by + size))
            image.paste(badge, (bx, by), badge)
            continue
        candidates = [spots["l"], spots["t"], spots["r"], spots["b"], (middle_x, middle_y)]

        def fits(spot):
            bx, by = spot
            if bx < 0 or by < 0 or bx + size > image.width or by + size > image.height:
                return False
            return all(bx + size <= a or bx >= c or by + size <= b or by >= d for a, b, c, d in taken)

        bx, by = next((spot for spot in candidates if fits(spot)), candidates[-1])
        taken.append((bx, by, bx + size, by + size))
        image.paste(badge, (bx, by), badge)
    result = frame(image, unit)
    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    if args.out.lower().endswith(".webp"):
        result.save(args.out, "WEBP", lossless=args.lossless, quality=90, method=6)
    else:
        result.save(args.out, "PNG", optimize=True)
    print(f"{args.out}: {result.width}x{result.height}, {os.path.getsize(args.out) // 1024} KB")


if __name__ == "__main__":
    main()
