#!/usr/bin/env bash
# Gera os DOIS APKs do produto (modelo launcher separado):
#   out/bunny-loader.apk            -> o launcher (com.bunnyloader)
#   out/terraria-bunny-coexist.apk  -> o Terraria modificado, pacote renomeado
#                                      (com.bunnyloader.terraria.paid), coexiste
#                                      com o Terraria original, com o icone do coelho.
#
# Instale os dois no celular (desinstalar Terraria original NAO e necessario):
#   1) terraria-bunny-coexist.apk  (o jogo com mods, ao lado do original)
#   2) bunny-loader.apk            (o launcher; botao "Iniciar Terraria com mods")
#
#   tools/build-release.sh [--no-build]
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"; cd "$ROOT"
export JAVA_HOME="/c/Program Files/Android/Android Studio/jbr"
export PATH="$JAVA_HOME/bin:$PATH"; export MSYS_NO_PATHCONV=1
GRADLE=$(ls -d "$USERPROFILE/.gradle/wrapper/dists/gradle-8.9-bin"/*/gradle-8.9/bin/gradle | head -1)
NEWPKG="com.bunnyloader.terraria.paid"
SP="$ROOT/.devcycle"

if [ "${1:-}" != "--no-build" ]; then
    echo "==> compilando (libbunny + launcher)"
    "$GRADLE" assembleDebug --console=plain >/dev/null 2>&1 || {
        echo "ERRO no build"; exit 1; }
fi

echo "==> extraindo libs arm64"
rm -rf "$SP"; mkdir -p "$SP"
unzip -o -j app/build/outputs/apk/debug/app-debug.apk \
    "lib/arm64-v8a/libbunny.so" "lib/arm64-v8a/libshadowhook.so" -d "$SP" >/dev/null

mkdir -p out
echo "==> jogo modificado (renomeado + icone)"
python tools/repack.py --apk refs/base.apk \
    --lib "$(cygpath -w "$SP/libbunny.so")" --dep "$(cygpath -w "$SP/libshadowhook.so")" \
    --icon "$(cygpath -w "$ROOT/Icon.png")" --rename "$NEWPKG" \
    --out "$(cygpath -w "$ROOT/out/terraria-bunny-coexist.apk")" >/dev/null
echo "    out/terraria-bunny-coexist.apk  ($NEWPKG)"

echo "==> launcher"
cp app/build/outputs/apk/debug/app-debug.apk out/bunny-loader.apk
echo "    out/bunny-loader.apk  (com.bunnyloader)"
echo "PRONTO. Instale os dois no celular."
