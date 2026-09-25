#!/usr/bin/env bash
# Um ciclo de medicao no MuMu: instala os mods pedidos, abre o jogo, entra no
# PRIMEIRO mundo da lista e salva as linhas de resultado.
#
#   tools/bench/run.sh <saida.txt> <pasta-de-mod>...
#   tools/bench/run.sh /tmp/r.txt tools/bench tools/tests/wrappers tools/tests/nullable
#
# Pre-condicoes: APK ja instalado; existe ao menos um mundo e o personagem de
# teste ("Bench") e o primeiro da lista. NAO crie mundo com estes mods
# instalados: hook JS na thread principal + mods de exemplo e a combinacao do
# crash em aberto (docs/AVALIACAO-PONTE-E-CRASH.md).
set -euo pipefail
D="${BL_DEVICE:-127.0.0.1:16384}"
OUT="$1"; shift
export MSYS_NO_PATHCONV=1
uid() { python -c "import json,sys; print(json.load(open(sys.argv[1]))['uid'])" "$1/manifest.json"; }

adb -s "$D" shell am force-stop com.bunnyloader
# Poderes do Mod Menu ficam salvos (Imortal, Sem inimigos...): um teste de
# dano ou de spawn falharia pelo poder que a sessao anterior deixou ligado.
adb -s "$D" shell "run-as com.bunnyloader rm -f shared_prefs/bunny_powers.xml" >/dev/null 2>&1 || true
# Os mods moram em Android/data/com.bunnyloader/bunny_packs/<uid>. Vao num
# .tar extraido la dentro: o `adb push` de uma pasta com subpastas falha nesse
# caminho no MuMu ("secure_mkdirs failed"), e so a raiz chega. Arquivo posto
# pelo shell precisa do chmod: sem ele o app le, mas nao consegue apagar nem
# sobrescrever (o mesmo problema dos saves).
PACKS=/sdcard/Android/data/com.bunnyloader/bunny_packs
for dir in "$@"; do
    u=$(uid "$dir")
    tar -C "$dir" -cf blmod.tar .
    adb -s "$D" push blmod.tar /data/local/tmp/blmod.tar >/dev/null
    rm -f blmod.tar
    # A pasta que o launcher recopiou (app atualizado) e do app, modo 0770: o
    # shell nao apaga. Com su (o host do MuMu), apaga como root.
    adb -s "$D" shell "rm -rf $PACKS/$u 2>/dev/null || su -c 'rm -rf $PACKS/$u'" >/dev/null 2>&1
    adb -s "$D" shell "mkdir -p $PACKS/$u && tar -xf /data/local/tmp/blmod.tar -C $PACKS/$u && chmod -R 777 $PACKS/$u; rm -f /data/local/tmp/blmod.tar"
done
adb -s "$D" logcat -c
adb -s "$D" shell monkey -p com.bunnyloader -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
# O launcher pode levar bem mais que 4 s (JIT frio depois de instalar): tocar
# antes fazia o toque seguinte cair num card de mod. Espera ele aparecer.
for i in $(seq 1 30); do
    sleep 1
    adb -s "$D" logcat -d | grep -q "Displayed com.bunnyloader/dev.bunnyloader.LauncherActivity" && break
done
sleep 2;  adb -s "$D" shell input tap 800 805     # JOGAR (launcher)
sleep 28; adb -s "$D" shell input tap 808 330     # Um Jogador
sleep 3;  adb -s "$D" shell input tap 997 327     # Jogar (personagem)
sleep 3;  adb -s "$D" shell input tap 997 318     # Jogar (primeiro mundo)
sleep 22; adb -s "$D" shell input tap 590 517     # "Mais tarde" (aviso de controles)
# O benchmark roda no primeiro quadro dentro do mundo e ainda mede 300 quadros.
# Espera o FIM de CADA teste instalado (os de tools/tests/), nao so o
# primeiro: um termina ao carregar, outro so depois de 300 quadros no mundo.
expected=0
for dir in "$@"; do case "$dir" in *tests/*) expected=$((expected + 1)) ;; esac; done
[ "$expected" -gt 0 ] || expected=1
for i in $(seq 1 60); do
    sleep 2
    got=$(adb -s "$D" logcat -d -s BunnyLoader | grep -c "bench quadro\|moditems FIM\|modsave FIM\|projeteis FIM\|npcs FIM\|hooks FIM\|exmod1 FIM\|extrafields FIM\|exmod2 FIM\|recipes FIM\|buffs FIM\|files FIM\|modplayer FIM\|refs FIM\|tiles FIM\|tilesave FIM\|hookslots FIM" || true)
    [ "$got" -ge "$expected" ] && break
done
sleep 4
adb -s "$D" logcat -d -s BunnyLoader | grep -a "\[mod\] \(bench\|wrappers\|nullable\|moditems\|modsave\|projeteis\|npcs\|hooks\|exmod1\|extrafields\|exmod2\|recipes\|buffs\|files\|modplayer\|refs\|tiles\|tilesave\|hookslots\)" \
    | sed 's/.*\[mod\] //' > "$OUT"
echo "segfaults: $(adb -s "$D" logcat -d | grep -a -c 'Forwarding signal')" >> "$OUT"
cat "$OUT"
