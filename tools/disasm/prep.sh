#!/usr/bin/env bash
# Gera os caches que os scripts desta pasta usam (a partir de refs/, ver
# refs/README.md). Rode uma vez depois de cada tools/dump.sh.
#
#   da.py      desassembla um metodo pelo nome, anotando chamadas e metadados
#   xref.py    quem chama (BL/B) um endereco
#   up.py      sobe a cadeia de chamadas do runtime ate uma API exportada
#   strscan.py quem escreve um offset (ex.: um campo estatico)
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
BIN="$LOCALAPPDATA/Android/Sdk/ndk/30.0.16248370/toolchains/llvm/prebuilt/windows-x86_64/bin"
SO="$HERE/../../refs/libil2cpp.so"
python "$HERE/got.py"
"$BIN/llvm-nm.exe" -D --defined-only "$SO" 2>/dev/null | sort > "$HERE/dynsym.txt"
"$BIN/llvm-readelf.exe" --unwind "$SO" 2>/dev/null \
    | grep -o "initial_location: 0x[0-9a-f]*" | awk '{print $2}' > "$HERE/fstarts.txt"
python "$HERE/da.py" 'Terraria.Main$$StopRain' 5 > /dev/null   # cria script.pkl
echo "pronto: got.pkl dynsym.txt fstarts.txt script.pkl"
