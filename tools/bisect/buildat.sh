#!/usr/bin/env bash
# Compila o APK de um commit antigo, para comparar versões (busca binária de
# uma regressão). A worktree fica fora do repositório e usa por junção o que o
# git não guarda (QuickJS, assets e libs do jogo); as junções são desfeitas
# antes de apagar a pasta, então os originais nunca são tocados.
#
#   tools/bisect/buildat.sh <commit>          -> out/bisect/app-<commit>.apk
#
# Depois, no MuMu: adb install -r -d out/bisect/app-<commit>.apk e medir com
# tools/tests/frametime (ver tools/README.md). Um de cada vez: gradle com
# -Xmx1000m, dois ao mesmo tempo derrubam o MuMu.
set -euo pipefail
C="${1:?uso: buildat.sh <commit>}"
R="$(cd "$(dirname "$0")/../.." && pwd)"
HERE="$(cd "$(dirname "$0")" && pwd)"
W="${TEMP:-/tmp}/bl-bisect-$C"
OUT="$R/out/bisect"
G="${GRADLE:-$(ls -d ~/.gradle/wrapper/dists/gradle-8.9-bin/*/gradle-8.9/bin/gradle | head -1)}"

mkdir -p "$OUT"
cd "$R"
git worktree add -q -f "$W" "$C"
cp local.properties "$W/"
powershell -NoProfile -ExecutionPolicy Bypass -File "$(cygpath -w "$HERE/linkwt.ps1")" -W "$(cygpath -w "$W")" -R "$(cygpath -w "$R")"

status=0
( cd "$W" && "$G" assembleDebug -Dorg.gradle.jvmargs=-Xmx1000m -q > build.log 2>&1 ) || status=$?
( cd "$W" && "$G" --stop -q ) || true
if [ "$status" -eq 0 ]; then
    cp "$W/app/build/outputs/apk/debug/app-debug.apk" "$OUT/app-$C.apk"
    echo "ok: out/bisect/app-$C.apk"
else
    echo "build do $C falhou:"; tail -20 "$W/build.log"
fi

powershell -NoProfile -ExecutionPolicy Bypass -File "$(cygpath -w "$HERE/cleanwt.ps1")" -W "$(cygpath -w "$W")"
git worktree prune
exit "$status"
