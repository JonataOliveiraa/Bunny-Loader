#!/usr/bin/env python3
"""Gerenciador do catálogo online, no navegador.

    python tools/mods/manager.py          (ou dois cliques em gerenciar.bat)

Sobe uma página em http://127.0.0.1:8770 e a abre no navegador. Tudo que ela
faz passa pelo publish.py e pelo pack.py, com as mesmas regras e conferências
da linha de comando: a página é só outro jeito de chamar os dois.

O servidor só atende este computador (127.0.0.1) e só aceita pedido que traga
a chave sorteada nesta execução, que vai embutida na página. Um site aberto em
outra aba não consegue publicar nada em seu nome. O token do GitHub fica na
memória deste processo e some quando ele fecha; nunca é gravado em disco.
"""
from __future__ import annotations

import base64
import contextlib
import datetime
import io
import json
import mimetypes
import re
import secrets
import sys
import threading
import traceback
import webbrowser
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlparse

sys.path.insert(0, str(Path(__file__).resolve().parent))
import pack  # noqa: E402
import publish  # noqa: E402

PORT = 8770
HERE = Path(__file__).resolve().parent
SAMPLES = publish.ROOT / "samples"
UPLOADS = publish.ROOT / "build" / "mods-uploads"
# Fica no APK, não no catálogo (BUNDLED_SAMPLES no app/build.gradle.kts).
BUNDLED = {"ExampleMod"}
KEY = secrets.token_urlsafe(24)
LOCK = threading.Lock()
VERSION_FIELD = re.compile(r'("version"\s*:\s*")([^"]*)(")')
UPDATED_FIELD = re.compile(r'("updated"\s*:\s*")([^"]*)(")')

publish.INTERACTIVE = False


def version_key(v: str) -> list[int]:
    return [int(x) if x.isdigit() else 0 for x in re.split(r"[.\-+]", v.removeprefix("v"))]


def compare(a: str, b: str) -> int:
    ka, kb = version_key(a), version_key(b)
    n = max(len(ka), len(kb))
    ka += [0] * (n - len(ka))
    kb += [0] * (n - len(kb))
    return (ka > kb) - (ka < kb)


def run(action) -> dict:
    """Roda uma operação do publish.py e devolve o que ela escreveu.

    Uma de cada vez: todas mexem no mesmo worktree e no mesmo índice.
    """
    out = io.StringIO()
    with LOCK, contextlib.redirect_stdout(out), contextlib.redirect_stderr(out):
        try:
            result = action()
            return {"ok": True, "log": out.getvalue(), "result": result}
        except publish.Fail as e:
            return {"ok": False, "log": out.getvalue(), "error": str(e)}
        except Exception as e:  # noqa: BLE001 — a página mostra; o servidor segue de pé
            traceback.print_exc()
            return {"ok": False, "log": out.getvalue(), "error": f"{type(e).__name__}: {e}"}


# ------------------------------------------------------------------ estado

def index_entries() -> list[dict]:
    wt = publish.WORKTREE
    if not (wt / ".git").exists():
        return []
    entries = []
    for m in publish.load_index(wt).get("mods", []):
        manifest = {}
        f = wt / "mods" / m["uid"] / "manifest.json"
        if f.exists():
            manifest = json.loads(f.read_text(encoding="utf-8-sig"))
        tag = m["download"]["url"].split("/releases/download/")[1].split("/")[0]
        entries.append({
            **m,
            "authors": author_line(manifest),
            "category": manifest.get("category", ""),
            "summary": manifest.get("summary", ""),
            "icon": f"/store/{m['uid']}/icon.png" if "icon.png" in m.get("files", []) else None,
            "release": f"https://github.com/{publish.OWNER}/{publish.REPO}/releases/tag/{tag}",
        })
    return entries


def author_line(manifest: dict) -> str:
    names = []
    for a in manifest.get("authors") or []:
        names.append(a if isinstance(a, str) else a.get("name", ""))
    if not names and manifest.get("author"):
        names = [manifest["author"]]
    return ", ".join(n for n in names if n)


def status_against(index: dict[str, dict], uid: str, version: str, sha: str) -> tuple[str, str]:
    """(código, frase) do pacote local comparado com o publicado."""
    pub = index.get(uid)
    if not pub:
        return "new", "Ainda não publicado"
    if pub["download"]["sha256"] == sha:
        return "same", "Em dia"
    c = compare(version, pub["version"])
    if c > 0:
        return "newer", f"Versão nova pronta (publicado: v{pub['version']})"
    if c == 0:
        return "changed", "Mudou, mas a versão é a mesma: suba a versão"
    return "older", f"Local é mais velho que o publicado (v{pub['version']})"


