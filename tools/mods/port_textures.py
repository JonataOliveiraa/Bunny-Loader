from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import uuid
from pathlib import Path

from pack import pack

ROOT = Path(__file__).resolve().parents[2]
WORKSHOP = Path(r"C:\Program Files (x86)\Steam\steamapps\workshop\content\105600")
STEAM_ICON = Path(__file__).resolve().parent / "assets" / "steam.png"
CREDIT_NAMES = {"2439853873": ["Stryke", "Sylvium", "TiredGhostDude"]}


def plain(text: str) -> str:
    return re.sub(r"\[c/[^:]+:([^\]]*)\]", r"\1", text).strip()


def port(source: Path) -> Path:
    metadata = json.loads((source / "pack.json").read_text(encoding="utf-8-sig"), strict=False)
    images = source / "Content" / "Images"
    files = sorted(p for p in images.rglob("*") if p.is_file() and p.suffix.lower() == ".png")
    if not files:
        raise ValueError(f"{source.name}: resource pack sem imagens; fonte/traducao nao e textura")
    name = plain(metadata["Name"])
    slug = re.sub(r"[^a-z0-9]+", "-", name.lower()).strip("-") or f"workshop-{source.name}"
    digest = hashlib.md5(f"steam-workshop:{source.name}".encode()).digest()
    uid = str(uuid.UUID(bytes=digest, version=3))
    folder_name = re.sub(r'[<>:"/\\|?*\x00-\x1f]', "_", name).rstrip(" .")
    if folder_name in {"", ".", ".."}:
        folder_name = f"workshop-{source.name}"
    folder = ROOT / "ports" / "textures" / folder_name / "bl"
    folder.mkdir(parents=True, exist_ok=True)
    manifest_file = folder / "manifest.json"
    if manifest_file.exists():
        existing = json.loads(manifest_file.read_text(encoding="utf-8-sig"))
        if not any(f"id={source.name}" in link.get("url", "") for link in existing.get("links", [])):
            raise ValueError(f"destino pertence a outro pacote: {folder}")
        uid = existing["uid"]
        slug = existing["id"]
    version = metadata.get("Version", {})
    major, minor = int(version.get("major", 1)), int(version.get("minor", 0))
    if major < 0 or minor < 0:
        raise ValueError(f"versao invalida: {source}")
    other = [p.name for p in (source / "Content").iterdir() if p.is_dir() and p.name != "Images"]
    description = (
        f"Port de {len(files)} imagens do resource pack {name}, de {plain(metadata.get('Author', ''))}. "
        "Substitui os assets de textura encontrados pelo nome no jogo mobile. "
        "Elementos exclusivos do PC podem ficar sem efeito e imagens com medidas diferentes podem exigir ajustes."
    )
    if other:
        description += f" Conteudo adicional do original nao aplicado por este port: {', '.join(other)}."
    if metadata.get("Description"):
        description += "\n\nDescricao do pacote original:\n" + metadata["Description"]
    workshop_url = f"https://steamcommunity.com/sharedfiles/filedetails/?id={source.name}"
    names = CREDIT_NAMES.get(source.name, [plain(metadata.get("Author", ""))])
    authors = [
        {"name": creator, "role": "Arte original", "avatar": "steam.png", "link": workshop_url}
        for creator in names if creator
    ]
    manifest = {
        "uid": uid, "id": slug, "name": name, "version": f"{major}.{minor}.0",
        "author": plain(metadata.get("Author", "")), "type": "texture", "category": "Texturas",
        "authors": authors,
        "description": description, "blVersion": 2,
        "links": [{"title": "Original no Steam Workshop", "url": workshop_url}],
    }
    (folder / "authors").mkdir(exist_ok=True)
    shutil.copy2(STEAM_ICON, folder / "authors" / "steam.png")
    manifest_file.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    for image in files:
        destination = folder / "content" / "Images" / image.relative_to(images)
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(image, destination)
    for filename in ("icon.png", "workshop.json", "pack.json"):
        if (source / filename).is_file():
            shutil.copy2(source / filename, folder / filename)
    if (source / "banner.png").is_file():
        shutil.copy2(source / "banner.png", folder / "banner.png")
    elif not (folder / "banner.png").is_file() and (folder / "icon.png").is_file():
        shutil.copy2(folder / "icon.png", folder / "banner.png")
    readme = (
        f"# {name}\n\n"
        f"Imagens de {', '.join(names)}. Original: "
        f"{workshop_url}\n\n"
        f"Port local de {len(files)} PNGs, sem alterar as imagens e sem main.js. "
        "O loader procura o caminho completo no jogo mobile; assets exclusivos do PC e alternativas em Misc ficam sem efeito. "
        "Avisos de dimensoes indicam possiveis diferencas de versao ou de enquadramento.\n\n"
    )
    if other:
        readme += f"Conteudo adicional do original, fora do port: {', '.join(other)}.\n\n"
    (folder.parent / "README.md").write_text(readme, encoding="utf-8")
    result = pack(folder)
    print(f"{source.name}: {name}: {len(files)} PNGs -> {result.relative_to(ROOT)}")
    return folder


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("ids", nargs="+")
    parser.add_argument("--workshop", type=Path, default=WORKSHOP)
    args = parser.parse_args()
    for workshop_id in args.ids:
        if not workshop_id.isdigit():
            parser.error(f"ID invalido: {workshop_id}")
        port(args.workshop / workshop_id)


if __name__ == "__main__":
    main()
