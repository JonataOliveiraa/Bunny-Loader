#!/usr/bin/env python3
"""Renders a code snippet as a framed PNG for the wiki.

Usage: python tools/wiki/codeshot.py <code file> <out.png> [--title T] [--lang L] [--hl 3,5-7]

A comment like `// (1)` at the end of a line becomes a numbered badge.
"""
import argparse
import os
import re
import sys

from PIL import Image, ImageDraw, ImageFont
from pygments import lexers
from pygments.token import Token

SCALE = 2
UNIT = 2

BACKDROP_TOP = (0x12, 0x15, 0x2B)
BACKDROP_BOTTOM = (0x23, 0x2A, 0x50)
OUTLINE = (0x13, 0x16, 0x25)
HEADER = (0x4A, 0x5D, 0xAA)
HEADER_LIGHT = (0x6A, 0x7D, 0xC8)
HEADER_DARK = (0x35, 0x44, 0x82)
BODY = (0x1A, 0x1E, 0x39)
BODY_LIGHT = (0x24, 0x29, 0x4C)
GUTTER_LINE = (0x26, 0x2B, 0x4D)
LINE_NUMBER = (0x5D, 0x66, 0x94)
HIGHLIGHT = (0x2A, 0x32, 0x66)
YELLOW = (0xF9, 0xE1, 0x41)
YELLOW_DARK = (0xB8, 0x9E, 0x1E)
INK = (0x12, 0x15, 0x2B)
WHITE = (0xFF, 0xFF, 0xFF)
PILL = (0x51, 0x2B, 0xD4)

TEXT = (0xDC, 0xDC, 0xDC)
PUNCTUATION = (0xB4, 0xB4, 0xB4)
KEYWORD = (0x56, 0x9C, 0xD6)
CONTROL = (0xD8, 0xA0, 0xDF)
TYPE = (0x4E, 0xC9, 0xB0)
METHOD = (0xDC, 0xDC, 0xAA)
LOCAL = (0x9C, 0xDC, 0xFE)
STRING = (0xD6, 0x9D, 0x85)
NUMBER = (0xB5, 0xCE, 0xA8)
COMMENT = (0x6A, 0xA8, 0x5A)

CONTROL_WORDS = {
    "if", "else", "for", "foreach", "while", "do", "switch", "case", "default", "break", "continue",
    "return", "goto", "yield", "throw", "try", "catch", "finally", "await", "in",
}

LANGS = {
    "csharp": "csharp", "cs": "csharp", "c#": "csharp",
    "json": "json", "hjson": "hjson", "xml": "xml", "csproj": "xml",
    "bash": "bash", "sh": "bash", "shell": "bash", "powershell": "powershell", "ps": "powershell",
    "text": "text", "": "text",
}

LANG_LABELS = {"csharp": "C#", "json": "JSON", "hjson": "HJSON", "xml": "XML", "bash": "Shell",
               "powershell": "PowerShell", "text": "Text"}

CALLOUT = re.compile(r"//\s*\((\d+)\)\s*$|#\s*\((\d+)\)\s*$|<!--\s*\((\d+)\)\s*-->\s*$")

FONT_DIRS = [os.path.join(os.environ.get("WINDIR", "C:/Windows"), "Fonts"),
             "/usr/share/fonts/truetype/dejavu", "/usr/share/fonts/TTF", "/Library/Fonts"]


def find_font(names):
    for directory in FONT_DIRS:
        for name in names:
            path = os.path.join(directory, name)
            if os.path.isfile(path):
                return path
    sys.exit(f"none of these fonts was found: {', '.join(names)}")


def load_font(names, size, variation=None):
    font = ImageFont.truetype(find_font(names), size)
    if variation:
        try:
            font.set_variation_by_name(variation)
        except (OSError, ValueError):
            pass
    return font


