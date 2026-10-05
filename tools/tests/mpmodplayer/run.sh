#!/usr/bin/env bash
set -euo pipefail
export MSYS_NO_PATHCONV=1
H="${BL_HOST:-127.0.0.1:16384}"
C="${BL_CLIENT:-127.0.0.1:16416}"
ROOT=$(cd "$(dirname "$0")/../../.." && pwd)
cd "$ROOT"
FIXTURE=tools/tests/mpmodplayer
OUT=build/mpmodplayer
UID_PACK=aba116f6-01ea-4975-9443-a3529782cb91
PACK=/sdcard/Android/data/com.bunnyloader/bunny_packs/$UID_PACK
mkdir -p "$OUT"
tap() {
    local focus
    focus=$(adb -s "$1" shell dumpsys activity activities | rg 'ResumedActivity:' || true)
    [[ "$focus" == *com.bunnyloader* ]] || { echo "Bunny Loader fora de foco em $1" >&2; exit 1; }
    adb -s "$1" shell input tap "$2" "$3"
}
launch() {
    adb -s "$1" shell am force-stop com.bunnyloader
    adb -s "$1" logcat -c
    adb -s "$1" shell am start -n com.bunnyloader/dev.bunnyloader.LauncherActivity >/dev/null
    local shown=0
    for i in {1..40}; do
        sleep 1
        adb -s "$1" logcat -d > "$OUT/launch.log"
        if rg -q 'Displayed com.bunnyloader/dev.bunnyloader.LauncherActivity' "$OUT/launch.log"; then shown=1; break; fi
    done
    [[ "$shown" == 1 ]] || { echo "Launcher nao abriu em $1" >&2; exit 1; }
    sleep 2
    tap "$1" 800 805
}
case "${1:-start}" in
    install)
        tar -C "$FIXTURE" -cf "$OUT/fixture.tar" manifest.json content
        for d in "$H" "$C"; do
            adb -s "$d" shell am force-stop com.bunnyloader
            python tools/tests/mpmodplayer/prefs.py prepare "$d"
            adb -s "$d" push "$OUT/fixture.tar" /data/local/tmp/mpmodplayer.tar >/dev/null
            adb -s "$d" shell "mkdir -p $PACK && tar -xf /data/local/tmp/mpmodplayer.tar -C $PACK && chmod -R 777 $PACK"
        done
        ;;
    start)
        launch "$H"
        launch "$C"
        sleep 24
        tap "$H" 808 410; sleep 3
        tap "$H" 997 327; sleep 3
        tap "$H" 932 210; sleep 3
        tap "$H" 996 398; sleep 3
        tap "$H" 1010 695
        tap "$C" 808 410; sleep 3
        tap "$C" 997 327
        sleep 12
        adb -s "$H" forward tcp:7777 tcp:7777 >/dev/null
        tap "$H" 590 517
        tap "$C" 700 370; sleep 2
        tap "$C" 996 465
        sleep 12
        tap "$C" 590 517
        ;;
    rejoin)
        launch "$C"
        sleep 24
        tap "$C" 808 410; sleep 3
        tap "$C" 997 327; sleep 3
        tap "$C" 700 370; sleep 2
        tap "$C" 996 465
        sleep 12
        tap "$C" 590 517
        ;;
    disconnect)
        adb -s "$C" logcat -d > "$OUT/cliente-first.log"
        rg -q 'mpmodplayer cliente FIM falhas=0' "$OUT/cliente-first.log"
        client_line=$(rg 'mpmodplayer cliente INICIO jogador=' "$OUT/cliente-first.log")
        [[ "$client_line" =~ jogador=([0-9]+) ]] || exit 1
        client_index=${BASH_REMATCH[1]}
        adb -s "$C" shell am force-stop com.bunnyloader
        disconnected=0
        for i in {1..120}; do
            sleep 1
            adb -s "$H" logcat -d > "$OUT/disconnect.log"
            if rg -q "mpmodplayer host DESCONECTOU jogador=$client_index\b" "$OUT/disconnect.log"; then disconnected=1; break; fi
        done
        [[ "$disconnected" == 1 ]] || { echo 'Host nao notificou a desconexao em 120 s' >&2; exit 1; }
        ;;
    collect)
        for entry in "host:$H" "cliente:$C"; do
            role=${entry%%:*}; device=${entry#*:}
            adb -s "$device" logcat -d > "$OUT/$role.log"
            rg 'mpmodplayer|JS.*Error|Fatal signal' "$OUT/$role.log" || true
        done
        ;;
    verify)
        for file in host.log cliente-first.log cliente.log; do
            if rg -q 'mpmodplayer .*FALHOU|Fatal signal' "$OUT/$file"; then exit 1; fi
        done
        rg -q 'mpmodplayer host FIM falhas=0' "$OUT/host.log"
        rg -q 'mpmodplayer cliente FIM falhas=0' "$OUT/cliente-first.log"
        rg -q 'mpmodplayer host REJOIN FIM falhas=0' "$OUT/host.log"
        rg -q 'mpmodplayer cliente REJOIN FIM' "$OUT/cliente.log"
        ;;
    cleanup)
        for d in "$H" "$C"; do
            adb -s "$d" shell am force-stop com.bunnyloader
            python tools/tests/mpmodplayer/prefs.py restore "$d"
            adb -s "$d" shell "su -c 'rm -rf $PACK'; rm -f /data/local/tmp/mpmodplayer.tar"
        done
        adb -s "$H" forward --remove tcp:7777
        ;;
    *) echo 'Uso: run.sh install|start|disconnect|rejoin|collect|verify|cleanup' >&2; exit 2 ;;
esac
