#!/usr/bin/env bash
# Ciclo curto de interface: compila, instala, abre e tira print.
#
#   tools/ui.sh              # abre o launcher e printa
#   tools/ui.sh out.png      # salva o print onde voce quiser
#
# Por que existe: o APK completo tem ~190 MB, quase tudo libil2cpp.so e
# libunity.so, que nunca mudam enquanto se mexe em tela. Com -Pbl.uiOnly=true
# elas ficam de fora, o pacote cai para ~11 MB e o ciclo vai de ~55s para ~6s.
#
# O JOGAR nao funciona nesse modo (a lib do jogo nao esta no pacote). Para
# voltar a jogar:
#
#   gradle :app:installDebug -Pbl.assetsInApk=true
#
# Se o ciclo comecar a demorar de novo sem motivo, apague o zip: o
# empacotamento incremental do AGP acumula lixo morto dentro do APK e ele volta
# a pesar 185 MB com 11 MB de conteudo.
#
#   rm -rf app/build/intermediates/apk app/build/outputs/apk
set -euo pipefail
cd "$(dirname "$0")/.."

DEVICE="${BL_DEVICE:-127.0.0.1:16384}"   # MuMu; exporte BL_DEVICE para trocar
SHOT="${1:-}"
PKG=com.bunnyloader

GRADLE="${BL_GRADLE:-}"
if [[ -z "$GRADLE" ]]; then
    GRADLE=$(echo "$HOME"/.gradle/wrapper/dists/gradle-*/*/gradle-*/bin/gradle | tr ' ' '\n' | tail -1)
fi

# Se o APK passou de 40 MB no modo UI, so pode ser lixo do zip incremental.
APK=app/build/outputs/apk/debug/app-debug.apk
if [[ -f "$APK" ]] && [[ $(stat -c %s "$APK") -gt 41943040 ]]; then
    echo "==> APK inchado; refazendo o zip do zero"
    rm -rf app/build/intermediates/apk app/build/outputs/apk
fi

echo "==> build + install ($DEVICE)"
ANDROID_SERIAL="$DEVICE" "$GRADLE" :app:installDebug -Pbl.uiOnly=true --console=plain -q

echo "==> abrindo"
adb -s "$DEVICE" shell am force-stop "$PKG"
adb -s "$DEVICE" shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
sleep 5

if [[ -n "$SHOT" ]]; then
    adb -s "$DEVICE" exec-out screencap -p > "$SHOT"
    echo "==> print em $SHOT"
fi
