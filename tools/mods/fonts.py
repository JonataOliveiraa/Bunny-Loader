"""Gera as fontes de um pacote do Bunny Loader (content/Fonts, formato blfont/1).

    python tools/mods/fonts.py ttf Fonte.ttf --out "MeuPacote/content/Fonts"
    python tools/mods/fonts.py xnb "pasta/com/os/xnb" --out "MeuPacote/content/Fonts"

`ttf` rasteriza um .ttf/.otf com N vezes mais pixels que o jogo (--scale) e
encaixa a letra nas medidas das fontes do celular (altura de maiuscula, linha
de base e espacamento de linha), entao a interface nao muda de lugar. E o
caminho nitido.

`xnb` converte os .xnb de um resource pack do PC (ReLogic DynamicSpriteFont,
sem compressao) em PNG + JSON, na resolucao original (scale 1): fica igual ao
PC, nao mais nitido que isso.

Arquivos gerados: <Fonte>.json + <Fonte>.<pagina>.png para Mouse_Text,
Item_Stack, Death_Text, Combat_Text e Combat_Crit.

Fonte propria de um mod (ModContent.Request('Assets/Fonts/MinhaFonte')): uma
so, com as medidas de uma do jogo e outro nome:

    python tools/mods/fonts.py ttf Fonte.ttf --only Mouse_Text --name MinhaFonte --out "MeuMod/content/Assets/Fonts"
"""
from __future__ import annotations

import argparse
import json
import math
import struct
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

FORMAT = "blfont/1"
FIELDS = "code,page,bx,by,bw,bh,cx,cy,cw,ch,leftBearing,width,rightBearing"
MAX_PAGE = 2048

# Medidas das fontes do Terraria Mobile 1.4.5.8.6 (tools/tests/fontprobe), em
# pixels logicos: altura da maiuscula, linha de base a partir do topo da linha,
# espacamento de linha e a escala padrao do atlas.
TARGETS = {
    "Mouse_Text": {"cap": 14, "baseline": 16, "lineSpacing": 24, "scale": 3},
    "Item_Stack": {"cap": 12, "baseline": 15, "lineSpacing": 21, "scale": 3},
    "Death_Text": {"cap": 34, "baseline": 41, "lineSpacing": 59, "scale": 2},
    "Combat_Text": {"cap": 15, "baseline": 21, "lineSpacing": 26, "scale": 3},
    "Combat_Crit": {"cap": 19, "baseline": 26, "lineSpacing": 32, "scale": 3},
}

PRESETS = {
    "latin": "0020-007E,00A0-024F,2010-2027,2030-203A,20A0-20BF,2122",
    "europe": "0020-007E,00A0-052F,2010-2027,2030-203A,20A0-20BF,2122,2190-21FF",
    "all": "",
}


def parse_ranges(text: str):
    ranges = []
    for part in filter(None, (p.strip() for p in text.split(","))):
        low, _, high = part.partition("-")
        ranges.append((int(low, 16), int(high or low, 16)))
    return ranges


def in_ranges(code: int, ranges) -> bool:
    return not ranges or any(low <= code <= high for low, high in ranges)