def code_font(size):
    return load_font(["CascadiaMono.ttf", "consola.ttf", "DejaVuSansMono.ttf", "Menlo.ttc"], size, "Regular")


def title_font(size):
    return load_font(["segoeuib.ttf", "DejaVuSans-Bold.ttf", "Arial Bold.ttf"], size)


def lexer_for(lang):
    if lang == "csharp":
        return lexers.CSharpLexer()
    if lang == "json":
        return lexers.JsonLexer()
    if lang == "hjson":
        return lexers.YamlLexer()
    if lang == "xml":
        return lexers.XmlLexer()
    if lang == "bash":
        return lexers.BashLexer()
    if lang == "powershell":
        return lexers.PowerShellLexer()
    return lexers.TextLexer()


def base_color(token, value):
    if token in Token.Comment:
        return COMMENT
    if token in Token.Literal.String:
        return STRING
    if token in Token.Literal.Number:
        return NUMBER
    if token in Token.Keyword.Type or token in Token.Keyword.Constant:
        return KEYWORD
    if token in Token.Keyword:
        return CONTROL if value in CONTROL_WORDS else KEYWORD
    if token in Token.Name.Tag:
        return LOCAL
    if token in Token.Name.Attribute:
        return LOCAL
    if token in Token.Name.Class or token in Token.Name.Builtin:
        return TYPE
    if token in Token.Name.Function:
        return METHOD
    if token in Token.Name.Variable:
        return LOCAL
    if token in Token.Operator or token in Token.Punctuation:
        return PUNCTUATION
    return TEXT


def csharp_colors(tokens):
    significant = [i for i, (t, v) in enumerate(tokens) if v.strip() and t not in Token.Comment]
    position = {index: n for n, index in enumerate(significant)}
    colors = []
    for i, (token, value) in enumerate(tokens):
        color = base_color(token, value)
        if token is Token.Name and i in position:
            n = position[i]
            previous = tokens[significant[n - 1]][1] if n > 0 else ""
            following = tokens[significant[n + 1]][1] if n + 1 < len(significant) else ""
            if previous == ".":
                color = METHOD if following in ("(", "<") else TEXT
            elif following == "(" and previous != "new":
                color = METHOD
            elif value[:1].isupper():
                color = TYPE
            else:
                color = LOCAL
        colors.append(color)
    return colors


def colored_lines(code, lang):
    tokens = [(t, v) for t, v in lexer_for(lang).get_tokens(code)]
    colors = csharp_colors(tokens) if lang == "csharp" else [base_color(t, v) for t, v in tokens]
    lines = [[]]
    for (token, value), color in zip(tokens, colors):
        parts = value.split("\n")
        for k, part in enumerate(parts):
            if k:
                lines.append([])
            if part:
                lines[-1].append((part, color))
    while lines and not lines[-1]:
        lines.pop()
    return lines


def split_callouts(lines):
    result = []
    for line in lines:
        badge = None
        if line:
            text, color = line[-1]
            match = CALLOUT.search(text)
            if match and color == COMMENT:
                badge = int(match.group(1) or match.group(2) or match.group(3))
                rest = text[:match.start()].rstrip()
                line = line[:-1] + ([(rest, color)] if rest else [])
                while line and not line[-1][0].strip():
                    line = line[:-1]
                if line:
                    last_text, last_color = line[-1]
                    line[-1] = (last_text.rstrip(), last_color)
        result.append((line, badge))
    return result


def parse_lines(spec):
    chosen = set()
    for part in filter(None, (p.strip() for p in (spec or "").split(","))):
        if "-" in part:
            a, b = part.split("-", 1)
            chosen.update(range(int(a), int(b) + 1))
        else:
            chosen.add(int(part))
    return chosen


def dedent(code):
    lines = code.expandtabs(4).split("\n")
    widths = [len(l) - len(l.lstrip(" ")) for l in lines if l.strip()]
    cut = min(widths) if widths else 0
    return "\n".join(l[cut:] if l.strip() else "" for l in lines).strip("\n") + "\n"


