#!/usr/bin/env python3
"""Publica um mod no catálogo online do Bunny Loader.

    python tools/mods/publish.py pacote.bl [outro.bl ...]
    python tools/mods/publish.py --dry-run pacote.bl     # só confere e monta, sem rede
    python tools/mods/publish.py --remove <uid>          # tira do catálogo (a Release fica)
    python tools/mods/publish.py --reindex               # põe o manifesto de cada mod no índice

O catálogo mora no próprio repositório, em dois lugares:

    branch mods-index   index.json + mods/<uid>/ (a vitrine: manifest, ícone,
                        capa, description.md, autores...). Branch órfão: não
                        divide histórico com o main nem dispara o workflow da wiki.
    Releases            uma por versão de mod, tag mod-<id>-v<versão>, com o .bl.
                        Nunca marcada como "Latest": essa fica para o APK.

Cada entrada do index.json leva o manifesto inteiro do mod (`manifest`): o app
monta a lista da aba Explorar só com o índice, numa requisição, e baixa ícone,
capa e descrição de cada mod só quando eles aparecem na tela.

O app lê tudo anonimamente (app/.../mods/RemoteCatalog.kt). Só publicar pede
token: um fine-grained token com "Contents: Read and write" neste repositório,
em BL_GITHUB_TOKEN (ou GITHUB_TOKEN). Ele fica no PC; nunca vai para o app.

A ordem é Release primeiro, índice depois: se o push do índice falhar, rodar
de novo reaproveita a Release (o sha256 confere) e só refaz o índice.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request
import zipfile
from pathlib import Path

OWNER = "JonataOliveiraa"
REPO = "Bunny-Loader"
BRANCH = "mods-index"
ROOT = Path(__file__).resolve().parents[2]
WORKTREE = ROOT / "build" / "mods-index"
API = f"https://api.github.com/repos/{OWNER}/{REPO}"
UPLOADS = f"https://uploads.github.com/repos/{OWNER}/{REPO}"
DOWNLOAD = f"https://github.com/{OWNER}/{REPO}/releases/download"
FORMAT = 1
BL_VERSION = 2

UID = re.compile(r"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")
# O mesmo filtro do RemoteCatalog.SAFE_PATH: o que passa aqui o app aceita.
SAFE_PATH = re.compile(r"^[A-Za-z0-9._ -]+(/[A-Za-z0-9._ -]+)*$")
PACKAGE_PATH = re.compile(r"^[A-Za-z0-9._ ()-]+(/[A-Za-z0-9._ ()-]+)*$")
VERSION = re.compile(r"^\d+(\.\d+)*([-+][0-9A-Za-z.]+)?$")
# As capas na ordem do Catalog.BANNERS: o app usa a primeira que existir.
STORE_ROOT_FILES = ["manifest.json", "icon.png", "icon.gif",
                    "banner.gif", "banner.png", "banner.webp", "banner.jpg", "banner.jpeg",
                    "description.md", "changelog.md", "license.md"]
HEX_COLOR = re.compile(r"^#?([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$")
STORE_DIRS = ["authors/", "thumbnails/"]
MD_IMAGE = re.compile(r"!\[[^\]]*\]\(([^)\s]+)")
MAX_STORE_FILE = 4 << 20


class Fail(Exception):
    pass


# ------------------------------------------------------------------ pacote

class Package:
    """Um .bl aberto e conferido com as mesmas regras do import do app."""

    def __init__(self, path: Path):
        self.path = path
        self.data = path.read_bytes()
        self.sha256 = hashlib.sha256(self.data).hexdigest()
        self.size = len(self.data)
        try:
            self.zip = zipfile.ZipFile(path)
        except zipfile.BadZipFile:
            raise Fail(f"{path.name}: não é zip")
        self.names = {n for n in self.zip.namelist() if not n.endswith("/")}
        for n in self.names:
            if not PACKAGE_PATH.fullmatch(n) or any(s in (".", "..") for s in n.split("/")):
                raise Fail(f"{path.name}: caminho que o app recusa: {n!r}")
        if "manifest.json" not in self.names:
            raise Fail(f"{path.name}: sem manifest.json na raiz")
        raw = self.zip.read("manifest.json").decode("utf-8-sig")
        try:
            self.manifest = json.loads(raw)
        except json.JSONDecodeError as e:
            raise Fail(f"{path.name}: manifest.json inválido ({e})")
        m = self.manifest
        self.uid = m.get("uid", "")
        self.id = m.get("id", "")
        self.version = m.get("version", "")
        self.name = m.get("name", self.id)
        if not UID.match(self.uid):
            raise Fail(f"{path.name}: uid ausente ou fora do formato UUID minúsculo")
        if not re.match(r"^[a-z0-9][a-z0-9._-]*$", self.id):
            raise Fail(f"{path.name}: id {self.id!r} precisa ser minúsculo, sem espaço")
        if not VERSION.match(self.version):
            raise Fail(f"{path.name}: versão {self.version!r} não é do tipo 1.2.0")
        if m.get("blVersion", 1) != BL_VERSION:
            raise Fail(f"{path.name}: blVersion {m.get('blVersion', 1)}; o app carrega {BL_VERSION}")
        kind = str(m.get("type", "")).strip().lower()
        if kind in {"texture", "textures", "textura", "texturas", "resourcepack", "resource-pack"}:
            if not any(n.startswith(("content/Images/", "Content/Images/")) and n.lower().endswith(".png")
                       for n in self.names):
                raise Fail(f"{path.name}: pacote de textura sem PNGs em content/Images")
        else:
            entry = f"content/{m.get('entry', 'main.js')}"
            if entry not in self.names:
                raise Fail(f"{path.name}: sem {entry}")
        if "icon.png" not in self.names and "icon.gif" not in self.names:
            other = [n for n in self.names if n.lower() in ("icon.png", "icon.gif")]
            hint = f" (achei {other[0]!r}: o nome é minúsculo)" if other else ""
            print(f"  aviso: {path.name} sem icon.png{hint}; o app usa o ícone da categoria")
        elif "icon.png" not in self.names:
            # O icon.gif é só do launcher: o menu de mods do jogo lê o icon.png.
            print(f"  aviso: {path.name} tem icon.gif mas não icon.png; no jogo o mod fica sem ícone")

    @property
    def tag(self) -> str:
        return f"mod-{self.id}-v{self.version}"

    @property
    def asset(self) -> str:
        return f"{self.id}-{self.version}.bl"

    @property
    def url(self) -> str:
        return f"{DOWNLOAD}/{self.tag}/{self.asset}"

    def store_files(self) -> list[str]:
        """O que a ficha do mod precisa antes de baixar o pacote inteiro."""
        files = [f for f in STORE_ROOT_FILES if f in self.names]
        files += sorted(n for n in self.names if any(n.startswith(d) for d in STORE_DIRS))
        for page in self.manifest.get("pages", []):
            f = page.get("file", "")
            if f in self.names:
                files.append(f)
        # O fundo da ficha (theme.background), quando é imagem e não cor.
        theme = self.manifest.get("theme") or {}
        background = str(theme.get("background", "")).strip() if isinstance(theme, dict) else ""
        if background and not HEX_COLOR.match(background):
            if background in self.names:
                files.append(background)
            else:
                print(f"  aviso: {self.path.name}: theme.background {background!r} não está no pacote")
        # Imagens que o Markdown da ficha usa (o ExampleMod mostra sprites do
        # próprio content/ na descrição).
        for md in [f for f in files if f.endswith(".md")]:
            for ref in MD_IMAGE.findall(self.zip.read(md).decode("utf-8", "replace")):
                ref = urllib.parse.unquote(ref.split("#")[0])
                if ref in self.names:
                    files.append(ref)
        seen, out = set(), []
        for f in files:
            if f not in seen:
                seen.add(f)
                out.append(f)
        for f in out:
            if not SAFE_PATH.fullmatch(f) or any(part in (".", "..") for part in f.split("/")):
                raise Fail(f"{self.path.name}: caminho que a vitrine recusa: {f!r}")
            if self.zip.getinfo(f).file_size > MAX_STORE_FILE:
                raise Fail(f"{self.path.name}: {f} passa de 4 MB, o limite da vitrine")
        return out

    def write_store(self, target: Path) -> list[str]:
        files = self.store_files()
        if target.exists():
            shutil.rmtree(target)
        for f in files:
            out = target / f
            out.parent.mkdir(parents=True, exist_ok=True)
            out.write_bytes(self.zip.read(f))
        return files


# ------------------------------------------------------------------ GitHub

_token: str | None = None
# O gerenciador (manager.py) desliga: lá o token vem da página, e um getpass
# esperando no console travaria o pedido sem ninguém ver.
INTERACTIVE = True


def set_token(value: str | None) -> None:
    global _token
    _token = (value or "").strip() or None


def has_token() -> bool:
    return bool(_token or os.environ.get("BL_GITHUB_TOKEN") or os.environ.get("GITHUB_TOKEN"))


def token() -> str:
    """Da variável de ambiente ou, sem ela, perguntado uma vez. O getpass não
    mostra o que se cola nem deixa o token no histórico do terminal."""
    global _token
    if _token:
        return _token
    t = os.environ.get("BL_GITHUB_TOKEN") or os.environ.get("GITHUB_TOKEN")
    if not t and INTERACTIVE and sys.stdin.isatty():
        import getpass
        t = getpass.getpass("Token do GitHub (cole e Enter; não aparece na tela): ")
    t = (t or "").strip()
    if not t:
        raise Fail("sem token: defina BL_GITHUB_TOKEN ou cole quando o script pedir "
                   f"(um token que escreva em {OWNER}/{REPO}, Contents: Read and write)")
    _token = t
    return t


def gh(method: str, url: str, body=None, raw: bytes | None = None,
       content_type: str = "application/json"):
    data = raw if raw is not None else (json.dumps(body).encode() if body is not None else None)
    req = urllib.request.Request(url, data=data, method=method, headers={
        "Authorization": f"Bearer {token()}",
        "Accept": "application/vnd.github+json",
        "X-GitHub-Api-Version": "2022-11-28",
        "User-Agent": "bunny-loader-publish",
        **({"Content-Type": content_type} if data is not None else {}),
    })
    try:
        with urllib.request.urlopen(req) as r:
            text = r.read()
            return json.loads(text) if text else None
    except urllib.error.HTTPError as e:
        if e.code == 404:
            return None
        raise Fail(f"GitHub {method} {url}: {e.code} {e.read().decode(errors='replace')[:300]}")


def release_for(pkg: Package, dry: bool) -> None:
    """Cria a Release com o .bl, ou confere a que já existe."""
    if dry:
        print(f"  [dry-run] Release {pkg.tag} com {pkg.asset} ({pkg.size} bytes)")
        return
    existing = gh("GET", f"{API}/releases/tags/{pkg.tag}")
    if existing:
        asset = next((a for a in existing.get("assets", []) if a["name"] == pkg.asset), None)
        if asset:
            digest = (asset.get("digest") or "").removeprefix("sha256:")
            if not digest:
                with urllib.request.urlopen(asset["browser_download_url"]) as r:
                    digest = hashlib.sha256(r.read()).hexdigest()
            if digest != pkg.sha256:
                raise Fail(f"{pkg.tag} já foi publicada com outro arquivo. "
                           "Suba a versão no manifest.json em vez de trocar a publicada.")
            print(f"  Release {pkg.tag} já existe com este arquivo: reaproveitada")
            return
        release = existing
    else:
        release = gh("POST", f"{API}/releases", {
            "tag_name": pkg.tag,
            # main e não mods-index: na primeira publicação o branch do
            # catálogo ainda não existe no GitHub (a Release vem antes do push).
            "target_commitish": "main",
            "name": f"{pkg.name} v{pkg.version}",
            "body": f"Mod para o Bunny Loader.\n\n- uid: `{pkg.uid}`\n- sha256: `{pkg.sha256}`",
            "make_latest": "false",
        })
    q = urllib.parse.urlencode({"name": pkg.asset})
    gh("POST", f"{UPLOADS}/releases/{release['id']}/assets?{q}",
       raw=pkg.data, content_type="application/zip")
    print(f"  Release {pkg.tag} publicada")


# ------------------------------------------------------------------ índice

def git(*args: str, cwd: Path = ROOT, check: bool = True) -> subprocess.CompletedProcess:
    return subprocess.run(["git", *args], cwd=cwd, check=check, text=True,
                          capture_output=True, encoding="utf-8")


def worktree() -> Path:
    """build/mods-index: o branch do catálogo, ao lado do código, sem misturar."""
    if not (WORKTREE / ".git").exists():
        WORKTREE.parent.mkdir(parents=True, exist_ok=True)
        git("worktree", "prune")
        git("fetch", "origin", BRANCH, check=False)
        has_remote = git("rev-parse", "--verify", "--quiet", f"origin/{BRANCH}", check=False).returncode == 0
        has_local = git("rev-parse", "--verify", "--quiet", BRANCH, check=False).returncode == 0
        if has_local:
            git("worktree", "add", str(WORKTREE), BRANCH)
        elif has_remote:
            git("worktree", "add", "-b", BRANCH, str(WORKTREE), f"origin/{BRANCH}")
        else:
            git("worktree", "add", "--orphan", "-b", BRANCH, str(WORKTREE))
    discard_pending(WORKTREE)
    git("pull", "--ff-only", "origin", BRANCH, cwd=WORKTREE, check=False)
    return WORKTREE


def discard_pending(wt: Path) -> None:
    """Começa do que está no branch. Um --dry-run deixa a vitrine montada sem
    commit, e sem isto a publicação seguinte levaria junto mods que não estão
    nela (no índice, mas sem Release)."""
    git("rm", "-r", "-q", "--cached", "--ignore-unmatch", ".", cwd=wt)
    git("clean", "-fdxq", cwd=wt)
    if git("rev-parse", "--verify", "--quiet", "HEAD", cwd=wt, check=False).returncode == 0:
        git("checkout", "HEAD", "--", ".", cwd=wt)


def load_index(wt: Path) -> dict:
    f = wt / "index.json"
    if f.exists():
        return json.loads(f.read_text(encoding="utf-8"))
    return {"format": FORMAT, "mods": []}


def save_index(wt: Path, index: dict) -> None:
    index["format"] = FORMAT
    index["mods"].sort(key=lambda m: m.get("name", m["uid"]).lower())
    (wt / "index.json").write_text(json.dumps(index, ensure_ascii=False, indent=2) + "\n",
                                   encoding="utf-8", newline="\n")


def readme(wt: Path) -> None:
    (wt / "README.md").write_text(
        "# Catálogo de mods do Bunny Loader\n\n"
        "Este branch é gerado por `tools/mods/publish.py` (no `main`). Não edite à mão.\n\n"
        "- `index.json`: a lista que o app lê.\n"
        "- `mods/<uid>/`: a vitrine de cada mod (manifesto, ícone, capa, descrição).\n"
        "- Os pacotes `.bl` estão nas Releases `mod-<id>-v<versão>`.\n",
        encoding="utf-8", newline="\n")


def commit(wt: Path, message: str, dry: bool) -> None:
    git("add", "-A", cwd=wt)
    if git("diff", "--cached", "--quiet", cwd=wt, check=False).returncode == 0:
        print("  índice sem mudança")
        return
    if dry:
        print(f"  [dry-run] commit no {BRANCH} montado em {wt} (sem commit nem push)")
        return
    git("commit", "-m", message, cwd=wt)
    r = git("push", "-u", "origin", BRANCH, cwd=wt, check=False)
    if r.returncode != 0:
        raise Fail(f"push do {BRANCH} falhou:\n{r.stderr}")
    print(f"  {BRANCH} enviado")


# ------------------------------------------------------------------ main

def publish(paths: list[Path], dry: bool) -> None:
    packages = []
    for p in paths:
        pkg = Package(p)
        print(f"{pkg.name} v{pkg.version}  ({pkg.uid})")
        print(f"  {pkg.size} bytes, sha256 {pkg.sha256}")
        packages.append(pkg)
    uids = [p.uid for p in packages]
    if len(set(uids)) != len(uids):
        raise Fail("dois pacotes com o mesmo uid na mesma chamada")

    wt = worktree()
    index = load_index(wt)
    for pkg in packages:
        old = next((m for m in index["mods"] if m["uid"] == pkg.uid), None)
        if old and old.get("id") != pkg.id:
            print(f"  aviso: o id mudou de {old.get('id')!r} para {pkg.id!r} (o uid manda)")
        release_for(pkg, dry)
        files = pkg.write_store(wt / "mods" / pkg.uid)
        entry = {
            "uid": pkg.uid,
            "id": pkg.id,
            "name": pkg.name,
            "version": pkg.version,
            "download": {"url": pkg.url, "sha256": pkg.sha256, "size": pkg.size},
            "files": files,
            "manifest": store_manifest(wt, pkg.uid),
        }
        index["mods"] = [m for m in index["mods"] if m["uid"] != pkg.uid] + [entry]
        print(f"  vitrine: {', '.join(files)}")
    readme(wt)
    save_index(wt, index)
    names = ", ".join(f"{p.name} v{p.version}" for p in packages)
    commit(wt, f"Publicar {names}", dry)


def store_manifest(wt: Path, uid: str) -> dict:
    """O manifest.json da vitrine do mod, para ir dentro do índice."""
    f = wt / "mods" / uid / "manifest.json"
    if not f.exists():
        raise Fail(f"{uid}: vitrine sem manifest.json")
    return json.loads(f.read_text(encoding="utf-8-sig"))


def reindex(dry: bool) -> None:
    """Põe o manifesto de cada mod já publicado no índice, sem Release nova."""
    wt = worktree()
    index = load_index(wt)
    for mod in index["mods"]:
        mod["manifest"] = store_manifest(wt, mod["uid"])
        print(f"  {mod.get('name', mod['uid'])}: manifesto no índice")
    save_index(wt, index)
    commit(wt, "Manifesto de cada mod dentro do índice", dry)


def remove(uid: str, dry: bool) -> None:
    wt = worktree()
    index = load_index(wt)
    before = len(index["mods"])
    index["mods"] = [m for m in index["mods"] if m["uid"] != uid]
    if len(index["mods"]) == before:
        raise Fail(f"{uid} não está no catálogo")
    shutil.rmtree(wt / "mods" / uid, ignore_errors=True)
    save_index(wt, index)
    commit(wt, f"Tirar {uid} do catálogo", dry)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("packages", nargs="*", type=Path)
    ap.add_argument("--dry-run", action="store_true", help="confere e monta o índice, sem rede")
    ap.add_argument("--remove", metavar="UID")
    ap.add_argument("--reindex", action="store_true", help="põe o manifesto de cada mod no índice")
    args = ap.parse_args()
    try:
        if args.remove:
            remove(args.remove, args.dry_run)
        elif args.reindex:
            reindex(args.dry_run)
        elif args.packages:
            publish(args.packages, args.dry_run)
        else:
            ap.print_help()
            return 2
    except Fail as e:
        print(f"erro: {e}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
