#!/usr/bin/env bash
# Testa a prontidao de tiles de mod sem abrir o jogo nem alterar saves.
set -euo pipefail
cd "$(dirname "$0")/../../.."
D="${BL_DEVICE:-127.0.0.1:16384}"
CXX="$LOCALAPPDATA/Android/Sdk/ndk/30.0.16248370/toolchains/llvm/prebuilt/windows-x86_64/bin/x86_64-linux-android26-clang++.cmd"
mkdir -p out/tilesettled
"$CXX" -std=c++20 -O2 -static-libstdc++ -ffunction-sections -fdata-sections \
    -I app/src/main/cpp tools/tests/tilesettled/main.cpp \
    app/src/main/cpp/content/common/TypeTables.cpp \
    -Wl,--gc-sections -o out/tilesettled/tilesettled
export MSYS_NO_PATHCONV=1
REMOTE="/data/local/tmp/bunny-tilesettled"
trap 'adb -s "$D" shell rm -f "$REMOTE" >/dev/null 2>&1 || true' EXIT
adb -s "$D" push out/tilesettled/tilesettled "$REMOTE" >/dev/null
adb -s "$D" shell "chmod 755 $REMOTE && $REMOTE"
