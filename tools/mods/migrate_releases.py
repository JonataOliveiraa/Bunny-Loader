#!/usr/bin/env python3
"""Copia as Releases dos mods do repositório do app para o Bunny-Loader-Mods.

    python tools/mods/migrate_releases.py            # copia e aponta o índice
    python tools/mods/migrate_releases.py --dry-run  # só lista o que faria

Uso único, na separação do catálogo (2026-10). Para cada Release `mod-*` de
JonataOliveiraa/Bunny-Loader: baixa o .bl, cria a mesma Release (tag, nome,
texto) no Bunny-Loader-Mods e sobe o arquivo. Release que já existe lá com o
mesmo arquivo é pulada, então rodar de novo depois de uma falha continua de
onde parou. No fim, o index.json do repositório de mods passa a apontar para
as Releases novas, conferindo o sha256 de cada pacote.

As Releases antigas não são apagadas: os APKs já instalados leem o índice
velho e baixam de lá. Apagar fica para quando todo mundo tiver o app novo.

O token é o mesmo do publish.py (BL_GITHUB_TOKEN ou bunnyloadermodstoken.txt):
Contents: Read and write só no Bunny-Loader-Mods. O repositório do app é
lido sem token, porque as Releases dele são públicas.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
import urllib.parse
import urllib.request

import publish

OLD_API = f"https://api.github.com/repos/{publish.OWNER}/Bunny-Loader"
OLD_DOWNLOAD = f"https://github.com/{publish.OWNER}/Bunny-Loader/releases/download/"
NEW_DOWNLOAD = publish.DOWNLOAD + "/"


def public_get(url: str):
    req = urllib.request.Request(url, headers={
        "Accept": "application/vnd.github+json", "User-Agent": "bunny-loader-migrate"})
    with urllib.request.urlopen(req) as r:
        return json.loads(r.read())


def download(url: str) -> bytes:
    req = urllib.request.Request(url, headers={"User-Agent": "bunny-loader-migrate"})
    with urllib.request.urlopen(req) as r:
        return r.read()


def old_mod_releases() -> list[dict]:
    out, page = [], 1
    while True:
        batch = public_get(f"{OLD_API}/releases?per_page=100&page={page}")
        out += [r for r in batch if r["tag_name"].startswith("mod-")]
        if len(batch) < 100:
            break
        page += 1
    # A mais antiga primeiro: a lista do repositório novo fica na mesma ordem.
    return sorted(out, key=lambda r: r["created_at"])


def copy_release(old: dict, dry: bool) -> dict[str, str]:
    """Devolve {nome do asset: sha256} do que ficou no repositório novo."""
    tag = old["tag_name"]
    assets = [a for a in old.get("assets", []) if a["name"].endswith(".bl")]
    if not assets:
        print(f"  {tag}: sem .bl, pulada")
        return {}
    existing = publish.gh("GET", f"{publish.API}/releases/tags/{urllib.parse.quote(tag)}")
    done: dict[str, str] = {}
    for asset in assets:
        data = download(asset["browser_download_url"])
        sha = hashlib.sha256(data).hexdigest()
        there = next((a for a in (existing or {}).get("assets", []) if a["name"] == asset["name"]), None)
        if there:
            digest = (there.get("digest") or "").removeprefix("sha256:")
            if digest and digest != sha:
                raise publish.Fail(f"{tag}/{asset['name']} já existe no repositório novo com outro arquivo")
            print(f"  {tag}: já copiada")
            done[asset["name"]] = sha
            continue
        if dry:
            print(f"  [dry-run] {tag}: copiaria {asset['name']} ({len(data)} bytes, sha256 {sha[:12]}...)")
            done[asset["name"]] = sha
            continue
        if existing is None:
            existing = publish.gh("POST", f"{publish.API}/releases", {
                "tag_name": tag,
                "target_commitish": publish.BRANCH,
                "name": old.get("name") or tag,
                "body": old.get("body") or "",
                "prerelease": bool(old.get("prerelease")),
                "make_latest": "false",
            })
        q = urllib.parse.urlencode({"name": asset["name"]})
        publish.gh("POST", f"{publish.UPLOADS}/releases/{existing['id']}/assets?{q}",
                   raw=data, content_type="application/zip")
        print(f"  {tag}: copiada ({len(data)} bytes)")
        done[asset["name"]] = sha
    return done


def repoint_index(copied: dict[str, dict[str, str]], dry: bool) -> None:
    wt = publish.worktree()
    index = publish.load_index(wt)
    changed = 0
    for mod in index["mods"]:
        url = mod["download"]["url"]
        if not url.startswith(OLD_DOWNLOAD):
            continue
        tag, _, name = url[len(OLD_DOWNLOAD):].partition("/")
        sha = copied.get(tag, {}).get(urllib.parse.unquote(name))
        if sha is None:
            print(f"  {mod.get('name', mod['uid'])}: {tag} não foi copiada; o link antigo fica")
            continue
        if sha != mod["download"]["sha256"].lower():
            raise publish.Fail(f"{mod.get('name', mod['uid'])}: o .bl de {tag} não confere com o sha256 do índice")
        mod["download"]["url"] = NEW_DOWNLOAD + url[len(OLD_DOWNLOAD):]
        changed += 1
    print(f"  índice: {changed} link(s) apontados para {publish.OWNER}/{publish.REPO}")
    if changed:
        publish.save_index(wt, index)
        publish.commit(wt, "Point the downloads to this repository's Releases", dry)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()
    try:
        releases = old_mod_releases()
        print(f"{len(releases)} Release(s) de mod em {publish.OWNER}/Bunny-Loader")
        copied = {r["tag_name"]: copy_release(r, args.dry_run) for r in releases}
        repoint_index(copied, args.dry_run)
    except publish.Fail as e:
        print(f"erro: {e}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
