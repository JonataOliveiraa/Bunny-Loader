"""Compile the production native metadata loader with engine/texture stubs (Linux/WSL)."""
import os
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[3]
TEST = Path(__file__).resolve().parent
OUT = ROOT / 'build/serversync/native-test'
OUT.parent.mkdir(parents=True, exist_ok=True)
subprocess.run([
    os.environ.get('CXX', 'g++'), '-std=c++20', '-Wall', '-Wextra', '-Werror',
    '-fsanitize=address,undefined', '-fno-omit-frame-pointer',
    '-I', str(TEST / 'host'), '-I', str(ROOT / 'app/src/main/cpp'),
    str(TEST / 'native.cpp'), str(ROOT / 'app/src/main/cpp/mods/ModLoader.cpp'),
    '-o', str(OUT),
], check=True)
subprocess.run([str(OUT)], check=True)
