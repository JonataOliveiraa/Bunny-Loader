#!/usr/bin/env bash
# Compila o fuzz do WrapperMap para Android x86_64 e roda no MuMu.
set -euo pipefail
cd "$(dirname "$0")/../../.."
D="${BL_DEVICE:-127.0.0.1:16384}"
CXX="$LOCALAPPDATA/Android/Sdk/ndk/30.0.16248370/toolchains/llvm/prebuilt/windows-x86_64/bin/x86_64-linux-android26-clang++.cmd"
OUT="${TMPDIR:-/tmp}/wmfuzz"
"$CXX" -std=c++20 -O2 -static-libstdc++ \
    -I app/src/main/cpp -I app/src/main/cpp/third_party/quickjs \
    tools/tests/wrappermap/fuzz.cpp -o "$OUT"
export MSYS_NO_PATHCONV=1
adb -s "$D" push "$(cygpath -w "$OUT")" /data/local/tmp/wmfuzz >/dev/null
adb -s "$D" shell "chmod 755 /data/local/tmp/wmfuzz && /data/local/tmp/wmfuzz; rm -f /data/local/tmp/wmfuzz"
