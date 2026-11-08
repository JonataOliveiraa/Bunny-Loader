#!/usr/bin/env bash
# Roda um pacote de teste C# que pede toques reais: o teste escreve
# "<pacote> TAP <n> x y" ou "<pacote> SWIPE <n> x1 y1 x2 y2" em unidades de UI
# e o script toca uma vez por <n>; "<pacote> FTAP <n> x y" toca na fracao da tela
# (0 a 1), FHOLD segura 300 ms; "<pacote> DRAG <n> x1 y1 x2 y2" aperta, arrasta e solta;
# "<pacote> SHOT <n>" so captura a tela.
# UIW = largura da tela em unidades de UI.
#
#   SAMPLE=1 tools/tests/run-taps.sh csemotemenu
set -u
source "$(dirname "$0")/lib.sh"
dev="${DEVICE:-127.0.0.1:16384}"
test="$1"
size=$(adb -s "$dev" shell wm size | grep -o '[0-9]*x[0-9]*' | tail -1 | tr 'x' '\n' | sort -n)
width=$(tail -1 <<< "$size")
height=$(head -1 <<< "$size")

fraction() { awk -v x="$1" -v y="$2" -v w="$width" -v h="$height" 'BEGIN { printf "%d %d", x * w, y * h }'; }

drag() {
    adb -s "$dev" shell input motionevent DOWN "$1" "$2"
    sleep 0.4
    for _ in 1 2 3 4 5 6 7 8; do
        adb -s "$dev" shell input motionevent MOVE "$3" "$4"
        sleep 0.25
    done
    adb -s "$dev" shell input motionevent UP "$3" "$4"
}

pixels() { awk -v x="$1" -v y="$2" -v w="$width" -v ui="${UIW:-1019}" 'BEGIN { printf "%d %d", x * w / ui, y * w / ui }'; }

env=()
[ "${NOENV:-0}" = 1 ] || env=(csenv)
[ "${SAMPLE:-0}" = 1 ] && env+=("$(cd "$here/../.." && (pwd -W 2>/dev/null || pwd))/samples/ExampleModCSharp")
prepare "$dev" 1 "$test" "${env[@]}"
launch "$dev"
wait_mods "$dev" && enter_single "$dev" || exit 1
done=" "
for _ in $(seq 1 "${WAIT:-240}"); do
    log="$(adb -s "$dev" logcat -d | grep -o "$test \(SWIPE\|TAP\|FTAP\|FHOLD\|DRAG\|SHOT\|END\) .*")"
    while read -r _ kind n rest; do
        case "$done" in *" $n "*) continue ;; esac
        set -- $rest
        shot "$dev" "${dev##*:}-$test-$n" >/dev/null
        case "$kind" in
            TAP) adb -s "$dev" shell input tap $(pixels "$1" "$2") ;;
            FTAP) adb -s "$dev" shell input tap $(fraction "$1" "$2") ;;
            FHOLD) set -- $(fraction "$1" "$2"); adb -s "$dev" shell input swipe "$1" "$2" "$1" "$2" 300 ;;
            DRAG) drag $(pixels "$1" "$2") $(pixels "$3" "$4") ;;
            SWIPE) adb -s "$dev" shell input swipe $(pixels "$1" "$2") $(pixels "$3" "$4") 900 ;;
            SHOT) ;;
            *) continue ;;
        esac
        done="$done$n "
    done <<< "$(grep -E " (TAP|FTAP|FHOLD|DRAG|SWIPE|SHOT) " <<< "$log")"
    grep -q " END " <<< "$log" && break
    sleep 1
done
results "$dev" "$test"
adb -s "$dev" logcat -d | grep -qE "$test END pass=[0-9]+ fail=0"
