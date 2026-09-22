#!/usr/bin/env bash
# Ciclo de desenvolvimento (modo repackage, dev no MuMu):
#   compila a libbunny.so -> repackage do jogo -> reinstala -> lanca -> log.
#
#   tools/dev-cycle.sh            # ciclo completo
#   tools/dev-cycle.sh --no-build # pula a compilacao (reaproveita a lib atual)
#
# Restaurar o Terraria original: adb uninstall <pkg> && adb install refs/base.apk
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
export JAVA_HOME="/c/Program Files/Android/Android Studio/jbr"
export PATH="$JAVA_HOME/bin:$PATH"  # apksigner/keytool precisam de java no PATH
export MSYS_NO_PATHCONV=1

PKG=com.and.games505.TerrariaPaid
ACT=com.unity3d.player.UnityPlayerActivity
SP="$(cygpath -w "$ROOT" 2>/dev/null >/dev/null && echo "$ROOT" || echo "$ROOT")/.devcycle"
GRADLE=$(ls -d "$USERPROFILE/.gradle/wrapper/dists/gradle-8.9-bin"/*/gradle-8.9/bin/gradle | head -1)
D=(-s "${ADB_SERIAL:-emulator-5554}")

if [ "${1:-}" != "--no-build" ]; then
    echo "==> compilando libbunny.so"
    "$GRADLE" assembleDebug --console=plain >/dev/null 2>&1 || {
        echo "ERRO no build; rode o gradle sem -q para ver"; exit 1; }
fi

echo "==> extraindo libs arm64"
rm -rf "$SP"; mkdir -p "$SP"
unzip -o -j app/build/outputs/apk/debug/app-debug.apk \
    "lib/arm64-v8a/libbunny.so" "lib/arm64-v8a/libshadowhook.so" -d "$SP" >/dev/null

echo "==> repackage"
python tools/repack.py --apk refs/base.apk \
    --lib "$(cygpath -w "$SP/libbunny.so")" --dep "$(cygpath -w "$SP/libshadowhook.so")" \
    --icon "$(cygpath -w "$ROOT/Icon.png")" \
    --out "$(cygpath -w "$ROOT/out/terraria-bunny.apk")" >/dev/null
echo "    out/terraria-bunny.apk"

echo "==> reinstalando"
adb "${D[@]}" uninstall "$PKG" >/dev/null 2>&1 || true
adb "${D[@]}" install "$(cygpath -w "$ROOT/out/terraria-bunny.apk")" >/dev/null

echo "==> provisionando config + mod (samples/HelloMod)"
# Pasta externa do proprio app do jogo: legivel pelo processo do jogo sem root
# (SELinux Enforcing OK) e gravavel via adb push. Igual em emulador e celular.
BASE="/sdcard/Android/data/$PKG/files/bunny"
adb "${D[@]}" shell "mkdir -p $BASE/mods/hellomod" >/dev/null 2>&1
adb "${D[@]}" push "$(cygpath -w "$ROOT/samples/HelloMod/main.js")" "$BASE/mods/hellomod/main.js" >/dev/null
adb "${D[@]}" push "$(cygpath -w "$ROOT/samples/HelloMod/mod.json")" "$BASE/mods/hellomod/mod.json" >/dev/null
adb "${D[@]}" shell "printf 'modsDir=$BASE/mods\nenabledMods=hellomod\nlogPath=$BASE/bunny.log\ngameVersion=301543\n' > $BASE/config" >/dev/null 2>&1
# No MuMu a pasta externa e um ext4 real: arquivos criados pelo adb ficam
# shell:ext_data_rw 0660 e o uid do jogo nao le. Liberamos leitura pra todos.
# (Num celular real, com FUSE, isso e no-op — o app ja le a propria Android/data.)
adb "${D[@]}" shell "chmod -R a+rX $BASE" >/dev/null 2>&1

echo "==> lancando"
adb "${D[@]}" logcat -c
adb "${D[@]}" shell "monkey -p $PKG -c android.intent.category.LAUNCHER 1" >/dev/null 2>&1
echo "    jogo lancado. Acompanhe: adb -s ${ADB_SERIAL:-emulator-5554} logcat -s BunnyLoader:*"
