"""Mods C# com fontes ("entry": "MeuMod.cs") saem do catálogo compilados.

O mesmo BunnyLoader.Compiler do app e do app do catálogo, pelo
tools/mods/csbuild: o pacote ganha content/.../bin/MeuMod.dll e o selo
bin/MeuMod.build.json, e o Bunny Loader de quem baixa só confere o selo.
"""
from __future__ import annotations

import json
import shutil
import subprocess
import tempfile
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TOOL = ROOT / "tools" / "mods" / "csbuild"
TOOL_DLL = TOOL / "bin" / "Release" / "net10.0" / "csbuild.dll"
# A pasta do runtime que vai no APK (app/build.gradle.kts, syncDotnetAssets):
# references.txt, a base do .NET e o BunnyLoader.dll.
RUNTIME = ROOT / "app" / "build" / "generated" / "blAssets" / "dotnet"


class BuildError(Exception):
    pass


def source_entry(path: Path) -> str | None:
    """O entry do manifesto, se o pacote é um mod C# com fontes."""
    with zipfile.ZipFile(path) as z:
        try:
            manifest = json.loads(z.read("manifest.json").decode("utf-8-sig"))
        except (KeyError, ValueError):
            return None
    entry = manifest.get("entry") if isinstance(manifest, dict) else None
    return entry if isinstance(entry, str) and entry.lower().endswith(".cs") else None


def parse_report(text: str) -> dict:
    report = {"status": "failed", "reason": "", "ms": 0, "files": 0, "lines": 0, "errors": [], "warnings": []}
    for line in text.splitlines():
        key, _, value = line.partition(" ")
        if key in ("status", "reason"):
            report[key] = value
        elif key == "stats":
            parts = (value.split() + ["0", "0", "0"])[:3]
            report["ms"], report["files"], report["lines"] = (int(p) if p.isdigit() else 0 for p in parts)
        elif key in ("error", "warning"):
            report[key + "s"].append(value)
    return report


def tool() -> Path:
    if not (RUNTIME / "references.txt").is_file():
        raise BuildError("falta o runtime do C# (app/build/generated/blAssets/dotnet): "
                         "rode o gradle :app:syncDotnetAssets uma vez")
    sources = [p for p in TOOL.rglob("*") if p.suffix in (".cs", ".csproj") and "bin" not in p.parts and "obj" not in p.parts]
    compiler = ROOT / "app" / "src" / "main" / "csharp" / "BunnyLoader.Compiler"
    sources += [p for p in compiler.glob("*") if p.suffix in (".cs", ".csproj")]
    if not TOOL_DLL.is_file() or max(p.stat().st_mtime for p in sources) > TOOL_DLL.stat().st_mtime:
        r = subprocess.run(["dotnet", "build", str(TOOL / "csbuild.csproj"), "-c", "Release", "-nologo", "-v", "q"],
                           capture_output=True, text=True)
        if r.returncode != 0:
            raise BuildError("o csbuild não compilou:\n" + (r.stdout + r.stderr)[-2000:])
    return TOOL_DLL


def prepare(path: Path) -> dict | None:
    """Compila o .bl em `path` no lugar, se é mod com fontes e o bin/ dele não bate. None se não é."""
    entry = source_entry(path)
    if entry is None:
        return None
    dll = tool()
    folder = Path(tempfile.mkdtemp(prefix="blcs-"))
    try:
        with zipfile.ZipFile(path) as z:
            root = folder.resolve()
            for name in z.namelist():
                if not (root / name).resolve().is_relative_to(root):
                    raise BuildError(f"{path.name}: {name!r} sai do pacote")
            z.extractall(folder)
        r = subprocess.run(["dotnet", str(dll), str(folder / "content" / entry), str(RUNTIME)],
                           capture_output=True, text=True, encoding="utf-8")
        report = parse_report(r.stdout)
        for w in report["warnings"][:12]:
            print(f"  aviso do C#: {w}")
        if report["status"] == "failed":
            shown = "\n".join("  " + e for e in report["errors"][:12]) or r.stderr[-1000:]
            raise BuildError(f"o C# não compilou ({len(report['errors'])} erro(s)):\n{shown}")
        project = "content/" + (entry.rsplit("/", 1)[0] + "/" if "/" in entry else "")
        name = entry.rsplit("/", 1)[-1][:-3]
        outputs = [f"{project}bin/{name}.dll", f"{project}bin/{name}.build.json"]
        if report["status"] == "compiled":
            rewrite(path, folder, outputs)
            print(f"  C#: {report['files']} arquivo(s), {report['lines']} linhas compilados em {report['ms']} ms")
        else:
            print("  C#: o bin/ do pacote já bate com as fontes")
        report["dll"] = outputs[0]
        return report
    finally:
        shutil.rmtree(folder, ignore_errors=True)


def rewrite(path: Path, folder: Path, outputs: list[str]) -> None:
    """O mesmo zip, na mesma ordem, com as saídas do compilador no lugar das velhas."""
    temp = path.with_suffix(path.suffix + ".tmp")
    with zipfile.ZipFile(path) as src, zipfile.ZipFile(temp, "w", zipfile.ZIP_DEFLATED) as out:
        for info in src.infolist():
            if info.is_dir() or info.filename in outputs:
                continue
            out.writestr(info, src.read(info), compress_type=zipfile.ZIP_DEFLATED)
        for name in outputs:
            out.write(folder / name, name)
    temp.replace(path)
