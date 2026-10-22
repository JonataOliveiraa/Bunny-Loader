"""Soma os downloads de cada mod e grava o stats.json do catálogo.

O GitHub conta cada download de asset de Release (`download_count`). Este
script soma, para cada mod do index.json, o `.bl` de todas as Releases dele
(`mod-<id>-v<versão>`, versões antigas inclusive) e grava:

    stats.json  {"format": 1, "updatedAt": "...", "downloads": {"<uid>": 123}}

O app lê esse arquivo para ordenar Populares e mostrar o número nos cartões.
Roda no repositório de mods pela Action download-stats (a cada 6 h), que
o publish.py mantém em `.github/`; à mão:

    python tools/mods/download_stats.py build/mods-repo

Só stdlib: a Action não instala nada. Com GH_TOKEN (a Action passa o dela) a
API dá 1000 pedidos por hora; sem, 60 — sobra para ~6000 Releases.
"""

from __future__ import annotations

import json
import os
import sys
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

REPO = os.environ.get("GITHUB_REPOSITORY", "JonataOliveiraa/Bunny-Loader-Mods")
FORMAT = 1


def tag_id(tag: str) -> str | None:
    """`mod-gravity-dont-flip-screen-v1.0.0` -> `gravity-dont-flip-screen`."""
    if not tag.startswith("mod-") or "-v" not in tag:
        return None
    return tag[4:].rsplit("-v", 1)[0] or None


def release_tag(url: str) -> str | None:
    """A tag da Release no link de download do índice."""
    marker = "/releases/download/"
    if marker not in url:
        return None
    return url.split(marker, 1)[1].split("/", 1)[0]


def count(index: dict, releases: list[dict]) -> dict[str, int]:
    """Downloads de cada uid do índice. Mod sem download fica com 0."""
    by_id: dict[str, str] = {}
    for mod in index.get("mods", []):
        uid = mod["uid"]
        if mod.get("id"):
            by_id[mod["id"]] = uid
        tid = tag_id(release_tag(mod.get("download", {}).get("url", "")) or "")
        if tid:
            by_id[tid] = uid
    counts = {mod["uid"]: 0 for mod in index.get("mods", [])}
    for release in releases:
        uid = by_id.get(tag_id(release.get("tag_name", "")) or "")
        if uid is None:
            continue
        counts[uid] += sum(int(asset.get("download_count", 0)) for asset in release.get("assets", [])
                           if asset.get("name", "").endswith(".bl"))
    return dict(sorted(counts.items()))


def fetch_releases() -> list[dict]:
    headers = {"Accept": "application/vnd.github+json", "User-Agent": "BunnyLoader-stats"}
    token = os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN")
    if token:
        headers["Authorization"] = f"Bearer {token}"
    out: list[dict] = []
    page = 1
    while True:
        url = f"https://api.github.com/repos/{REPO}/releases?per_page=100&page={page}"
        with urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=30) as r:
            batch = json.load(r)
        if not batch:
            return out
        out += batch
        page += 1


def write(root: Path, downloads: dict[str, int]) -> bool:
    """Grava stats.json se os números mudaram. A data sozinha não vale um
    commit: sem download novo, o arquivo fica como está."""
    target = root / "stats.json"
    try:
        old = json.loads(target.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        old = {}
    if old.get("downloads") == downloads:
        return False
    stats = {
        "format": FORMAT,
        "updatedAt": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "downloads": downloads,
    }
    target.write_text(json.dumps(stats, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    return True


def main() -> int:
    root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(".")
    index = json.loads((root / "index.json").read_text(encoding="utf-8"))
    downloads = count(index, fetch_releases())
    changed = write(root, downloads)
    print(f"{sum(downloads.values())} downloads em {len(downloads)} mods"
          + ("" if changed else " (sem mudança)"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