class Packer:
    """Prateleiras numa pagina de ate MAX_PAGE, com posicoes multiplas de `step`."""

    def __init__(self, step: int, padding: int):
        self.step, self.padding = step, padding
        self.side = MAX_PAGE // step * step
        self.pages: list[list] = []
        self._new()

    def _new(self):
        self.pages.append([])
        self.x = self.y = self.padding
        self.shelf = 0

    def _align(self, v: int) -> int:
        return -(-v // self.step) * self.step

    def place(self, w: int, h: int):
        if w + 2 * self.padding > self.side or h + 2 * self.padding > self.side:
            raise ValueError(f"glyph {w}x{h} does not fit a {self.side} page")
        if self.x + w + self.padding > self.side:
            self.x = self.padding
            self.y = self._align(self.y + self.shelf + self.padding)
            self.shelf = 0
        if self.y + h + self.padding > self.side:
            self._new()
        spot = (len(self.pages) - 1, self.x, self.y)
        self.x = self._align(self.x + w + self.padding)
        self.shelf = max(self.shelf, h)
        return spot


def write_font(out: Path, name: str, scale: int, line_spacing: int, glyphs: list, packer: Packer):
    """glyphs: (code, image RGBA hi-res ou None, cropping logico, [left, width, right] logico)."""
    sizes = [0] * len(packer.pages)
    placed = []
    for code, image, cropping, kerning in glyphs:
        if image is None:
            placed.append((code, 0, 0, 0, 0, 0, cropping, kerning))
            continue
        page, x, y = packer.spots[code]
        sizes[page] = max(sizes[page], y + image.height + packer.padding)
        placed.append((code, page, x, y, image.width, image.height, cropping, kerning))
    pages = []
    for index, used in enumerate(sizes):
        height = min(packer.side, -(-max(used, scale) // scale) * scale)
        canvas = Image.new("RGBA", (packer.side, height), (255, 255, 255, 0))
        for code, image, *_ in glyphs:
            if image is not None and packer.spots[code][0] == index:
                canvas.paste(image, packer.spots[code][1:])
        file = f"{name}.{index}.png"
        canvas.save(out / file, optimize=True)
        pages.append(file)
    records = []
    for code, page, x, y, w, h, cropping, kerning in sorted(placed):
        records.append([code, page, x // scale, y // scale, w // scale, h // scale, *cropping,
                        round(kerning[0], 3), round(kerning[1], 3), round(kerning[2], 3)])
    meta = {"format": FORMAT, "scale": scale, "lineSpacing": line_spacing, "spacing": 0,
            "pages": pages, "fields": FIELDS, "glyphs": records}
    (out / f"{name}.json").write_text(json.dumps(meta, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    kb = sum((out / p).stat().st_size for p in pages) / 1024
    print(f"  {name}: {len(records)} glyphs, scale {scale}, {len(pages)} page(s), {kb:.0f} KB")


def pack_glyphs(glyphs: list, scale: int) -> Packer:
    packer = Packer(scale, 2 * scale)
    packer.spots = {}
    drawable = [g for g in glyphs if g[1] is not None]
    for code, image, *_ in sorted(drawable, key=lambda g: (-g[1].height, -g[1].width)):
        packer.spots[code] = packer.place(image.width, image.height)
    return packer


# ---- TTF/OTF ----

def font_codepoints(path: Path) -> set[int]:
    from fontTools.ttLib import TTFont
    with TTFont(path, fontNumber=0, lazy=True) as font:
        return {code for table in font["cmap"].tables if table.isUnicode() for code in table.cmap}


def cap_height(path: Path, size: float) -> float:
    font = ImageFont.truetype(str(path), size)
    left, top, right, bottom = font.getbbox("H", anchor="ls")
    return -top


# Acento combinante -> o caractere de espacamento que o desenha sozinho.
MARKS = {0x300: "`", 0x301: "´", 0x302: "ˆ", 0x303: "˜", 0x304: "¯", 0x306: "˘",
         0x307: "˙", 0x308: "¨", 0x30A: "˚", 0x30B: "˝", 0x30C: "ˇ",
         0x327: "¸", 0x328: "˛"}
DEFAULT_ACCENTS = Path(__file__).resolve().parents[2] / "app" / "src" / "fontFallback" / "res" / "font" / "bunny.ttf"


def composable(code: int, available: set[int], accents: set[int]):
    """(base, [acentos]) quando a letra decompoe em base da fonte + acentos que a fonte de acentos tem."""
    import unicodedata
    parts = unicodedata.normalize("NFD", chr(code))
    if len(parts) < 2 or ord(parts[0]) not in available:
        return None
    marks = [MARKS.get(ord(m)) for m in parts[1:]]
    if any(m is None or ord(m) not in accents for m in marks):
        return None
    return parts[0], marks


class Renderer:
    """Desenha cada caractere numa tela com a origem (caneta, linha de base) fixa."""

    def __init__(self, font, accent_font, size_px: float, stroke: int):
        self.font, self.accent_font, self.stroke = font, accent_font, stroke
        side = int(size_px * 4) + 8
        self.size = (side, side)
        self.origin = (side // 4, int(side * 0.62))
        xb = font.getbbox("x", anchor="ls")
        self.x_top = xb[1] if xb[3] > xb[1] else -size_px * 0.5

    def _draw(self, font, char, offset=(0, 0)):
        canvas = Image.new("L", self.size)
        ImageDraw.Draw(canvas).text((self.origin[0] + offset[0], self.origin[1] + offset[1]), char, font=font,
                                    fill=255, anchor="ls", stroke_width=self.stroke, stroke_fill=255)
        return canvas

    def plain(self, char):
        return self._draw(self.font, char), self.font.getlength(char)

    def composed(self, base, marks):
        from PIL import ImageChops
        canvas = self._draw(self.font, base)
        if base in "ij":   # tira o pingo antes do acento
            cut = self.origin[1] + int(self.x_top) - max(1, self.stroke)
            canvas.paste(0, (0, 0, self.size[0], max(0, cut)))
        for mark in marks:
            box = canvas.getbbox()
            accent = self._draw(self.accent_font, mark)
            abox = accent.getbbox()
            if not box or not abox:
                continue
            gap = max(1, round((box[3] - box[1]) * 0.06))
            dx = round((box[0] + box[2]) / 2 - (abox[0] + abox[2]) / 2)
            below = mark in ("¸", "˛")
            dy = (box[3] - abox[1] - gap) if below else (box[1] - gap - abox[3])
            moved = Image.new("L", self.size)
            moved.paste(accent, (dx, dy))
            canvas = ImageChops.lighter(canvas, moved)
        return canvas, self.font.getlength(base)


def from_ttf(args):
    path = Path(args.font)
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    ranges = parse_ranges(args.ranges if args.ranges is not None else PRESETS[args.preset])
    excluded = parse_ranges(args.exclude or "")
    wanted = lambda c: 0x20 <= c < 0xFFFF and in_ranges(c, ranges) and not (excluded and in_ranges(c, excluded))
    available = font_codepoints(path)
    codes = {c: None for c in available if wanted(c)}
    accent_path = Path(args.compose_accents) if args.compose_accents else None
    if accent_path:
        accents = font_codepoints(accent_path)
        for low, high in ranges or [(0x20, 0x24F)]:
            for c in range(low, min(high, 0xFFFE) + 1):
                if c not in codes and wanted(c) and (parts := composable(c, available, accents)):
                    codes[c] = parts
    if not codes:
        raise SystemExit("no code point of the selected ranges exists in this font")
    reference = cap_height(path, 1000) / 1000
    if args.name and len(args.only or []) != 1:
        raise SystemExit("--name needs exactly one --only font (the measurements to copy)")
    composed = sum(1 for v in codes.values() if v)
    print(f"{path.name}: {len(codes)} glyphs" + (f" ({composed} with composed accents)" if composed else ""))
    accent_reference = cap_height(accent_path, 1000) / 1000 if accent_path else 1
    for name, target in TARGETS.items():
        if args.only and name not in args.only:
            continue
        scale = args.scale or target["scale"]
        size = target["cap"] / reference * args.size_factor
        font = ImageFont.truetype(str(path), size * scale)
        accent_font = ImageFont.truetype(str(accent_path), target["cap"] / accent_reference * args.size_factor * scale)             if accent_path else None
        renderer = Renderer(font, accent_font, size * scale, args.stroke)
        baseline = target["baseline"]
        glyphs = []
        for code in sorted(codes):
            canvas, advance = renderer.composed(*codes[code]) if codes[code] else renderer.plain(chr(code))
            advance /= scale
            box = canvas.getbbox()
            if not box:
                glyphs.append((code, None, [0, 0, 0, target["lineSpacing"]], [0.0, 0.0, advance]))
                continue
            ox, oy = renderer.origin
            l, t, r, b = box[0] - ox, box[1] - oy, box[2] - ox, box[3] - oy
            left, top = math.floor(l / scale), math.floor(t / scale)
            width, height = math.ceil(r / scale) - left, math.ceil(b / scale) - top
            x0, y0 = ox + left * scale, oy + top * scale
            mask = canvas.crop((x0, y0, x0 + width * scale, y0 + height * scale))
            if args.gamma != 1:
                mask = mask.point([round(255 * (i / 255) ** (1 / args.gamma)) for i in range(256)])
            image = Image.new("RGBA", mask.size, (255, 255, 255, 0))
            image.putalpha(mask)
            cropping = [0, baseline + top, width, target["lineSpacing"]]
            glyphs.append((code, image, cropping, [float(left), float(width), advance - left - width]))
        write_font(out, args.name or name, scale, target["lineSpacing"], glyphs, pack_glyphs(glyphs, scale))


# ---- XNB do PC ----

class Reader:
    def __init__(self, data: bytes, pos: int = 0):
        self.data, self.pos = data, pos

    def take(self, n: int) -> bytes:
        chunk = self.data[self.pos:self.pos + n]
        if len(chunk) != n:
            raise ValueError("truncated XNB")
        self.pos += n
        return chunk

    def u8(self): return self.take(1)[0]
    def i32(self): return struct.unpack("<i", self.take(4))[0]
    def f32(self): return struct.unpack("<f", self.take(4))[0]

    def var(self) -> int:
        value = shift = 0
        while True:
            b = self.u8()
            value |= (b & 0x7F) << shift
            shift += 7
            if not b & 0x80:
                return value

    def text(self) -> str:
        return self.take(self.var()).decode("utf-8")

    def char(self) -> str:
        first = self.data[self.pos]
        n = 1 if first < 0x80 else 2 if first < 0xE0 else 3 if first < 0xF0 else 4
        return self.take(n).decode("utf-8")


def decode_dxt(data: bytes, width: int, height: int, fmt: int) -> np.ndarray:
    """SurfaceFormat 4 Dxt1, 5 Dxt3, 6 Dxt5 -> RGBA (h, w, 4)."""
    block = 8 if fmt == 4 else 16
    bw, bh = (width + 3) // 4, (height + 3) // 4
    raw = np.frombuffer(data[:bw * bh * block], dtype=np.uint8).reshape(bh * bw, block)
    color = raw[:, block - 8:]
    c0 = color[:, 0].astype(np.uint32) | (color[:, 1].astype(np.uint32) << 8)
    c1 = color[:, 2].astype(np.uint32) | (color[:, 3].astype(np.uint32) << 8)

    def rgb(c):
        r, g, b = (c >> 11) & 31, (c >> 5) & 63, c & 31
        return np.stack([(r << 3) | (r >> 2), (g << 2) | (g >> 4), (b << 3) | (b >> 3)], -1).astype(np.int32)

    a, b = rgb(c0), rgb(c1)
    palette = np.stack([a, b, (2 * a + b) // 3, (a + 2 * b) // 3], 1)
    if fmt == 4:
        punch = c0 <= c1
        palette[punch, 2] = (a[punch] + b[punch]) // 2
        palette[punch, 3] = 0
    bits = color[:, 4].astype(np.uint32) | (color[:, 5].astype(np.uint32) << 8) | \
        (color[:, 6].astype(np.uint32) << 16) | (color[:, 7].astype(np.uint32) << 24)
    index = (bits[:, None] >> (2 * np.arange(16, dtype=np.uint32))) & 3
    rgbs = np.take_along_axis(palette, index[:, :, None].astype(np.int64), 1)
    alpha = np.full((bh * bw, 16), 255, dtype=np.int32)
    if fmt == 5:
        abits = raw[:, :8].view("<u8")[:, 0]
        alpha = ((abits[:, None] >> (4 * np.arange(16, dtype=np.uint64))) & 15).astype(np.int32) * 17
    elif fmt == 6:
        a0, a1 = raw[:, 0].astype(np.int32), raw[:, 1].astype(np.int32)
        abits = np.zeros(len(raw), dtype=np.uint64)
        for i in range(6):
            abits |= raw[:, 2 + i].astype(np.uint64) << np.uint64(8 * i)
        aidx = ((abits[:, None] >> (3 * np.arange(16, dtype=np.uint64))) & 7).astype(np.int32)
        table = np.zeros((len(raw), 8), dtype=np.int32)
        table[:, 0], table[:, 1] = a0, a1
        eight = a0 > a1
        for i in range(1, 7):
            table[eight, i + 1] = ((7 - i) * a0[eight] + i * a1[eight]) // 7
        for i in range(1, 5):
            table[~eight, i + 1] = ((5 - i) * a0[~eight] + i * a1[~eight]) // 5
        table[~eight, 6], table[~eight, 7] = 0, 255
        alpha = np.take_along_axis(table, aidx, 1)
    elif fmt == 4:
        alpha = np.where(index == 3, np.where((c0 <= c1)[:, None], 0, 255), 255)
    pixels = np.concatenate([rgbs, alpha[:, :, None]], 2).reshape(bh, bw, 4, 4, 4)
    image = pixels.transpose(0, 2, 1, 3, 4).reshape(bh * 4, bw * 4, 4)[:height, :width]
    return image.astype(np.uint8)


def read_xnb_font(path: Path):
    data = path.read_bytes()
    if data[:3] != b"XNB" or data[4] != 5:
        raise ValueError(f"{path.name}: not an XNB v5 file")
    if data[5] & 0xC0:
        raise ValueError(f"{path.name}: compressed XNB; decompress it first (e.g. with xnbcli)")
    r = Reader(data, 10)
    readers = []
    for _ in range(r.var()):
        readers.append(r.text())
        r.i32()   # versao do leitor
    if not readers or "DynamicSpriteFontReader" not in readers[0]:
        raise ValueError(f"{path.name}: not a ReLogic DynamicSpriteFont")
    r.var()   # recursos compartilhados
    r.var()   # leitor do objeto principal
    spacing, line_spacing, default = r.f32(), r.i32(), r.char()
    pages = []
    for _ in range(r.i32()):
        r.var()
        fmt, width, height, mips = r.i32(), r.i32(), r.i32(), r.i32()
        size = r.i32()
        pixels = r.take(size)
        for _ in range(mips - 1):
            r.take(r.i32())
        if fmt == 0:
            image = np.frombuffer(pixels, dtype=np.uint8).reshape(height, width, 4).copy()
        elif fmt in (4, 5, 6):
            image = decode_dxt(pixels, width, height, fmt)
        else:
            raise ValueError(f"{path.name}: texture format {fmt} not supported")
        lists = []
        for kind in ("rect", "rect", "char", "vec3"):
            r.var()
            count = r.i32()
            if kind == "rect":
                lists.append([struct.unpack("<4i", r.take(16)) for _ in range(count)])
            elif kind == "char":
                lists.append([r.char() for _ in range(count)])
            else:
                lists.append([struct.unpack("<3f", r.take(12)) for _ in range(count)])
        pages.append((image, *lists))
    return spacing, line_spacing, default, pages


def unpremultiply(image: np.ndarray) -> np.ndarray:
    out = image.astype(np.float32)
    alpha = out[:, :, 3:4]
    out[:, :, :3] = np.where(alpha > 0, np.minimum(255, out[:, :, :3] * 255 / np.maximum(alpha, 1)), 255)
    return out.round().astype(np.uint8)


def from_xnb(args):
    source = Path(args.folder)
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    ranges = parse_ranges(args.ranges if args.ranges is not None else PRESETS[args.preset])
    for name in TARGETS:
        if args.only and name not in args.only:
            continue
        path = next((p for p in source.iterdir() if p.stem.lower() == name.lower() and p.suffix.lower() == ".xnb"), None)
        if path is None:
            print(f"  {name}: no {name}.xnb, skipped")
            continue
        _, line_spacing, _, pages = read_xnb_font(path)
        glyphs, seen = [], set()
        for image, bounds, cropping, chars, kerning in pages:
            rgba = unpremultiply(image)
            for (x, y, w, h), crop, char, (left, width, right) in zip(bounds, cropping, chars, kerning):
                code = ord(char)
                if code in seen or code >= 0xFFFF or not in_ranges(code, ranges):
                    continue
                seen.add(code)
                tile = Image.fromarray(rgba[y:y + h, x:x + w], "RGBA") if w > 0 and h > 0 else None
                glyphs.append((code, tile, list(crop), [left, width, right]))
        write_font(out, name, 1, line_spacing, glyphs, pack_glyphs(glyphs, 1))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    for command in ("ttf", "xnb"):
        p = sub.add_parser(command)
        p.add_argument("font" if command == "ttf" else "folder")
        p.add_argument("--out", required=True, help="content/Fonts folder of the package")
        p.add_argument("--preset", choices=PRESETS, default="latin")
        p.add_argument("--ranges", help="hex code point ranges, e.g. 0020-007E,0400-04FF (overrides --preset)")
        p.add_argument("--only", nargs="*", choices=TARGETS, help="generate only these fonts")
        if command == "ttf":
            p.add_argument("--scale", type=int, help="atlas pixels per game pixel (default: per font, 2-3)")
            p.add_argument("--size-factor", type=float, default=1.0, help="multiplies the fitted size")
            p.add_argument("--stroke", type=int, default=0, help="thickens strokes by N atlas pixels")
            p.add_argument("--gamma", type=float, default=1.0, help=">1 makes anti-aliased edges heavier")
            p.add_argument("--exclude", help="hex code point ranges to leave with the game's font")
            p.add_argument("--compose-accents", nargs="?", const=str(DEFAULT_ACCENTS), metavar="FONT",
                           help="build accented letters the font lacks: its base letter + the accent from FONT "
                                "(default: Comic Neue Bold)")
            p.add_argument("--name", help="file name of a mod's own font (with one --only)")
    args = parser.parse_args()
    (from_ttf if args.command == "ttf" else from_xnb)(args)


if __name__ == "__main__":
    sys.exit(main())
