"""Teste nativo com IL2CPP/Unity simulados. Linux/WSL, requer g++ ou CXX.

    python3 tools/tests/texturepacks/run.py

Compila o TexturePacks.cpp de producao. Nao valida a decodificacao Unity nem
o desenho do Terraria; para isso e necessario rodar o APK no dispositivo.
"""
import os
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[3]
TEST = Path(__file__).resolve().parent
OUT = ROOT / "build" / "texture-validation" / "native-test"
OUT.parent.mkdir(parents=True, exist_ok=True)
subprocess.run([
    os.environ.get("CXX", "g++"), "-std=c++20", "-Wall", "-Wextra", "-Werror",
    "-pthread", "-I", str(TEST / "host"), "-I", str(ROOT / "app/src/main/cpp"),
    str(TEST / "native.cpp"), str(ROOT / "app/src/main/cpp/content/textures/TexturePacks.cpp"),
    "-o", str(OUT),
], check=True)
subprocess.run([str(OUT)], check=True)
