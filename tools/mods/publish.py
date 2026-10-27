#!/usr/bin/env python3
"""Publica um mod no catálogo online do Bunny Loader.

    python tools/mods/publish.py pacote.bl [outro.bl ...]
    python tools/mods/publish.py --dry-run pacote.bl     # só confere e monta, sem rede
    python tools/mods/publish.py --remove <uid>          # tira do catálogo (a Release fica)
    python tools/mods/publish.py --reindex               # põe o manifesto de cada mod no índice
    python tools/mods/publish.py --feature consolaria    # "Em destaque" do Início (uid ou id; vários = carrossel)
    python tools/mods/publish.py --feature               # sem nenhum: volta ao destaque do mod.json

O catálogo mora no repositório JonataOliveiraa/Bunny-Loader-Mods, separado do
código do app (um pedido de remoção contra um mod não derruba o app):

    main                index.json + mods/<uid>/ (a vitrine: manifest, ícone,
                        capa, description.md, autores...) e ports/ (a origem
                        dos pacotes de textura e de fonte portados).
    Releases            uma por versão de mod, tag mod-<id>-v<versão>, com o .bl.
    stats.json          downloads de cada mod, da Action download-stats (a
                        cada 6 h; .github/, que este script mantém em dia).

Cada entrada do index.json leva o manifesto inteiro do mod (`manifest`): o app
monta a lista da aba Explorar só com o índice, numa requisição, e baixa ícone,
capa e descrição de cada mod só quando eles aparecem na tela.

O app lê tudo anonimamente (app/.../mods/RemoteCatalog.kt). Só publicar pede
token: um fine-grained token com "Contents: Read and write" só no Bunny-Loader-Mods,
em BL_GITHUB_TOKEN (ou GITHUB_TOKEN) ou no arquivo bunnyloadermodstoken.txt da
raiz (fora do Git). Ele fica no PC; nunca vai para o app.

A ordem é Release primeiro, índice depois: se o push do índice falhar, rodar
de novo reaproveita a Release (o sha256 confere) e só refaz o índice.
"""
from __future__ import annotations

import argparse
from contextlib import ExitStack
from datetime import datetime, timezone
import hashlib
import io
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
import zlib
from pathlib import Path

import csharp_build
import discord_updates

OWNER = "JonataOliveiraa"
REPO = "Bunny-Loader-Mods"
NOREPLY_EMAIL = "122224076+JonataOliveiraa@users.noreply.github.com"
BRANCH = "main"
ROOT = Path(__file__).resolve().parents[2]
WORKTREE = ROOT / "build" / "mods-repo"
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
LOCALIZED_PAGE = re.compile(r"^(?:description|changelog)_[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*\.md$")
HEX_COLOR = re.compile(r"^#?([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$")
STORE_DIRS = ["authors/", "thumbnails/"]
MD_IMAGE = re.compile(r"!\[[^\]]*\]\(([^)\s]+)")
MAX_STORE_FILE = 4 << 20


class Fail(Exception):
    pass


def _opens_as_zip(data: bytes) -> bool:
    try:
        with zipfile.ZipFile(io.BytesIO(data)) as z:
            return z.testzip() is None
    except (zipfile.BadZipFile, ValueError, OSError, EOFError, zlib.error):
        return False


def not_zip_reason(data: bytes) -> str:
    """Por que estes bytes não abrem como zip, do jeito que quem publica entende.

    O .bl costuma chegar estragado no caminho (mandado por app de mensagem,
    copiado como texto, baixado pela página e não pelo arquivo), e "não é zip"
    sozinho não diz o que fazer.
    """
    if not data:
        return "o arquivo está vazio (0 bytes); exporte de novo"
    head = data[:512].lstrip().lower()
    if head.startswith((b"<!doctype", b"<html")) or (head.startswith(b"<") and b"<html" in head):
        return "é uma página da web, não o pacote: o link baixou a página. Baixe o arquivo em si"
    if not data.startswith(b"PK"):
        return f"não começa como zip (os primeiros bytes são {data[:4].hex(' ')}, um .bl começa com 50 4b)"
    if b"\r\n" in data and _opens_as_zip(data.replace(b"\r\n", b"\n")):
        return ("o arquivo foi tratado como texto no caminho (cada \\n virou \\r\\n). "
                "Mande-o como arquivo, ou renomeie para .zip antes de enviar")
    if b"PK\x05\x06" not in data[-65_557:]:
        return f"o zip está cortado: o fim dele não chegou ({len(data)} bytes). Copie ou baixe de novo"
    return "o zip está corrompido; exporte de novo pelo Bunny Loader"


# ------------------------------------------------------------------ pacote

