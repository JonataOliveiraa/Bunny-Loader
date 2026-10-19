"""Porta pacotes de fonte para o catalogo do Bunny Loader.

    python tools/mods/port_fonts.py                 # todos de PORTS
    python tools/mods/port_fonts.py Aclonica        # so os nomeados

Cada port sai em build/mods-repo/ports/fonts/<Nome>/bl (o pacote) com um
README ao lado, e o .bl em build/mods-repo/ports/fonts/in bl/<Nome>/. As
fontes de origem (.ttf, .xnb, licenca, icone) ficam em build/font-sources/
<Nome>/, fora de qualquer Git: algumas licencas nao deixam redistribuir o
arquivo da fonte, so o que se desenha com ela.
"""
from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
import uuid
from pathlib import Path

from PIL import Image, ImageDraw

from pack import pack

ROOT = Path(__file__).resolve().parents[2]
SOURCES = ROOT / "build" / "font-sources"
PORTS_DIR = ROOT / "build" / "mods-repo" / "ports" / "fonts"
WORKSHOP = Path(r"C:\Program Files (x86)\Steam\steamapps\workshop\content\105600")
POTATO = ROOT / "samples" / "ExampleMod" / "authors" / "potato.png"
FONTS_TOOL = Path(__file__).resolve().parent / "fonts.py"

PORTER = {"name": "Potato", "role": "Port para o Bunny Loader", "avatar": "potato.png", "color": "#E8C170",
          "link": "https://github.com/JonataOliveiraa"}

PORTS = [
    {
        "name": "Aclonica", "id": "fonte-aclonica", "version": "1.0.0",
        "source": ("ttf", "Aclonica-Regular.ttf"), "args": [],
        "designer": "Astigmatic", "designer_link": "https://fonts.google.com/specimen/Aclonica",
        "license": "Apache-2.0", "license_file": "Aclonica-LICENSE.txt",
        "summary": "Letras arredondadas e grossas, no estilo de cartaz de aventura",
        "accent": "#FFB347", "panel": "#3A2A1C", "heading": "#FFE8C7",
    },
    {
        "name": "Ringbearer", "id": "fonte-ringbearer", "version": "1.0.0",
        "source": ("ttf", "RINGM___.TTF"),
        # ss, (c), (r), Ê e (tm) sao enfeites do logotipo nesta fonte, nao essas letras.
        "args": ["--exclude", "00A9,00AE,00CA,00DF,2122"],
        "designer": "Pete Klassen", "designer_link": "https://www.dafont.com/ringbearer.font",
        "license": "Gratuita para uso pessoal", "license_file": "readme.html",
        "summary": "A letra do logotipo de O Senhor dos Anéis",
        "notes": "As maiúsculas são enfeitadas e algumas ficam penduradas, como no logotipo do filme. "
                 "ß, ©, ®, ™ e Ê continuam com a letra do jogo (nesta fonte eles são enfeites).",
        "accent": "#E8C170", "panel": "#2B2418", "heading": "#FFF1CF",
    },
    {
        "name": "Pescadero", "id": "fonte-pescadero", "version": "1.0.0",
        "source": ("xnb", WORKSHOP / "2439864365" / "Content" / "Fonts"),
        "args": [],
        "designer": "Microsoft", "designer_link": None,
        "pack_author": {"name": "Mirasein", "role": "Resource pack Custom Fonts (PC)",
                        "link": "https://steamcommunity.com/sharedfiles/filedetails/?id=2439864365"},
        "license": "Fonte Pescadero da Microsoft", "license_file": None,
        "summary": "Serifada clássica, do resource pack Custom Fonts do PC",
        "notes": "Convertida dos .xnb do resource pack do PC, na resolução original: fica igual ao PC, "
                 "menos nítida que as fontes geradas do arquivo .ttf.",
        "accent": "#9FC9E8", "panel": "#1E2A36", "heading": "#E6F3FF",
    },
    {
        "name": "Ale and Wenches", "id": "fonte-ale-and-wenches", "version": "1.0.0",
        "source": ("ttf", "ALEAWB__.TTF"), "args": ["--compose-accents"],
        "designer": "Nate Piekos (Blambot)", "designer_link": "https://blambot.com",
        "license": "Freeware não comercial (Blambot)", "license_file": "font info.txt",
        "summary": "Letra de taverna medieval, desenhada à mão",
        "notes": "A fonte original não tem letras acentuadas: os acentos (á, ç, õ...) foram montados com a "
                 "letra da própria fonte e o acento da Comic Neue.",
        "accent": "#D9A066", "panel": "#33221A", "heading": "#FBE6CF",
    },
]


def uid_for(port_id: str) -> str:
    return str(uuid.uuid3(uuid.NAMESPACE_URL, f"bunny-loader:font-port:{port_id}"))


def generate_fonts(port: dict, fonts_dir: Path) -> None:
    kind, source = port["source"]
    path = source if isinstance(source, Path) else SOURCES / port["name"] / source
    if not path.exists():
        raise SystemExit(f"{port['name']}: falta a origem {path}")
    if fonts_dir.exists():
        shutil.rmtree(fonts_dir)
    command = [sys.executable, "-E", str(FONTS_TOOL), kind, str(path), "--out", str(fonts_dir), *port["args"]]
    subprocess.run(command, check=True)


