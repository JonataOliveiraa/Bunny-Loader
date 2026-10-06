#!/usr/bin/env python3
"""Baixa mods públicos do Workshop do tModLoader e extrai com tModUnpacker.

Item do Workshop do Terraria (resource pack, mundo...) não vem pelo SteamCMD
anônimo: o Terraria é pago, e a Steam só entrega a quem tem o jogo. Para
esses, o script copia o item que a Steam deste PC já baixou (inscreva-se nele
na página do Workshop).
"""
from __future__ import annotations

import argparse
import hashlib
import io
import json
import os
import re
import shutil
import struct
import subprocess
import sys
import tempfile
import urllib.parse
import urllib.request
import zipfile
import zlib
from datetime import datetime
from pathlib import Path, PureWindowsPath
from typing import BinaryIO

ROOT = Path(__file__).resolve().parents[2]
APP_ID = "1281930"
TERRARIA_APP_ID = "105600"
DETAILS_URL = "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/"
CACHE = ROOT / "build" / "tmod-tools"
OUT = ROOT / "out" / "tmod"
STEAM_URL = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip"
UNPACKER_URL = "https://github.com/IVogel/tModUnpacker/releases/download/1.3/tModUnpacker.7z"
UNPACKER_SHA256 = "02f96afd4aadeebecdf5e6b03fad9ca0a4921def10913005058b86e4489f364c"
SEVEN_ZIP_URL = "https://www.7-zip.org/a/7zr.exe"
MAX_DATA = 2 * 1024**3
RESERVED = {"CON", "PRN", "AUX", "NUL", *(f"COM{i}" for i in range(1, 10)),
            *(f"LPT{i}" for i in range(1, 10))}


class ToolError(Exception):
    pass


class ProcessError(ToolError):
    def __init__(self, code: int, text: str, log: Path):
        self.code = code
        self.text = text
        super().__init__(f"Ferramenta encerrou com código {code}.\n{text[-2500:]}\nLog: {log}")


def workshop_id(value: str) -> str:
    value = value.strip()
    if not re.fullmatch(r"[0-9]+", value):
        url = urllib.parse.urlsplit(value)
        if (url.scheme not in {"http", "https"} or
                url.netloc.lower() not in {"steamcommunity.com", "www.steamcommunity.com"} or
                url.path.rstrip("/") not in {"/sharedfiles/filedetails", "/sharedfiles/filedetails.php"}):
            raise ToolError("Use um ID ou um link steamcommunity.com/sharedfiles/filedetails/?id=...")
        values = urllib.parse.parse_qs(url.query).get("id", [])
        if len(values) != 1 or not re.fullmatch(r"[0-9]+", values[0]):
            raise ToolError("O link precisa ter um único parâmetro id numérico.")
        value = values[0]
    if len(value) > 20 or not 0 < int(value) < 2**64:
        raise ToolError("ID do Workshop fora do intervalo permitido.")
    return str(int(value))


def safe_name(value: str, *, single: bool = False) -> str:
    """O unpacker combina os nomes internos diretamente com a pasta de saída."""
    value = value.replace("\\", "/")
    parts = value.split("/")
    if (not value or PureWindowsPath(value).is_absolute() or
            (single and len(parts) != 1) or any(
                p in {"", ".", ".."} or p.endswith((" ", ".")) or
                re.search(r'[<>:"|?*\x00-\x1f]', p) or
                p.split(".")[0].upper() in RESERVED for p in parts)):
        raise ToolError(f"Nome de arquivo inseguro no pacote: {value!r}")
    return value


def read_exact(stream: BinaryIO, size: int) -> bytes:
    if size < 0:
        raise ToolError("Tamanho negativo no pacote.")
    data = stream.read(size)
    if len(data) != size:
        raise ToolError("Arquivo .tmod incompleto.")
    return data


def read_int(stream: BinaryIO) -> int:
    return struct.unpack("<i", read_exact(stream, 4))[0]


def read_string(stream: BinaryIO) -> str:
    length = 0
    for shift in range(0, 35, 7):
        byte = read_exact(stream, 1)[0]
        length |= (byte & 0x7f) << shift
        if not byte & 0x80:
            if length > 32768:
                raise ToolError("Texto interno excessivamente longo no .tmod.")
            return read_exact(stream, length).decode("utf-8")
    raise ToolError("Comprimento de texto inválido no .tmod.")