def samples_state(index: dict[str, dict]) -> list[dict]:
    out = []
    for folder in sorted(p for p in SAMPLES.iterdir() if (p / "manifest.json").is_file()):
        if folder.name in BUNDLED:
            continue
        try:
            m = json.loads((folder / "manifest.json").read_text(encoding="utf-8-sig"))
            data = pack.pack_bytes(folder)
        except Exception as e:  # noqa: BLE001
            out.append({"folder": folder.name, "name": folder.name, "status": "error",
                        "statusText": f"manifest.json ilegível: {e}"})
            continue
        sha = publish.hashlib.sha256(data).hexdigest()
        code, text = status_against(index, m.get("uid", ""), m.get("version", ""), sha)
        out.append({
            "folder": folder.name,
            "uid": m.get("uid", ""),
            "name": m.get("name", folder.name),
            "version": m.get("version", ""),
            "authors": author_line(m),
            "size": len(data),
            "icon": f"/sample/{folder.name}/icon.png" if (folder / "icon.png").is_file() else None,
            "status": code,
            "statusText": text,
        })
    return out


def state() -> dict:
    entries = index_entries()
    by_uid = {e["uid"]: e for e in entries}
    return {
        "tokenSet": publish.has_token(),
        "worktree": (publish.WORKTREE / ".git").exists(),
        "catalog": entries,
        "samples": samples_state(by_uid),
    }


# ------------------------------------------------------------------ ações

def sync() -> None:
    publish.worktree()
    print("Catálogo sincronizado com o GitHub.")


def check_upload(path: Path) -> dict:
    pkg = publish.Package(path)
    index = {e["uid"]: e for e in index_entries()}
    code, text = status_against(index, pkg.uid, pkg.version, pkg.sha256)
    return {
        "file": path.name,
        "uid": pkg.uid,
        "id": pkg.id,
        "name": pkg.name,
        "version": pkg.version,
        "authors": author_line(pkg.manifest),
        "category": pkg.manifest.get("category", ""),
        "summary": pkg.manifest.get("summary", ""),
        "size": pkg.size,
        "sha256": pkg.sha256,
        "store": pkg.store_files(),
        # Ainda não está no worktree para servir por /store: vai junto.
        "icon": ("data:image/png;base64," + base64.b64encode(pkg.zip.read("icon.png")).decode()
                 if "icon.png" in pkg.names else None),
        "status": code,
        "statusText": text,
    }


def require_token() -> None:
    if not publish.has_token():
        raise publish.Fail("informe o token do GitHub no topo da página antes de publicar")


def publish_sample(folder: str) -> None:
    require_token()
    path = sample_dir(folder)
    out = pack.pack(path)
    print(f"Empacotado: {out.name}")
    publish.publish([out], dry=False)


def publish_upload(name: str) -> None:
    require_token()
    path = upload_path(name)
    if not path.is_file():
        raise publish.Fail("o arquivo enviado não está mais aqui; envie de novo")
    publish.publish([path], dry=False)


def remove(uid: str) -> None:
    if not publish.UID.match(uid):
        raise publish.Fail("uid inválido")
    publish.remove(uid, dry=False)


def bump(folder: str, part: str) -> str:
    """Sobe a versão no manifest.json do sample (e a data de `updated`)."""
    f = sample_dir(folder) / "manifest.json"
    text = f.read_text(encoding="utf-8")
    match = VERSION_FIELD.search(text)
    if not match:
        raise publish.Fail("manifest.json sem \"version\"")
    nums = (version_key(match.group(2)) + [0, 0, 0])[:3]
    i = {"major": 0, "minor": 1, "patch": 2}.get(part)
    if i is None:
        raise publish.Fail("parte de versão inválida")
    nums[i] += 1
    for j in range(i + 1, 3):
        nums[j] = 0
    new = ".".join(map(str, nums))
    text = VERSION_FIELD.sub(lambda m: m.group(1) + new + m.group(3), text, count=1)
    today = datetime.date.today().isoformat()
    text = UPDATED_FIELD.sub(lambda m: m.group(1) + today + m.group(3), text, count=1)
    f.write_text(text, encoding="utf-8", newline="\n")
    print(f"{folder}: v{match.group(2)} -> v{new}. Lembre de anotar no changelog.md.")
    return new