def render_text(fonts_dir: Path, name: str, text: str, color=(255, 255, 255), shadow=(0, 0, 0)) -> Image.Image:
    """O texto com a fonte gerada (logica 1:1 com o atlas, no tamanho do atlas), com a borda do jogo."""
    meta = json.loads((fonts_dir / f"{name}.json").read_text(encoding="utf-8"))
    scale = meta.get("scale", 1)
    pages = [Image.open(fonts_dir / p).convert("RGBA") for p in meta["pages"]]
    glyphs = {g[0]: g for g in meta["glyphs"]}
    pen, pieces = 0.0, []
    for i, ch in enumerate(text):
        g = glyphs.get(ord(ch))
        if not g:
            pen += meta["lineSpacing"] * 0.3
            continue
        _, page, bx, by, bw, bh, cx, cy, _, _, left, width, right = g
        pen += max(left, 0) if i == 0 else left
        if bw and bh:
            tile = pages[page].crop((bx * scale, by * scale, (bx + bw) * scale, (by + bh) * scale))
            pieces.append((tile, round((pen + cx) * scale), cy * scale))
        pen += width + right
    spread = 2 * scale
    image = Image.new("RGBA", (round(pen * scale) + 2 * spread, meta["lineSpacing"] * scale + 2 * spread))
    for dx, dy, fill in [(-spread, 0, shadow), (spread, 0, shadow), (0, -spread, shadow), (0, spread, shadow), (0, 0, color)]:
        for tile, x, y in pieces:
            solid = Image.new("RGBA", tile.size, fill + (255,))
            solid.putalpha(tile.getchannel("A"))
            image.alpha_composite(solid, (x + spread + dx, y + spread + dy))
    return image.crop(image.getbbox())


def hex_rgb(value: str):
    return tuple(int(value[i:i + 2], 16) for i in (1, 3, 5))