def validate_tmod(path: Path) -> tuple[str, list[tuple[str, int | None]]]:
    """Confere integridade e destinos antes de executar o extrator externo."""
    with path.open("rb") as stream, tempfile.TemporaryFile() as legacy:
        if read_exact(stream, 4) != b"TMOD":
            raise ToolError(f"Não é um arquivo .tmod: {path}")
        version = read_string(stream)
        if not re.fullmatch(r"\d+(?:\.\d+){1,3}", version):
            raise ToolError("Versão do tModLoader inválida no pacote.")
        old = tuple(map(int, version.split("."))) < (0, 11)
        expected_hash = read_exact(stream, 20)
        read_exact(stream, 256)
        data_len = read_int(stream)
        start = stream.tell()
        if data_len != path.stat().st_size - start or not 0 <= data_len <= MAX_DATA:
            raise ToolError("Tamanho do .tmod incorreto ou maior que 2 GiB.")
        digest = hashlib.sha1()
        while block := stream.read(1024 * 1024):
            digest.update(block)
        if digest.digest() != expected_hash:
            raise ToolError(f"Hash do .tmod inválido (download corrompido): {path}")
        stream.seek(start)
        if old:
            decoder = zlib.decompressobj(-zlib.MAX_WBITS)
            while block := stream.read(1024 * 1024):
                while block:
                    remaining = MAX_DATA - legacy.tell()
                    expanded = decoder.decompress(block, remaining + 1)
                    if len(expanded) > remaining:
                        raise ToolError("Pacote antigo descomprimido maior que 2 GiB.")
                    legacy.write(expanded)
                    block = decoder.unconsumed_tail
            if not decoder.eof or decoder.unused_data:
                raise ToolError("Compressão inválida no pacote antigo.")
            legacy.seek(0)
            table = legacy
            table_size = legacy.seek(0, io.SEEK_END)
            legacy.seek(0)
        else:
            table = stream
            table_size = path.stat().st_size
        name = safe_name(read_string(table), single=True)
        read_string(table)  # versão do mod
        count = read_int(table)
        if not 0 < count <= 100000:
            raise ToolError("Quantidade de arquivos inválida no .tmod.")
        files = []
        seen = set()
        stored_total = 0
        expanded_total = 0
        for _ in range(count):
            member = safe_name(read_string(table))
            size = read_int(table)
            stored = size if old else read_int(table)
            if size < 0 or stored < 0:
                raise ToolError("Tamanho negativo na tabela do .tmod.")
            expanded_total += size
            if expanded_total > MAX_DATA:
                raise ToolError("Arquivos extraídos excedem 2 GiB.")
            output = Path(member).with_suffix(".png").as_posix() if member.lower().endswith(".rawimg") else member
            key = output.replace("\\", "/").casefold()
            if key in seen:
                raise ToolError(f"Arquivos com o mesmo destino no pacote: {output}")
            seen.add(key)
            files.append((output, None if member.lower().endswith(".rawimg") else size))
            if old:
                if table.tell() + size > table_size:
                    raise ToolError("Conteúdo truncado no pacote antigo.")
                table.seek(size, io.SEEK_CUR)
            else:
                stored_total += stored
        if table.tell() + stored_total != table_size:
            raise ToolError("Tabela de arquivos inconsistente no .tmod.")
        return name, files


def download(url: str, target: Path, sha256: str | None = None) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    request = urllib.request.Request(url, headers={"User-Agent": "Bunny-Loader-tmod-tools"})
    with tempfile.TemporaryDirectory(dir=target.parent) as temp:
        partial = Path(temp) / "download"
        digest = hashlib.sha256()
        total = 0
        with urllib.request.urlopen(request, timeout=60) as response, partial.open("wb") as out:
            while block := response.read(1024 * 1024):
                total += len(block)
                if total > 64 * 1024**2:
                    raise ToolError("Download da ferramenta excede 64 MiB.")
                digest.update(block)
                out.write(block)
        if not total or (sha256 and digest.hexdigest() != sha256):
            raise ToolError(f"Download da ferramenta vazio ou com hash incorreto: {url}")
        partial.replace(target)


