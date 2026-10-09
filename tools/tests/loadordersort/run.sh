#!/usr/bin/env bash
# Testa a ordenacao por dependencias do nucleo no PC, sem aparelho.
# No Windows: wsl bash tools/tests/loadordersort/run.sh
set -euo pipefail
cd "$(dirname "$0")/../../.."
mkdir -p out/loadordersort
g++ -std=c++20 -O1 -Wall -Wextra -fsanitize=address,undefined \
    -I app/src/main/cpp tools/tests/loadordersort/main.cpp \
    app/src/main/cpp/mods/LoadOrder.cpp app/src/main/cpp/mods/ManifestText.cpp -o out/loadordersort/loadordersort
out/loadordersort/loadordersort
