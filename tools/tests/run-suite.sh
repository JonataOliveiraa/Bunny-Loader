#!/usr/bin/env bash
set -u
[ $# -gt 0 ] || { echo "usage: tools/tests/run-suite.sh <pack>... (DEVICE, BL_PKG and WAIT as in run-cs.sh)" >&2; exit 2; }
here="$(cd "$(dirname "$0")" && pwd)"
dev="${DEVICE:-127.0.0.1:16384}"
mkdir -p "$here/out"
out="${SUITE_OUT:-$here/out/suite-$(date +%Y%m%d-%H%M%S).tsv}"
[ -f "$out" ] || printf 'pack\tresult\tpass\tfail\terrors\tseconds\tdetail\n' > "$out"

dismiss_errors() {
    local seen=0 now xml bounds x1 y1 x2 y2
    while true; do
        sleep 3
        adb -s "$dev" shell dumpsys window 2>/dev/null | grep -q "mCurrentFocus=.*Application \(Error\|Not Responding\)" &&
            adb -s "$dev" shell am broadcast -a android.intent.action.CLOSE_SYSTEM_DIALOGS >/dev/null 2>&1
        now=$(adb -s "$dev" logcat -d 2>/dev/null | grep -c " E BunnyLoader")
        [ "$now" -lt "$seen" ] && seen=0
        [ "$now" -gt "$seen" ] || continue
        xml=$(adb -s "$dev" exec-out uiautomator dump /dev/tty 2>/dev/null)
        echo "$xml" | grep -q "Bunny Loader — error" || continue
        bounds=$(echo "$xml" | tr '>' '\n' | grep 'text="OK"' | grep -o 'bounds="\[[0-9]*,[0-9]*\]\[[0-9]*,[0-9]*\]"' | head -1)
        [ -n "$bounds" ] || continue
        read -r x1 y1 x2 y2 < <(echo "$bounds" | grep -o '[0-9]*' | tr '\n' ' ')
        adb -s "$dev" shell input tap $(( (x1 + x2) / 2 )) $(( (y1 + y2) / 2 ))
        seen=$now
    done
}

dismiss_errors &
watcher=$!
trap 'kill $watcher 2>/dev/null' EXIT

for t in "$@"; do
    manifest="$here/$t/manifest.json"
    sample=0
    grep -qE "SAMPLE=1|ExampleModCSharp|Example Mod" "$manifest" 2>/dev/null && sample=1
    grep -lq '"ExampleMod/' "$here/$t"/src/*.cs 2>/dev/null && sample=1
    noenv=0
    grep -q "NOENV=1" "$manifest" "$here/$t"/src/*.cs 2>/dev/null && noenv=1
    runner=run-cs.sh
    grep -q "run-taps.sh" "$manifest" 2>/dev/null && runner=run-taps.sh
    grep -lqE '" TAP |TAP \{|FTAP|FHOLD| DRAG |SWIPE' "$here/$t"/src/*.cs 2>/dev/null && runner=run-taps.sh
    extra=$(python -I -c "import json,sys; print(' '.join(d.lower() for d in json.load(open(sys.argv[1], encoding='utf-8')).get('dependencies', [])))" "$manifest" 2>/dev/null)
    start=$(date +%s)
    case "$dev" in
        *:*)
            run=$(SAMPLE=$sample NOENV=$noenv EXTRA="$extra" DEVICE="$dev" bash "$here/$runner" "$t" 2>&1)
            if grep -qi "run twice" "$manifest" 2>/dev/null; then
                run=$(SAMPLE=$sample NOENV=$noenv EXTRA="$extra" KEEPSAVES=1 DEVICE="$dev" bash "$here/$runner" "$t" 2>&1)
            fi
            ;;
        *)
            if [ "$runner" != run-cs.sh ] || [ -n "$extra" ]; then
                printf '%s\tskipped\t\t\t\t0\tneeds the emulator\n' "$t" >> "$out"
                printf '%-18s skipped (needs the emulator)\n' "$t"
                continue
            fi
            run=$(PLAYER="${PLAYER:-BagTest}" QWORLD="${QWORLD:-QA_B1_Sonda}" DEVICE="$dev" bash "$here/run-phone.sh" "$t" 2>&1)
            if grep -qi "run twice" "$manifest" 2>/dev/null; then
                run=$(PLAYER="${PLAYER:-BagTest}" QWORLD="${QWORLD:-QA_B1_Sonda}" DEVICE="$dev" bash "$here/run-phone.sh" "$t" 2>&1)
            fi
            ;;
    esac
    secs=$(( $(date +%s) - start ))
    logcat=$(adb -s "$dev" logcat -d 2>/dev/null | tr -d '\r')
    end=$(echo "$logcat" | grep "\[mod\].* $t END" | tail -1)
    pass=$(echo "$end" | grep -o "pass=[0-9]*" | cut -d= -f2)
    fail=$(echo "$end" | grep -o "fail=[0-9]*" | cut -d= -f2)
    fails=$(echo "$logcat" | grep "\[mod\].* $t .*: FAIL" | sed 's/.*\] //' | cut -c1-160 | head -3 | paste -sd '|' -)
    errors=$(echo "$logcat" | grep -c " E BunnyLoader")
    if [ -z "$end" ]; then
        result=noend
        detail=$(echo "$run" | grep "^!!" | head -1 | cut -c1-200)
        crash=$(echo "$logcat" | grep -E "Forwarding signal|FATAL" | head -1 | cut -c1-160)
        detail="${detail}${crash:+ | $crash}"
    elif [ "${fail:-0}" != 0 ] || [ -n "$fails" ]; then
        result=fail
        detail="$fails"
    else
        result=ok
        detail=""
    fi
    printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\n' "$t" "$result" "${pass:-}" "${fail:-}" "$errors" "$secs" "$detail" >> "$out"
    printf '%-18s %-6s pass=%s fail=%s errors=%s %ss %s\n' "$t" "$result" "${pass:-?}" "${fail:-?}" "$errors" "$secs" "$detail"
done
echo "summary: $out"