def run(command: list[str], log: Path, timeout: int, *, cwd: Path | None = None) -> str:
    log.parent.mkdir(parents=True, exist_ok=True)
    print(f"  Log: {log}", flush=True)
    try:
        with log.open("wb") as output:
            result = subprocess.run(command, cwd=cwd, stdout=output, stderr=subprocess.STDOUT,
                                    timeout=timeout, check=False)
    except subprocess.TimeoutExpired as exc:
        raise ToolError(f"Tempo limite de {timeout}s excedido. Consulte {log}") from exc
    text = log.read_text(encoding="utf-8", errors="replace")
    if result.returncode:
        raise ProcessError(result.returncode, text, log)
    return text


def executable(value: Path | None, default: Path) -> Path:
    path = (value or default).expanduser().resolve()
    if value and not path.is_file():
        raise ToolError(f"Executável não encontrado: {path}")
    return path


def ensure_steamcmd(cache: Path, supplied: Path | None) -> Path:
    exe = executable(supplied, cache / "steamcmd" / "steamcmd.exe")
    if exe.is_file():
        return exe
    print("Baixando SteamCMD oficial...", flush=True)
    archive = cache / "steamcmd.zip"
    download(STEAM_URL, archive)
    exe.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(archive) as pack:
        for member in pack.infolist():
            safe_name(member.filename)
            if member.file_size > 64 * 1024**2:
                raise ToolError("Arquivo excessivamente grande no instalador do SteamCMD.")
        pack.extractall(exe.parent)
    if not exe.is_file():
        raise ToolError("O instalador não contém steamcmd.exe.")
    return exe


def ensure_unpacker(cache: Path, supplied: Path | None) -> Path:
    exe = executable(supplied, cache / "unpacker-1.3" / "tModUnpacker.exe")
    if exe.is_file():
        return exe
    print("Preparando tModUnpacker 1.3 oficial...", flush=True)
    archive = cache / "tModUnpacker-1.3.7z"
    download(UNPACKER_URL, archive, UNPACKER_SHA256)
    seven = cache / "7zr.exe"
    if not seven.is_file():
        download(SEVEN_ZIP_URL, seven)
    exe.parent.mkdir(parents=True, exist_ok=True)
    # Só os dois arquivos necessários da release cujo SHA-256 foi conferido.
    run([str(seven), "x", str(archive), f"-o{exe.parent}", "-y",
         "tModUnpacker.exe", "tModUnpacker.exe.config"], cache / "setup-unpacker.log", 120)
    if not exe.is_file():
        raise ToolError("A release não contém tModUnpacker.exe.")
    return exe


def item_details(item: str) -> tuple[str, str] | None:
    """(app do item, título) pela API pública da Steam; None sem resposta."""
    body = urllib.parse.urlencode({"itemcount": 1, "publishedfileids[0]": item}).encode()
    try:
        with urllib.request.urlopen(urllib.request.Request(DETAILS_URL, data=body), timeout=20) as response:
            data = json.load(response)
        details = data["response"]["publishedfiledetails"][0]
    except (OSError, ValueError, KeyError, IndexError):
        return None
    if details.get("result") != 1:
        return None
    return str(details.get("consumer_app_id", "")), str(details.get("title", ""))


def steam_libraries() -> list[Path]:
    """As bibliotecas da Steam instalada (a pasta dela e as do libraryfolders.vdf)."""
    try:
        import winreg
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Software\Valve\Steam") as key:
            steam = Path(winreg.QueryValueEx(key, "SteamPath")[0])
    except OSError:
        return []
    libraries = [steam]
    vdf = steam / "steamapps" / "libraryfolders.vdf"
    if vdf.is_file():
        for match in re.finditer(r'"path"\s+"([^"]+)"', vdf.read_text(encoding="utf-8", errors="replace")):
            libraries.append(Path(match.group(1).replace("\\\\", "\\")))
    return list(dict.fromkeys(p.resolve() for p in libraries))


def find_steam_item(app: str, item: str, libraries: list[Path]) -> Path | None:
    """A pasta do item que a Steam deste PC já baixou, se houver."""
    for library in libraries:
        folder = library / "steamapps" / "workshop" / "content" / app / item
        if folder.is_dir() and any(folder.iterdir()):
            return folder
    return None


