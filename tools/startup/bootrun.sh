#!/usr/bin/env bash
set -u
export MSYS_NO_PATHCONV=1
dev="${DEVICE:-127.0.0.1:16384}"
pkg="${BL_PKG:-com.bunnyloader}"
here="$(cd "$(dirname "$0")" && pwd -W)"
out="$1"; label="$2"; runs="$3"; shift 3
auto=$(python -I -c "import json,sys; print(json.load(open(sys.argv[1],encoding='utf-8'))['uid'])" "$here/../tests/csauto/manifest.json")
mkdir -p "$out"
{
    printf '<?xml version='"'"'1.0'"'"' encoding='"'"'utf-8'"'"' standalone='"'"'yes'"'"' ?>\n<map>\n'
    for u in "$@" "$auto"; do printf '    <boolean name="%s" value="true" />\n' "$u"; done
    printf '</map>\n'
} > "$out/mods.xml"
adb -s "$dev" push "$out/mods.xml" /data/local/tmp/m.xml >/dev/null
adb -s "$dev" shell "chmod 644 /data/local/tmp/m.xml; run-as $pkg cp /data/local/tmp/m.xml shared_prefs/mods.xml; rm -f /data/local/tmp/m.xml"
adb -s "$dev" logcat -G 16M
for r in $(seq 1 "$runs"); do
    adb -s "$dev" shell am force-stop "$pkg"
    sleep 2
    adb -s "$dev" logcat -c
    adb -s "$dev" shell monkey -p "$pkg" -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
    for _ in $(seq 1 30); do
        sleep 1
        adb -s "$dev" logcat -d -s ActivityTaskManager:I | grep -q "Displayed $pkg/dev.bunnyloader.LauncherActivity" && break
    done
    sleep 2
    adb -s "$dev" shell input tap ${TAP:-800 805}
    for _ in $(seq 1 "${WAIT:-400}"); do
        adb -s "$dev" logcat -d -s BunnyLoader:I | grep -q "startup: title on screen" && break
        adb -s "$dev" logcat -d -s CRASH:E libc:F DEBUG:F | grep -qE "Forwarding signal|Fatal signal" && break
        sleep 1
    done
    sleep 2
    adb -s "$dev" logcat -d -v threadtime -s BunnyLoader:V > "$out/$label-$r.log"
    echo "$out/$label-$r.log"
done
