#!/usr/bin/env bash
# Regenera refs/ a partir do Terraria instalado no dispositivo/emulador.
#
#   tools/dump.sh                    # usa o primeiro device do adb
#   ADB_SERIAL=127.0.0.1:16384 tools/dump.sh
#   IL2CPPDUMPER=/c/tools/Il2CppDumper.exe tools/dump.sh
#   tools/dump.sh --skip-pull        # reaproveita refs/base.apk
#
# Nada aqui é versionado: refs/ está no .gitignore.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
REFS="$ROOT/refs"
PKG="${PKG:-com.and.games505.TerrariaPaid}"
ABI="${ABI:-arm64-v8a}"
SKIP_PULL=0
[ "${1:-}" = "--skip-pull" ] && SKIP_PULL=1

# --- localizar o Il2CppDumper ---
if [ -z "${IL2CPPDUMPER:-}" ]; then
    IL2CPPDUMPER="$(find "$USERPROFILE/Downloads" -maxdepth 2 \
        -ipath "*Il2CppDumper-win*" -iname "Il2CppDumper.exe" 2>/dev/null | head -1)"
fi
[ -n "${IL2CPPDUMPER:-}" ] && [ -f "$IL2CPPDUMPER" ] || {
    echo "ERRO: Il2CppDumper.exe nao encontrado. Defina IL2CPPDUMPER=<caminho>." >&2
    exit 1
}

# --- device ---
if [ -z "${ADB_SERIAL:-}" ]; then
    ADB_SERIAL="$(adb devices | awk 'NR>1 && $2=="device" {print $1; exit}')"
fi
[ -n "$ADB_SERIAL" ] || { echo "ERRO: nenhum device no adb." >&2; exit 1; }
D=(-s "$ADB_SERIAL")
echo "==> device: $ADB_SERIAL"

mkdir -p "$REFS"

# --- pull do APK ---
if [ "$SKIP_PULL" -eq 0 ]; then
    APK_PATH="$(adb "${D[@]}" shell pm path "$PKG" | tr -d '\r' | sed 's/^package://' | head -1)"
    [ -n "$APK_PATH" ] || { echo "ERRO: $PKG nao instalado." >&2; exit 1; }
    echo "==> pull: $APK_PATH"
    adb "${D[@]}" pull "$APK_PATH" "$REFS/base.apk"
else
    echo "==> reaproveitando refs/base.apk"
fi
[ -f "$REFS/base.apk" ] || { echo "ERRO: refs/base.apk ausente." >&2; exit 1; }

# --- versão do jogo (registre no mod.json dos mods) ---
adb "${D[@]}" shell dumpsys package "$PKG" 2>/dev/null \
    | grep -E "versionCode|versionName" | head -2 | sed 's/^/    /' || true

# --- extrair alvos do APK ---
echo "==> extraindo libil2cpp.so / libunity.so / global-metadata.dat ($ABI)"
cd "$REFS"
unzip -o -j base.apk \
    "lib/$ABI/libil2cpp.so" \
    "lib/$ABI/libunity.so" \
    "assets/bin/Data/Managed/Metadata/global-metadata.dat" -d . > /dev/null

# sanity: metadata valido comeca com AF 1B B1 FA
MAGIC="$(head -c 4 global-metadata.dat | od -An -tx1 | tr -d ' \n')"
[ "$MAGIC" = "af1bb1fa" ] || { echo "ERRO: global-metadata.dat invalido (magic=$MAGIC)" >&2; exit 1; }
echo "    metadata ok (magic af1bb1fa)"

# --- dump ---
# O "< /dev/null" e proposital: o dumper termina com "Press any key to exit" e
# lanca uma excecao sem console interativo. Isso acontece DEPOIS de gerar tudo.
echo "==> Il2CppDumper"
"$IL2CPPDUMPER" libil2cpp.so global-metadata.dat . < /dev/null 2>&1 \
    | grep -vE "^Unhandled exception|^   at " || true

[ -f "$REFS/dump.cs" ] && [ -f "$REFS/DummyDll/Assembly-CSharp.dll" ] || {
    echo "ERRO: dump incompleto." >&2; exit 1; }

echo
echo "==> pronto:"
echo "    dump.cs                    $(wc -l < dump.cs) linhas"
echo "    DummyDll/Assembly-CSharp.dll"
echo "    il2cpp.h, script.json, stringliteral.json"
echo
echo "Consulte com: tools/dumpgrep.sh Projectile Terraria"