def copy_steam_item(folder: Path, destination: Path) -> int:
    """Copia o item, conferindo nomes e tamanho como no .tmod. Devolve os arquivos."""
    files = [p for p in folder.rglob("*") if p.is_file()]
    if sum(p.stat().st_size for p in files) > MAX_DATA:
        raise ToolError("Item excessivamente grande.")
    for path in files:
        for part in path.relative_to(folder).parts:
            safe_name(part, single=True)
    if destination.exists():
        raise ToolError(f"Pasta de destino já existe: {destination}")
    shutil.copytree(folder, destination)
    return len(files)


def fetch_mod(item: str, steam: Path, cache: Path, log: Path, timeout: int) -> Path:
    install = cache / "workshop"
    install.mkdir(parents=True, exist_ok=True)
    print(f"Baixando Workshop {item} (primeiro uso também atualiza o SteamCMD)...", flush=True)
    command = [str(steam), "+force_install_dir", str(install), "+login", "anonymous",
               "+workshop_download_item", APP_ID, item, "+quit"]
    try:
        text = run(command, log, timeout, cwd=steam.parent)
    except ProcessError as exc:
        # O bootstrap Windows encerra com 7 após instalar sua própria atualização.
        if exc.code != 7 or not re.search(r"Update complete, launching", exc.text, re.IGNORECASE):
            raise
        log.replace(log.with_name(log.stem + "-bootstrap.log"))
        print("SteamCMD atualizado; continuando o download...", flush=True)
        text = run(command, log, timeout, cwd=steam.parent)
    # SteamCMD pode retornar zero mesmo quando o download do Workshop falha.
    match = re.search(rf'Success\. Downloaded item {item} to ["\'](.+?)["\']', text)
    if not match:
        raise ToolError(f"Steam não confirmou o download do mod {item}. O item pode estar privado, "
                        f"indisponível ou exigir acesso pela Steam.\n{text[-1800:]}\nLog: {log}")
    folder = Path(match.group(1)).resolve()
    allowed = (install.resolve(), steam.parent.resolve())
    if not any(folder.is_relative_to(root / "steamapps" / "workshop" / "content" / APP_ID)
               for root in allowed) or folder.name != item:
        raise ToolError(f"Pasta inesperada informada pelo SteamCMD: {folder}")
    if not folder.is_dir():
        raise ToolError(f"Steam confirmou o download, mas a pasta não existe: {folder}")
    return folder


def unpack(source: Path, destination: Path, unpacker: Path, log: Path, timeout: int) -> None:
    name, files = validate_tmod(source)
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.exists():
        raise ToolError(f"Pasta de extração já existe: {destination}")
    with tempfile.TemporaryDirectory(prefix=".extract-", dir=destination.parent) as temp:
        stage = Path(temp)
        run([str(unpacker), str(source), str(stage)], log, timeout)
        for member, size in files:
            extracted = stage / name / member
            if not extracted.is_file() or (size is not None and extracted.stat().st_size != size):
                raise ToolError(f"O unpacker não extraiu corretamente: {member}. Consulte {log}")
            if size is None:
                with extracted.open("rb") as png:
                    if png.read(8) != b"\x89PNG\r\n\x1a\n":
                        raise ToolError(f"Conversão de .rawimg para PNG inválida: {member}")
        stage.rename(destination)