class Package:
    """Um .bl aberto e conferido com as mesmas regras do import do app."""

    def __init__(self, path: Path):
        self.path = path
        self.data = path.read_bytes()
        self.sha256 = hashlib.sha256(self.data).hexdigest()
        self.size = len(self.data)
        # Abrir não basta: estragado no caminho, o zip às vezes abre e só falha
        # ao ler uma entrada, e o pacote quebrado iria para a Release.
        try:
            self.zip = zipfile.ZipFile(path)
            bad = self.zip.testzip()
            if bad is not None:
                raise zipfile.BadZipFile(f"CRC errado em {bad}")
        except (zipfile.BadZipFile, zlib.error, EOFError, ValueError, OSError):
            raise Fail(f"{path.name}: não é zip: {not_zip_reason(self.data)}")
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
        elif kind in {"font", "fonts", "fonte", "fontes"}:
            if not any(re.match(r"^[cC]ontent/Fonts/[^/]+\.json$", n) for n in self.names):
                raise Fail(f"{path.name}: pacote de fonte sem .json em content/Fonts")
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
        # A ficha baixa a descricao Markdown da cultura selecionada.
        files += sorted(n for n in self.names if LOCALIZED_PAGE.fullmatch(n))
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


# Os arquivos de token na raiz, fora do Git: o do catálogo primeiro, o antigo de reserva.
TOKEN_FILES = ("bunnyloadermodstoken.txt", "_token.txt")


def file_token() -> str:
    for name in TOKEN_FILES:
        f = ROOT / name
        if f.is_file() and f.read_text(encoding="utf-8").strip():
            return f.read_text(encoding="utf-8").strip()
    return ""


def has_token() -> bool:
    return bool(_token or os.environ.get("BL_GITHUB_TOKEN") or os.environ.get("GITHUB_TOKEN")
                or file_token())


def token() -> str:
    """Da variável de ambiente, de bunnyloadermodstoken.txt ou, sem os dois,
    perguntado uma vez. O getpass não mostra o que se cola nem deixa o token no
    histórico do terminal."""
    global _token
    if _token:
        return _token
    t = os.environ.get("BL_GITHUB_TOKEN") or os.environ.get("GITHUB_TOKEN") or file_token()
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
            # O main do repositório de mods: o índice e a vitrine.
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
    """build/mods-repo: um clone do repositório de mods, ao lado do código."""
    if not (WORKTREE / ".git").exists():
        WORKTREE.parent.mkdir(parents=True, exist_ok=True)
        # Os nomes dos ports passam do limite de 260 caracteres do Windows.
        r = git("-c", "core.longpaths=true", "clone", "--branch", BRANCH,
                f"https://github.com/{OWNER}/{REPO}.git", str(WORKTREE), check=False)
        if r.returncode != 0:
            raise Fail(f"clone de {OWNER}/{REPO} falhou:\n{r.stderr}")
        git("config", "core.longpaths", "true", cwd=WORKTREE)
    # O repositório é público: o e-mail do commit é o noreply, nunca o do
    # .gitconfig global da máquina.
    git("config", "user.name", OWNER, cwd=WORKTREE)
    git("config", "user.email", NOREPLY_EMAIL, cwd=WORKTREE)
    discard_pending(WORKTREE)
    r = git("pull", "--ff-only", "origin", BRANCH, cwd=WORKTREE, check=False)
    if r.returncode != 0:
        raise Fail(f"não foi possível sincronizar o catálogo em {WORKTREE}; "
                   "a operação parou antes de alterar as Releases ou o índice. "
                   "Confira a conexão e, se os históricos divergiram, integre os "
                   f"commits locais com origin/{BRANCH} antes de tentar de novo:\n{r.stderr}")
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
    # O mesmo texto do Publisher.README do app de catálogo: os dois publicam.
    (wt / "README.md").write_text(
        "# Bunny Loader mod catalog\n"
        "\n"
        "The online catalog of [Bunny Loader](https://github.com/JonataOliveiraa/Bunny-Loader), generated by `tools/mods/publish.py` and by the catalog app. Do not edit `index.json` or `mods/` by hand.\n"
        "\n"
        "- `index.json`: the list the app reads.\n"
        "- `mods/<uid>/`: each mod's page (manifest, icon, banner, description, authors).\n"
        "- Releases `mod-<id>-v<version>`: the `.bl` packages.\n"
        "- `ports/`: the sources of the ported texture packs.\n"
        "\n"
        "Each mod and texture pack belongs to its authors and keeps its own license (`license.md` and `authors/` inside the package). Bunny Loader's license does not apply to them.\n"
        "\n"
        "If you made something listed here and want it removed or credited differently, open an issue.\n",
        encoding="utf-8", newline="\n")


def github_files(wt: Path) -> None:
    """A Action dos downloads e o script dela, no .github/ do repositório de
    mods. A cópia de verdade fica aqui (tools/mods/); lá é só o que roda."""
    here = Path(__file__).resolve().parent
    (wt / ".github" / "workflows").mkdir(parents=True, exist_ok=True)
    for source, target in (("download_stats.py", ".github/download_stats.py"),
                           ("download-stats.yml", ".github/workflows/download-stats.yml")):
        text = (here / source).read_text(encoding="utf-8").replace("\r\n", "\n")
        (wt / target).write_text(text, encoding="utf-8", newline="\n")


