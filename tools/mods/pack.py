#!/usr/bin/env python3
"""Empacota uma pasta de mod ou textura num .bl para importar ou publicar.

    python tools/mods/pack.py samples/VidaCheia [samples/SemQueda ...]
    python tools/mods/pack.py "ports/textures/Un-mechanical Bosses/bl"

Texturas: manifest.json com type="texture" e content/Images/*.png.
Nao precisam de main.js. O launcher tambem importa ZIPs de resource packs
do PC (pack.json + Content/Images), convertendo o manifesto na importacao.

Sai em build/mods-packs/<id>-<versão>.bl. É o zip que o "Importar pacote" do
app aceita: a raiz do zip é a raiz do mod (manifest.json, content/...).
A ordem dos arquivos e a data de cada entrada são fixas, então a mesma pasta
gera sempre o mesmo .bl, com o mesmo sha256. Sem isso, empacotar de novo uma
versão já publicada daria outro hash, e o publish.py recusaria.
"""
from __future__ import annotations

import io
import json
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "build" / "mods-packs"
SKIP = {".DS_Store", "Thumbs.db", "desktop.ini"}
FIXED_TIME = (2020, 1, 1, 0, 0, 0)


def pack_bytes(folder: Path) -> bytes:
    """O .bl em memória: o gerenciador compara o sha256 sem gravar arquivo."""
    manifest = json.loads((folder / "manifest.json").read_text(encoding="utf-8-sig"))
    if str(manifest.get("type", "")).strip().lower() in {
        "texture", "textures", "textura", "texturas", "resourcepack", "resource-pack"
    }:
        roots = [folder / "content" / "Images", folder / "Content" / "Images"]
        if not any(p.is_file() and p.suffix.lower() == ".png" for root in roots for p in root.rglob("*")):
            raise ValueError("pacote de textura sem PNGs em content/Images")
    files = sorted(p for p in folder.rglob("*") if p.is_file() and p.name not in SKIP)
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, "w", zipfile.ZIP_DEFLATED) as z:
        for f in files:
            info = zipfile.ZipInfo(f.relative_to(folder).as_posix(), FIXED_TIME)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o644 << 16
            z.writestr(info, f.read_bytes())
    return buffer.getvalue()


def pack(folder: Path) -> Path:
    manifest = json.loads((folder / "manifest.json").read_text(encoding="utf-8-sig"))
    out = OUT / f"{manifest['id']}-{manifest['version']}.bl"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(pack_bytes(folder))
    return out


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    for arg in sys.argv[1:]:
        out = pack(Path(arg))
        print(f"{out.relative_to(ROOT)}  ({out.stat().st_size} bytes)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
