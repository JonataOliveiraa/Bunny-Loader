#!/usr/bin/env bash
set -u
export MSYS_NO_PATHCONV=1
here="$(cd "$(dirname "$0")" && (pwd -W 2>/dev/null || pwd))"
dev="${DEVICE:-127.0.0.1:16384}"
adb="adb -s $dev"
pkg=com.bunnyloader
data=/sdcard/Android/data/$pkg
tmpdir="$(mktemp -d)"
trap 'rm -rf "$tmpdir"' EXIT
tmp="$(cygpath -m "$tmpdir" 2>/dev/null || echo "$tmpdir")"

uid() { python -I -c "import json,sys; print(json.load(open(sys.argv[1], encoding='utf-8'))['uid'])" "$1/manifest.json"; }

push() {
    local dir="$1" u
    u=$(uid "$dir")
    tar --force-local -C "$dir" -cf "$tmp/pack.tar" .
    $adb push "$tmp/pack.tar" /data/local/tmp/blpack.tar >/dev/null
    $adb shell "rm -rf $data/bunny_packs/$u 2>/dev/null || su -c 'rm -rf $data/bunny_packs/$u'; mkdir -p $data/bunny_packs/$u && tar -xf /data/local/tmp/blpack.tar -C $data/bunny_packs/$u && chmod -R 777 $data/bunny_packs/$u; rm -f /data/local/tmp/blpack.tar"
    echo "$u"
}

pref() {
    local file="$1" name="$2" value="$3"
    if grep -q "name=\"$name\"" "$file"; then
        sed -i "s#<boolean name=\"$name\" value=\"[a-z]*\" />#<boolean name=\"$name\" value=\"$value\" />#" "$file"
    else
        sed -i "s#</map>#    <boolean name=\"$name\" value=\"$value\" />\n</map>#" "$file"
    fi
}

run() {
    local test="$1" uids=() u
    $adb shell am force-stop $pkg
    $adb shell "run-as $pkg rm -f shared_prefs/bunny_powers.xml" >/dev/null 2>&1
    if [ "${KEEPSAVES:-0}" != 1 ]; then
        $adb shell "su -c 'rm -f $data/Players/BagTest.plr* $data/Worlds/QA_B1_Sonda.wld* && cp /data/local/tmp/plrbak/* $data/Players/ && cp /data/local/tmp/${WORLD:-wldflat}/* $data/Worlds/ && chmod 666 $data/Players/BagTest.plr* $data/Worlds/QA_B1_Sonda.wld*'"
    fi
    uids+=("$(push "$here/$test")")
    [ "${NOENV:-0}" = 1 ] || uids+=("$(push "$here/csenv")")

    printf '<?xml version='"'"'1.0'"'"' encoding='"'"'utf-8'"'"' standalone='"'"'yes'"'"' ?>\n<map>\n' > "$tmp/mods.xml"
    for u in "${uids[@]}"; do printf '    <boolean name="%s" value="true" />\n' "$u" >> "$tmp/mods.xml"; done
    printf '</map>\n' >> "$tmp/mods.xml"
    $adb shell "run-as $pkg cat shared_prefs/settings.xml" > "$tmp/settings.xml"
    pref "$tmp/settings.xml" enableOnInstall false
    pref "$tmp/settings.xml" quickJs "${QJS:-false}"
    $adb push "$tmp/mods.xml" /data/local/tmp/m.xml >/dev/null
    $adb push "$tmp/settings.xml" /data/local/tmp/s.xml >/dev/null
    $adb shell "chmod 644 /data/local/tmp/m.xml /data/local/tmp/s.xml; run-as $pkg cp /data/local/tmp/m.xml shared_prefs/mods.xml; run-as $pkg cp /data/local/tmp/s.xml shared_prefs/settings.xml; rm -f /data/local/tmp/m.xml /data/local/tmp/s.xml"

    $adb logcat -G 16M
    $adb logcat -c
    $adb shell monkey -p $pkg -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
    for _ in $(seq 1 30); do sleep 1; $adb logcat -d | grep -q "Displayed $pkg/dev.bunnyloader.LauncherActivity" && break; done
    sleep 2; $adb shell input tap 800 805; sleep 30
    $adb shell input tap 808 330; sleep 3; $adb shell input tap 997 327; sleep 3; $adb shell input tap 997 318
    sleep "${WAIT:-45}"
    echo "==> $test $($adb logcat -d | grep -o '([0-9]* habilitados)' | tail -1)"
    $adb logcat -d | grep -E "BunnyLoader: \[mod\]" | grep -E " END( |$)|: FAIL|threw|error" | cut -c50-300 | tail -8
    $adb logcat -d | grep -E "Forwarding signal|FATAL" | head -3
}

for t in "$@"; do run "$t"; done