def commit(wt: Path, message: str, dry: bool) -> None:
    git("add", "-A", cwd=wt)
    changed = git("diff", "--cached", "--quiet", cwd=wt, check=False).returncode != 0
    if not changed:
        print("  índice sem mudança")
    if dry:
        print(f"  [dry-run] commit no {BRANCH} montado em {wt} (sem commit nem push)")
        return
    if changed:
        git("commit", "-m", message, cwd=wt)
    # O push avança origin/main também: guarde antes a base para distinguir
    # uma publicação nova de uma atualização, inclusive num push repetido.
    notification_base = git("rev-parse", "--verify", f"origin/{BRANCH}", cwd=wt, check=False).stdout.strip()
    # Uma tentativa anterior pode ter feito o commit e falhado só no push.
    r = git("push", "-u", "origin", BRANCH, cwd=wt, check=False)
    if r.returncode != 0:
        # A Action dos downloads pode ter gravado o stats.json no meio: os
        # arquivos são outros, então o rebase passa limpo.
        if git("pull", "--rebase", "origin", BRANCH, cwd=wt, check=False).returncode == 0:
            r = git("push", "-u", "origin", BRANCH, cwd=wt, check=False)
        else:
            git("rebase", "--abort", cwd=wt, check=False)
    if r.returncode != 0:
        raise Fail(f"push do {BRANCH} falhou:\n{r.stderr}")
    print(f"  {BRANCH} enviado")
    notify_catalog(wt, notification_base)


def notify_catalog(wt: Path, previous_commit: str) -> None:
    try:
        if not discord_updates.webhook_url():
            return
        current = git("rev-parse", "HEAD", cwd=wt).stdout.strip()
        if not previous_commit or current == previous_commit:
            discord_updates.flush()
            return

        def snapshot(revision: str) -> dict:
            result = git("show", f"{revision}:index.json", cwd=wt, check=False)
            index = json.loads(result.stdout) if result.returncode == 0 else {"mods": []}
            for entry in index.get("mods", []):
                if "manifest" not in entry:
                    manifest = git("show", f"{revision}:mods/{entry['uid']}/manifest.json", cwd=wt, check=False)
                    if manifest.returncode == 0:
                        entry["manifest"] = json.loads(manifest.stdout.lstrip("\ufeff"))
            return index

        before, after = snapshot(previous_commit), snapshot(current)
        paths = git("diff", "--name-only", "-z", previous_commit, current, "--", "mods/", cwd=wt).stdout
        discord_updates.notify(discord_updates.catalog_events(
            before, after, previous_commit, current, [p for p in paths.split("\0") if p]))
    except (discord_updates.NotificationError, OSError, ValueError, KeyError, TypeError, subprocess.CalledProcessError):
        print("  Discord notifications could not be prepared. The catalog operation succeeded.")


# ------------------------------------------------------------------ main

def publish(paths: list[Path], dry: bool) -> None:
    with ExitStack() as opened:
        packages = []
        for p in paths:
            # Mod C# com fontes: o .bl ganha o bin/ compilado antes de tudo
            # (o sha256 e a Release são do pacote compilado).
            try:
                csharp_build.prepare(p)
            except csharp_build.BuildError as e:
                raise Fail(f"{p.name}: {e}") from None
            pkg = Package(p)
            opened.enter_context(pkg.zip)
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
            published_at = first_published(old)
            if published_at:
                entry["publishedAt"] = published_at
            index["mods"] = [m for m in index["mods"] if m["uid"] != pkg.uid] + [entry]
            print(f"  vitrine: {', '.join(files)}")
        readme(wt)
        github_files(wt)
        save_index(wt, index)
        names = ", ".join(f"{p.name} v{p.version}" for p in packages)
        commit(wt, f"Publicar {names}", dry)


def first_published(old: dict | None) -> str | None:
    """Quando o mod entrou no catálogo (UTC, ISO 8601): o app marca como novo
    por alguns dias. Uma versão nova mantém a data; mod publicado antes deste
    campo fica sem ela, e o app não o trata como novo."""
    if old is None:
        return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    return old.get("publishedAt")


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


def feature(keys: list[str], dry: bool) -> None:
    """O "Em destaque" do Início: a lista `featured` do índice, na ordem dada
    (mais de um vira carrossel no app). Aceita uid ou id. Vazia, sai do índice
    e o app volta ao `featured` do mod.json dos embutidos."""
    wt = worktree()
    index = load_index(wt)
    uids = []
    for key in keys:
        found = next((m for m in index["mods"] if key in (m["uid"], m.get("id"))), None)
        if found is None:
            raise Fail(f"{key} não está no catálogo")
        if found["uid"] not in uids:
            uids.append(found["uid"])
    names = [next(m.get("name", u) for m in index["mods"] if m["uid"] == u) for u in uids]
    if uids:
        index["featured"] = uids
    else:
        index.pop("featured", None)
    github_files(wt)
    save_index(wt, index)
    print(f"  em destaque: {', '.join(names) if names else '(nenhum)'}")
    commit(wt, f"Destaque: {', '.join(names)}" if names else "Sem destaque", dry)


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
    ap.add_argument("--feature", nargs="*", metavar="UID", help="mods em destaque no Início (uid ou id)")
    args = ap.parse_args()
    try:
        if args.remove:
            remove(args.remove, args.dry_run)
        elif args.feature is not None:
            feature(args.feature, args.dry_run)
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