def sample_dir(folder: str) -> Path:
    path = (SAMPLES / folder).resolve()
    if path.parent != SAMPLES.resolve() or not (path / "manifest.json").is_file():
        raise publish.Fail(f"não achei samples/{folder}")
    if folder in BUNDLED:
        raise publish.Fail(f"{folder} vai dentro do APK, não no catálogo")
    return path


def upload_path(name: str) -> Path:
    clean = re.sub(r"[^A-Za-z0-9._ -]", "_", Path(name).name)
    if not clean.lower().endswith((".bl", ".bmod", ".zip")):
        raise publish.Fail("o arquivo precisa ser .bl, .bmod ou .zip")
    return UPLOADS / clean


# ------------------------------------------------------------------ HTTP

class Handler(BaseHTTPRequestHandler):
    server_version = "BunnyMods"

    def log_message(self, fmt, *args):  # o console fica para os erros
        pass

    def host_ok(self) -> bool:
        # Contra DNS rebinding: um site que aponte o próprio nome para
        # 127.0.0.1 ainda chega com o Host dele.
        return self.headers.get("Host", "") in (f"127.0.0.1:{PORT}", f"localhost:{PORT}")

    def send(self, code: int, body: bytes, ctype: str) -> None:
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def json(self, data, code: int = 200) -> None:
        self.send(code, json.dumps(data, ensure_ascii=False).encode(), "application/json; charset=utf-8")

    def file(self, path: Path, base: Path) -> None:
        path = path.resolve()
        if not path.is_file() or base.resolve() not in path.parents:
            self.send(404, b"", "text/plain")
            return
        ctype = mimetypes.guess_type(path.name)[0] or "application/octet-stream"
        self.send(200, path.read_bytes(), ctype)

    def do_GET(self):
        if not self.host_ok():
            return self.send(403, b"", "text/plain")
        path = unquote(urlparse(self.path).path)
        if path == "/":
            html = (HERE / "manager.html").read_text(encoding="utf-8").replace("__BL_KEY__", KEY)
            return self.send(200, html.encode(), "text/html; charset=utf-8")
        if path.startswith("/store/"):
            return self.file(publish.WORKTREE / "mods" / path[len("/store/"):], publish.WORKTREE / "mods")
        if path.startswith("/sample/"):
            return self.file(SAMPLES / path[len("/sample/"):], SAMPLES)
        self.send(404, b"", "text/plain")

    def do_POST(self):
        if not self.host_ok() or self.headers.get("X-BL-Key") != KEY:
            return self.send(403, b"", "text/plain")
        path = urlparse(self.path).path
        length = int(self.headers.get("Content-Length") or 0)
        if length > 200 << 20:
            return self.json({"ok": False, "error": "arquivo grande demais"}, 413)
        body = self.rfile.read(length)

        if path == "/api/upload":
            name = unquote(self.headers.get("X-Filename", ""))

            def save():
                target = upload_path(name)
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(body)
                return check_upload(target)
            return self.json(run(save))

        data = json.loads(body or b"{}")
        if path == "/api/state":
            return self.json(run(state))
        if path == "/api/token":
            publish.set_token(data.get("token"))
            return self.json({"ok": True, "result": publish.has_token()})
        if path == "/api/sync":
            return self.json(run(sync))
        if path == "/api/publish-sample":
            return self.json(run(lambda: publish_sample(data.get("folder", ""))))
        if path == "/api/publish-upload":
            return self.json(run(lambda: publish_upload(data.get("file", ""))))
        if path == "/api/remove":
            return self.json(run(lambda: remove(data.get("uid", ""))))
        if path == "/api/bump":
            return self.json(run(lambda: bump(data.get("folder", ""), data.get("part", "patch"))))
        self.send(404, b"", "text/plain")


def main() -> int:
    open_browser = "--no-browser" not in sys.argv
    print("Sincronizando o catálogo com o GitHub...")
    r = run(sync)
    print(r["log"].strip() or r.get("error", ""))
    try:
        server = ThreadingHTTPServer(("127.0.0.1", PORT), Handler)
    except OSError:
        print(f"A porta {PORT} já está em uso: o gerenciador já está aberto? "
              f"Tente http://127.0.0.1:{PORT}")
        if open_browser:
            webbrowser.open(f"http://127.0.0.1:{PORT}")
        return 1
    url = f"http://127.0.0.1:{PORT}"
    print(f"Gerenciador aberto em {url}  (Ctrl+C para fechar)")
    if open_browser:
        webbrowser.open(url)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    return 0


if __name__ == "__main__":
    sys.exit(main())