def banner(port: dict, fonts_dir: Path, target: Path) -> None:
    panel, accent, heading = hex_rgb(port["panel"]), hex_rgb(port["accent"]), hex_rgb(port["heading"])
    image = Image.new("RGBA", (768, 224), panel + (255,))
    draw = ImageDraw.Draw(image)
    draw.rectangle((6, 6, 761, 217), outline=accent + (255,), width=4)
    title = render_text(fonts_dir, "Death_Text", port["name"], color=heading)
    fit = min(680 / title.width, 120 / title.height)
    title = title.resize((round(title.width * fit), round(title.height * fit)), Image.LANCZOS)
    image.alpha_composite(title, ((768 - title.width) // 2, 92 - title.height // 2))
    line = render_text(fonts_dir, "Mouse_Text", "Espada Curta de Cobre   1234", color=accent)
    fit = min(680 / line.width, 44 / line.height)
    line = line.resize((round(line.width * fit), round(line.height * fit)), Image.LANCZOS)
    image.alpha_composite(line, ((768 - line.width) // 2, 176 - line.height // 2))
    image.resize((384, 112), Image.LANCZOS).save(target)


def generated_icon(port: dict, fonts_dir: Path, target: Path) -> None:
    """No estilo dos icones dos pacotes do TL Pro: o nome na propria fonte, num quadro escuro."""
    template = SOURCES / "Ringbearer" / "icon.png"
    base = Image.open(template).convert("RGBA")
    pixels = base.load()
    top, bottom = 95, 175   # faixa do texto no modelo: vira o degrade de cima e de baixo
    for x in range(40, base.width - 40):
        a, b = pixels[x, top], pixels[x, bottom]
        for y in range(top, bottom):
            t = (y - top) / (bottom - top)
            pixels[x, y] = tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(4))
    words = port["name"].split(" ")
    lines = [" ".join(words[:len(words) // 2 + 1]), " ".join(words[len(words) // 2 + 1:])] if len(words) > 2 else [port["name"]]
    rendered = [render_text(fonts_dir, "Death_Text", text) for text in lines if text]
    height = sum(r.height for r in rendered)
    fit = min(1.0, 170 / max(r.width for r in rendered), 120 / height)
    y = (base.height - height * fit) / 2
    for r in rendered:
        r = r.resize((round(r.width * fit), round(r.height * fit)), Image.LANCZOS)
        base.alpha_composite(r, ((base.width - r.width) // 2, round(y)))
        y += r.height
    base.save(target)


def description(port: dict) -> str:
    text = (
        f"Troca **todas as fontes do jogo** pela {port['name']}: dicas dos itens, menus, quantidade no "
        "inventário, números de dano (normal e crítico) e a mensagem de morte.\n\n"
    )
    if port["source"][0] == "ttf":
        text += ("As letras foram desenhadas do arquivo original da fonte com **3 vezes mais pixels** que as do "
                 "jogo (2 vezes na mensagem de morte), então ficam nítidas no celular.\n\n")
    if port.get("notes"):
        text += port["notes"] + "\n\n"
    text += ("Cobre o latim completo com acentos. Um caractere que a fonte não tem continua com a letra original "
             "do jogo.\n\n"
             "> [!NOTE]\n"
             "> É um pacote de fonte: não tem código. Para voltar à letra do jogo, desligue na aba Pacotes e abra "
             "o jogo de novo.\n")
    return text


def license_text(port: dict) -> str:
    link = f" ({port['designer_link']})" if port.get("designer_link") else ""
    text = f"### {port['license']}\n\nA fonte {port['name']} é de {port['designer']}{link}. "
    text += "Este pacote traz só as letras desenhadas com ela, não o arquivo da fonte.\n\n"
    file = port.get("license_file")
    if file:
        data = (SOURCES / port["name"] / file).read_bytes()
        try:
            raw = data.decode("utf-8")
        except UnicodeDecodeError:
            raw = data.decode("cp1252")
        if file.endswith(".html"):
            import re
            raw = re.sub(r"<style.*?</style>|<!--.*?-->", "", raw, flags=re.S)
            import html
            raw = html.unescape(re.sub(r"<[^>]+>", " ", raw))
            raw = "\n".join(" ".join(line.split()) for line in raw.splitlines() if line.strip())
        text += "Texto do autor:\n\n```text\n" + raw.strip() + "\n```\n"
    else:
        text += ("A fonte Pescadero pertence à Microsoft. As letras vêm do resource pack Custom Fonts (Pescadero), "
                 "de Mirasein, publicado na Steam Workshop do Terraria.\n")
    return text


def port_one(port: dict) -> Path:
    name = port["name"]
    folder = PORTS_DIR / name / "bl"
    folder.mkdir(parents=True, exist_ok=True)
    fonts_dir = folder / "content" / "Fonts"
    generate_fonts(port, fonts_dir)

    (folder / "authors").mkdir(exist_ok=True)
    shutil.copy2(POTATO, folder / "authors" / "potato.png")
    icon = SOURCES / name / "icon.png"
    if icon.is_file():
        shutil.copy2(icon, folder / "icon.png")
    else:
        generated_icon(port, fonts_dir, folder / "icon.png")
    banner(port, fonts_dir, folder / "banner.png")

    manifest_file = folder / "manifest.json"
    uid = json.loads(manifest_file.read_text(encoding="utf-8"))["uid"] if manifest_file.is_file() else uid_for(port["id"])
    manifest = {
        "uid": uid, "id": port["id"], "name": f"Fonte {name}", "version": port["version"],
        "author": port["designer"],
        "authors": [
            {"name": port["designer"], "role": "Fonte original",
             **({"link": port["designer_link"]} if port.get("designer_link") else {})},
            *([port["pack_author"]] if port.get("pack_author") else []),
            PORTER,
        ],
        "type": "font", "category": "Fontes",
        "summary": port["summary"], "license": port["license"],
        "blVersion": 2, "gameVersion": [301720, 301720],
        "links": [{"title": "Fonte original", "url": port["designer_link"]}] if port.get("designer_link") else
                 [{"title": "Original no Steam Workshop", "url": port["pack_author"]["link"]}],
        "theme": {"accent": port["accent"], "panel": port["panel"], "heading": port["heading"]},
    }
    manifest_file.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (folder / "description.md").write_text(description(port), encoding="utf-8")
    (folder / "license.md").write_text(license_text(port), encoding="utf-8")
    (folder / "changelog.md").write_text(f"## {port['version']}\n\n- Primeira versão.\n", encoding="utf-8")

    kind = "arquivo .ttf original, 3x (2x na mensagem de morte)" if port["source"][0] == "ttf" else ".xnb do PC, 1x"
    (folder.parent / "README.md").write_text(
        f"# Fonte {name}\n\nFonte de {port['designer']}"
        + (f": {port['designer_link']}" if port.get("designer_link") else "")
        + (f". Resource pack do PC de {port['pack_author']['name']}: {port['pack_author']['link']}"
           if port.get("pack_author") else "") + "\n\n"
        f"Licença: {port['license']} (texto em bl/license.md).\n\n"
        f"Gerada por tools/mods/port_fonts.py a partir do {kind}. A origem fica fora do Git, em "
        f"build/font-sources/{name}/.\n" + (f"\n{port['notes']}\n" if port.get("notes") else ""),
        encoding="utf-8")

    built = pack(folder)
    finished = PORTS_DIR / "in bl" / name
    if finished.exists():
        shutil.rmtree(finished)
    finished.mkdir(parents=True)
    shutil.copy2(built, finished / built.name)
    print(f"{name}: {finished / built.name}")
    return folder


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("names", nargs="*", help="ports to build (default: all)")
    args = parser.parse_args()
    known = {p["name"].lower(): p for p in PORTS}
    for name in args.names or [p["name"] for p in PORTS]:
        if name.lower() not in known:
            parser.error(f"port desconhecido: {name} (conheço {', '.join(p['name'] for p in PORTS)})")
        port_one(known[name.lower()])


if __name__ == "__main__":
    main()