def stepped_mask(w, h, steps=(4, 2, 1, 1)):
    mask = Image.new("L", (w, h), 255)
    px = mask.load()
    for row, inset in enumerate(steps):
        for x in range(inset):
            for y in (row, h - 1 - row):
                px[x, y] = 0
                px[w - 1 - x, y] = 0
    return mask


def pixel_badge(number, size, font):
    u = UNIT * SCALE
    cells = size // u
    small = Image.new("RGBA", (cells, cells), YELLOW + (255,))
    draw = ImageDraw.Draw(small)
    draw.rectangle([0, 0, cells - 1, cells - 1], outline=INK + (255,))
    draw.line([1, cells - 2, cells - 2, cells - 2], fill=YELLOW_DARK + (255,))
    draw.line([cells - 2, 1, cells - 2, cells - 2], fill=YELLOW_DARK + (255,))
    small.putalpha(Image.composite(small.getchannel("A"), Image.new("L", small.size, 0),
                                   stepped_mask(cells, cells, (2, 1))))
    badge = small.resize((cells * u, cells * u), Image.NEAREST)
    draw = ImageDraw.Draw(badge)
    label = str(number)
    box = draw.textbbox((0, 0), label, font=font)
    x = (badge.width - (box[2] - box[0])) / 2 - box[0]
    y = (badge.height - (box[3] - box[1])) / 2 - box[1] - SCALE * 0.5
    draw.text((x, y), label, font=font, fill=INK)
    return badge