def copy_terraria_item(item: str, title: str, result: Path) -> None:
    """Um item do Workshop do Terraria: a cópia que a Steam deste PC baixou."""
    print(f"{item} ({title}) é do Workshop do Terraria, não do tModLoader.", flush=True)
    libraries = steam_libraries()
    folder = find_steam_item(TERRARIA_APP_ID, item, libraries)
    if folder is None:
        where = (libraries[0] / "steamapps" / "workshop" / "content" / TERRARIA_APP_ID / item) if libraries else "a pasta do Workshop da Steam"
        raise ToolError(
            "O SteamCMD anônimo não baixa itens do Terraria (o jogo é pago; a Steam só os entrega a quem "
            "tem o Terraria).\n"
            f"  1. Abra https://steamcommunity.com/sharedfiles/filedetails/?id={item} e clique em Inscrever-se.\n"
            "  2. Espere a Steam baixar (abrir o Terraria uma vez também força o download).\n"
            f"  3. Rode de novo: o item é copiado de {where}.")
    destination = result / "extraido" / item
    destination.parent.mkdir(parents=True, exist_ok=True)
    count = copy_steam_item(folder, destination)
    print(f"Pronto: {count} arquivo(s) copiados da Steam.\n  De: {folder}\n  Para: {destination}", flush=True)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("links", nargs="*", help="Links do Workshop ou IDs (um ou vários)")
    parser.add_argument("--local", type=Path, action="append", default=[], metavar="ARQUIVO.tmod",
                        help="Extrai um .tmod local sem consultar a Steam (pode repetir)")
    parser.add_argument("--out", type=Path, default=OUT, help="Pasta de saída (padrão: out/tmod)")
    parser.add_argument("--cache", type=Path, default=CACHE, help="Cache de ferramentas e do Workshop")
    parser.add_argument("--steamcmd", type=Path, help="Usa um steamcmd.exe já instalado")
    parser.add_argument("--unpacker", type=Path, help="Usa um tModUnpacker.exe já instalado")
    parser.add_argument("--timeout", type=int, default=900, help="Limite por ferramenta em segundos")
    parser.add_argument("--ask", action="store_true", help="Pede um link/ID no terminal")
    args = parser.parse_args(argv)
    if args.ask and not args.links and not args.local:
        try:
            args.links = [input("Link ou ID do Workshop: ").strip()]
        except (EOFError, KeyboardInterrupt):
            return 130
    if not args.links and not args.local:
        parser.error("Informe um link/ID ou --local ARQUIVO.tmod.")
    if args.timeout <= 0:
        parser.error("--timeout precisa ser positivo.")
    if os.name != "nt":
        parser.error("Este script usa as versões Windows do SteamCMD e tModUnpacker.")
    try:
        items = list(dict.fromkeys(workshop_id(link) for link in args.links))
        local_files = [p.expanduser().resolve() for p in args.local]
        for path in local_files:
            if not path.is_file() or path.suffix.lower() != ".tmod":
                raise ToolError(f"Arquivo .tmod local não encontrado: {path}")
            validate_tmod(path)
        cache = args.cache.expanduser().resolve()
        out = args.out.expanduser().resolve()
        cache.mkdir(parents=True, exist_ok=True)
        unpacker = ensure_unpacker(cache, args.unpacker)
        steam = ensure_steamcmd(cache, args.steamcmd) if items else None
        jobs = [(item, None) for item in items] + [(f"local-{p.stem}", p) for p in local_files]
        failed = 0
        for label, local in jobs:
            # Cada execução preserva os resultados anteriores, inclusive os .tmod originais.
            job = out / safe_name(label, single=True)
            job.mkdir(parents=True, exist_ok=True)
            result = Path(tempfile.mkdtemp(prefix=datetime.now().strftime("%Y%m%d-%H%M%S-"), dir=job))
            try:
                details = None if local else item_details(label)
                if details and details[0] == TERRARIA_APP_ID:
                    copy_terraria_item(label, details[1], result)
                    continue
                if details and details[0] and details[0] != APP_ID:
                    raise ToolError(f"O item {label} ({details[1]}) é do app {details[0]} da Steam, "
                                    f"nem do tModLoader nem do Terraria.")
                source_root = local.parent if local else fetch_mod(label, steam, cache,
                                                                 result / "steamcmd.log", args.timeout)
                packages = [local] if local else sorted(source_root.rglob("*.tmod"))
                if not packages:
                    raise ToolError("O item foi baixado, mas não contém arquivos .tmod (confira se é um mod do tModLoader).")
                for package in packages:
                    relative = Path(package.name) if local else package.relative_to(source_root)
                    for part in relative.parts:
                        safe_name(part, single=True)
                    original = result / "tmod" / relative
                    original.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(package, original)
                    destination = result / "extraido" / relative.with_suffix("")
                    print(f"Extraindo {relative}...", flush=True)
                    unpack(original, destination, unpacker,
                           result / "logs" / relative.with_suffix(".log"), args.timeout)
                print(f"Pronto: {len(packages)} pacote(s).\n  .tmod: {result / 'tmod'}\n  Extraído: {result / 'extraido'}", flush=True)
            except (ToolError, OSError, ValueError, zlib.error) as exc:
                failed += 1
                print(f"Erro em {label}: {exc}\nResultados/logs: {result}", file=sys.stderr, flush=True)
        return 1 if failed else 0
    except (ToolError, OSError, ValueError, zipfile.BadZipFile, zlib.error) as exc:
        print(f"Erro: {exc}", file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        print("\nInterrompido. Resultados anteriores foram preservados.", file=sys.stderr)
        return 130


if __name__ == "__main__":
    raise SystemExit(main())
