#!/usr/bin/env bash
# Rodada multijogador do mpexample: instala o APK de out/ (com "apk"), liga só
# este teste e o Example Mod nos dois aparelhos, sobe o host e o cliente pelos
# toques do mpmodplayer, atende os TAP/UISCALE/SHOT de cada lado e para quando
# os dois logam FIM (ou em 15 min). "cleanup" devolve as preferências e apaga
# o pacote de teste.
#   BL_HOST=127.0.0.1:16384 BL_CLIENT=127.0.0.1:16416 tools/tests/mpexample/run.sh [apk]
#   tools/tests/mpexample/run.sh cleanup
set -u
export MSYS_NO_PATHCONV=1
H="${BL_HOST:-127.0.0.1:16384}"
C="${BL_CLIENT:-127.0.0.1:16416}"
ROOT=$(cd "$(dirname "$0")/../../.." && pwd)
cd "$ROOT"
OUT=build/mpexample
UIDT=c8db5391-2561-479b-b5ee-55a8dce58ccc
EXMOD=31f7d550-a80e-4e5d-9dfa-1b3ea23c2584
PACK=/sdcard/Android/data/com.bunnyloader/bunny_packs/$UIDT
mkdir -p "$OUT"

if [ "${1:-}" = cleanup ]; then
    for d in $H $C; do
        adb -s $d shell am force-stop com.bunnyloader
        BL_PREFS_DIR="$OUT/prefs" BL_ENABLE="$UIDT,$EXMOD" python tools/tests/mpexample/prefs.py restore $d
        adb -s $d shell "su -c 'rm -rf $PACK'"
    done
    adb -s $H forward --remove tcp:7777 2>/dev/null
    exit 0
fi

rm -f "$OUT"/*.png "$OUT"/*.log
if [ "${1:-}" = apk ]; then
    for d in $H $C; do adb -s $d install -r "$(pwd -W)/out/bunny-loader.apk" | tail -1; done
fi
tar --force-local -C tools/tests/mpexample -cf "$OUT/fixture.tar" manifest.json content
for d in $H $C; do
    adb -s $d shell am force-stop com.bunnyloader
    BL_PREFS_DIR="$OUT/prefs" BL_ENABLE="$UIDT,$EXMOD" python tools/tests/mpexample/prefs.py prepare $d
    adb -s $d push "$(pwd -W)/$OUT/fixture.tar" /data/local/tmp/mpexample.tar >/dev/null
    adb -s $d shell "su -c 'rm -rf $PACK'; mkdir -p $PACK && tar -xf /data/local/tmp/mpexample.tar -C $PACK && chmod -R 777 $PACK; rm -f /data/local/tmp/mpexample.tar"
    # O spam do SurfaceFlinger empurra as linhas do teste para fora do buffer padrão.
    adb -s $d logcat -G 16M
done

tap() {
    local focus
    focus=$(adb -s "$1" shell dumpsys activity activities | grep 'ResumedActivity:' || true)
    case "$focus" in *com.bunnyloader*) adb -s "$1" shell input tap "$2" "$3" ;; *) echo "Bunny Loader fora de foco em $1" ;; esac
}
launch() {
    adb -s "$1" shell am force-stop com.bunnyloader
    adb -s "$1" logcat -c
    adb -s "$1" shell am start -n com.bunnyloader/dev.bunnyloader.LauncherActivity >/dev/null
    for i in $(seq 1 40); do
        sleep 1
        adb -s "$1" logcat -d | grep -q 'Displayed com.bunnyloader/dev.bunnyloader.LauncherActivity' && break
    done
    sleep 2
    tap "$1" 800 805
}
launch $H
launch $C
sleep 24
tap $H 808 410; sleep 3
tap $H 997 327; sleep 3
tap $H 932 210; sleep 3
tap $H 996 398; sleep 3
tap $H 1010 695
tap $C 808 410; sleep 3
tap $C 997 327
sleep 12
adb -s $H forward tcp:7777 tcp:7777 >/dev/null
tap $H 590 517
tap $C 700 370; sleep 2
tap $C 996 465
sleep 12
tap $C 590 517

declare -A scale seen fim
scale[host]=1.5702; scale[cliente]=1.5702; seen[host]=0; seen[cliente]=0; fim[host]=0; fim[cliente]=0
end=$((SECONDS + 900))
while [ $SECONDS -lt $end ] && { [ ${fim[host]} = 0 ] || [ ${fim[cliente]} = 0 ]; }; do
    sleep 0.5
    for entry in host:$H cliente:$C; do
        r=${entry%%:*}; d=${entry#*:}
        mapfile -t lines < <(adb -s $d logcat -d -s BunnyLoader 2>/dev/null | grep -a "\[mod\] mpexample $r " | sed "s/.*\[mod\] mpexample $r //" | tr -d '\r')
        n=${#lines[@]}
        while [ ${seen[$r]} -lt $n ]; do
            l="${lines[${seen[$r]}]}"
            seen[$r]=$((seen[$r] + 1))
            set -- $l
            case "$1" in
                UISCALE) scale[$r]=$(python -c "print(1600/float('$2'))") ;;
                TAP)
                    x=$(python -c "print(round(float('$2')*${scale[$r]}))"); y=$(python -c "print(round(float('$3')*${scale[$r]}))")
                    adb -s $d shell input tap $x $y ;;
                SHOT) adb -s $d exec-out screencap -p > "$OUT/$r-$2.png" ;;
                FIM:*) fim[$r]=1 ;;
            esac
        done
    done
done
status=0
for entry in host:$H cliente:$C; do
    r=${entry%%:*}; d=${entry#*:}
    adb -s $d logcat -d > "$OUT/$r.log"
    echo "== $r (segfaults: $(grep -a -c 'Forwarding signal' "$OUT/$r.log"))"
    grep -a "\[mod\] mpexample $r \|error in\|Fatal signal" "$OUT/$r.log" | sed 's/.*\[mod\] mpexample //' | grep -v " TAP \| SHOT \|UISCALE\|fase \|moedas:"
    grep -a -q "mpexample $r FIM: tudo ok" "$OUT/$r.log" || status=1
done
exit $status