def render(code, out_path, title="", lang="csharp", highlight=frozenset(), numbers=True):
    lang = LANGS.get(lang.lower(), lang.lower())
    code = dedent(code)
    lines = split_callouts(colored_lines(code, lang))
    s = SCALE
    u = UNIT * s
    font = code_font(14 * s)
    tfont = title_font(13 * s)
    pfont = title_font(11 * s)
    bfont = title_font(11 * s)
    char = font.getlength("M")
    line_h = 22 * s
    pad_outer = 28 * s
    header_h = 18 * u
    pad_x = 18 * s
    pad_y = 14 * s
    digits = len(str(len(lines)))
    gutter = (digits * char + 22 * s) if numbers else 0
    badge_size = 9 * u
    widths = []
    for line, badge in lines:
        w = sum(font.getlength(text) for text, _ in line)
        if badge is not None:
            w += 10 * s + badge_size
        widths.append(w)
    label = LANG_LABELS.get(lang, lang.upper())
    title_w = tfont.getlength(title) + pfont.getlength(label) + 60 * s if title else 0
    inner_w = max(max(widths, default=0) + gutter + 2 * pad_x, title_w, 320 * s)
    inner_h = len(lines) * line_h + 2 * pad_y
    card_w = -(-int(inner_w + 2 * u) // u) * u
    card_h = -(-int(header_h + inner_h + 2 * u) // u) * u
    width = card_w + 2 * pad_outer
    height = card_h + 2 * pad_outer

    image = Image.new("RGB", (width, height))
    top, bottom = BACKDROP_TOP, BACKDROP_BOTTOM
    gradient = ImageDraw.Draw(image)
    for y in range(height):
        t = y / max(1, height - 1)
        gradient.line([(0, y), (width, y)], fill=tuple(round(a + (b - a) * t) for a, b in zip(top, bottom)))

    cw, ch = card_w // u, card_h // u
    hh = header_h // u
    small = Image.new("RGBA", (cw, ch), OUTLINE + (255,))
    draw = ImageDraw.Draw(small)
    draw.rectangle([1, 1, cw - 2, hh], fill=HEADER + (255,))
    draw.line([1, 1, cw - 2, 1], fill=HEADER_LIGHT + (255,))
    draw.line([1, 1, 1, hh], fill=HEADER_LIGHT + (255,))
    draw.line([1, hh, cw - 2, hh], fill=HEADER_DARK + (255,))
    draw.rectangle([1, hh + 1, cw - 2, ch - 2], fill=BODY + (255,))
    draw.line([1, hh + 1, cw - 2, hh + 1], fill=OUTLINE + (255,))
    small.putalpha(stepped_mask(cw, ch))
    card = small.resize((card_w, card_h), Image.NEAREST)
    image.paste(card, (pad_outer, pad_outer), card)

    draw = ImageDraw.Draw(image)
    left = pad_outer + u
    body_top = pad_outer + header_h + 2 * u
    body_right = pad_outer + card_w - 2 * u
    code_x = left + gutter + pad_x
    if title:
        box = draw.textbbox((0, 0), title, font=tfont)
        ty = pad_outer + u + (header_h - u - (box[3] - box[1])) / 2 - box[1]
        draw.text((left + 14 * s, ty), title, font=tfont, fill=WHITE)
    pill_w = pfont.getlength(label) + 14 * s
    pill_h = 9 * u
    px1 = body_right - 10 * s
    py0 = pad_outer + u + (header_h - u - pill_h) // 2
    draw.rectangle([px1 - pill_w, py0, px1, py0 + pill_h], fill=PILL)
    box = draw.textbbox((0, 0), label, font=pfont)
    draw.text((px1 - pill_w + (pill_w - (box[2] - box[0])) / 2 - box[0],
               py0 + (pill_h - (box[3] - box[1])) / 2 - box[1] - s * 0.5), label, font=pfont, fill=WHITE)

    if numbers:
        gx = left + gutter
        draw.rectangle([gx, body_top, gx + u // 2 - 1, pad_outer + card_h - 2 * u - 1], fill=GUTTER_LINE)

    ascent = font.getmetrics()[0]
    for n, (line, badge) in enumerate(lines, start=1):
        y = body_top + pad_y + (n - 1) * line_h
        baseline = y + (line_h + ascent - font.getmetrics()[1]) // 2
        if n in highlight:
            draw.rectangle([left, y, body_right, y + line_h - 1], fill=HIGHLIGHT)
            draw.rectangle([left, y, left + u - 1, y + line_h - 1], fill=YELLOW)
        if numbers:
            number = str(n)
            nx = left + gutter - 12 * s - font.getlength(number)
            draw.text((nx, baseline), number, font=font, anchor="ls",
                      fill=YELLOW if n in highlight else LINE_NUMBER)
        x = code_x
        for text, color in line:
            draw.text((x, baseline), text, font=font, anchor="ls", fill=color)
            x += font.getlength(text)
        if badge is not None:
            mark = pixel_badge(badge, badge_size, bfont)
            image.paste(mark, (int(x + 10 * s), int(y + (line_h - badge_size) / 2)), mark)

    image = image.quantize(colors=256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    os.makedirs(os.path.dirname(os.path.abspath(out_path)), exist_ok=True)
    tmp = out_path + ".tmp"
    image.save(tmp, "PNG", optimize=True)
    with open(tmp, "rb") as f:
        fresh = f.read()
    old = None
    if os.path.isfile(out_path):
        with open(out_path, "rb") as f:
            old = f.read()
    if old != fresh:
        os.replace(tmp, out_path)
    else:
        os.remove(tmp)
    return width // s, height // s


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source")
    parser.add_argument("out")
    parser.add_argument("--title", default="")
    parser.add_argument("--lang", default="csharp")
    parser.add_argument("--hl", default="")
    parser.add_argument("--no-numbers", action="store_true")
    args = parser.parse_args()
    with open(args.source, encoding="utf-8") as f:
        code = f.read()
    w, h = render(code, args.out, args.title, args.lang, parse_lines(args.hl), not args.no_numbers)
    print(f"{args.out}: {w}x{h}")


if __name__ == "__main__":
    main()
