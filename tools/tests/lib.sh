export MSYS_NO_PATHCONV=1
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && (pwd -W 2>/dev/null || pwd))"
pkg=com.bunnyloader
data=/sdcard/Android/data/$pkg
shots="$here/out"
tmpdir="$(mktemp -d)"
trap 'rm -rf "$tmpdir"' EXIT
tmp="$(cygpath -m "$tmpdir" 2>/dev/null || echo "$tmpdir")"

pack_dir() { case "$1" in /*|?:*) echo "$1" ;; *) echo "$here/$1" ;; esac; }

uid() { python -I -c "import json,sys; print(json.load(open(sys.argv[1], encoding='utf-8'))['uid'])" "$(pack_dir "$1")/manifest.json"; }

push() {
    local dev="$1" dir u
    dir="$(pack_dir "$2")"
    u=$(uid "$dir")
    tar --force-local -C "$dir" -cf "$tmp/pack.tar" .
    adb -s "$dev" push "$tmp/pack.tar" /data/local/tmp/blpack.tar >/dev/null
    adb -s "$dev" shell "rm -rf $data/bunny_packs/$u 2>/dev/null || su -c 'rm -rf $data/bunny_packs/$u'; mkdir -p $data/bunny_packs/$u && tar -xf /data/local/tmp/blpack.tar -C $data/bunny_packs/$u && chmod -R 777 $data/bunny_packs/$u; rm -f /data/local/tmp/blpack.tar"
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

restore_saves() {
    adb -s "$1" shell "su -c 'rm -f $data/Players/BagTest.plr* $data/Worlds/QA_B1_Sonda.wld* && cp /data/local/tmp/plrbak/* $data/Players/ && cp /data/local/tmp/${WORLD:-wldflat}/* $data/Worlds/ && chmod 666 $data/Players/BagTest.plr* $data/Worlds/QA_B1_Sonda.wld*'"
}

prepare() {
    local dev="$1" saves="$2" u
    shift 2
    [ -f "$here/csauto/content/CsAuto.dll" ] || bash "$here/build-cs.sh" csauto >/dev/null
    adb -s "$dev" shell am force-stop $pkg
    adb -s "$dev" shell "run-as $pkg rm -f shared_prefs/bunny_powers.xml" >/dev/null 2>&1
    [ "$saves" = 1 ] && [ "${KEEPSAVES:-0}" != 1 ] && restore_saves "$dev"
    printf '<?xml version='"'"'1.0'"'"' encoding='"'"'utf-8'"'"' standalone='"'"'yes'"'"' ?>\n<map>\n' > "$tmp/mods.xml"
    for pack in "$@" csauto; do
        u=$(push "$dev" "$pack")
        printf '    <boolean name="%s" value="true" />\n' "$u" >> "$tmp/mods.xml"
    done
    printf '</map>\n' >> "$tmp/mods.xml"
    adb -s "$dev" shell "run-as $pkg cat shared_prefs/settings.xml" > "$tmp/settings.xml"
    pref "$tmp/settings.xml" enableOnInstall false
    pref "$tmp/settings.xml" quickJs "${QJS:-false}"
    adb -s "$dev" push "$tmp/mods.xml" /data/local/tmp/m.xml >/dev/null
    adb -s "$dev" push "$tmp/settings.xml" /data/local/tmp/s.xml >/dev/null
    adb -s "$dev" shell "chmod 644 /data/local/tmp/m.xml /data/local/tmp/s.xml; run-as $pkg cp /data/local/tmp/m.xml shared_prefs/mods.xml; run-as $pkg cp /data/local/tmp/s.xml shared_prefs/settings.xml; rm -f /data/local/tmp/m.xml /data/local/tmp/s.xml"
    adb -s "$dev" logcat -G 16M
    adb -s "$dev" logcat -c
}

launch() {
    local dev="$1"
    adb -s "$dev" shell monkey -p $pkg -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
    for _ in $(seq 1 30); do
        sleep 1
        adb -s "$dev" logcat -d | grep -q "Displayed $pkg/dev.bunnyloader.LauncherActivity" && break
    done
    sleep 2
    adb -s "$dev" shell input tap 800 805
}

state() { adb -s "$1" logcat -d | grep "csauto state" | tail -1 | sed 's/.*csauto state //'; }

shot() {
    mkdir -p "$shots"
    adb -s "$1" exec-out screencap -p > "$shots/$2.png"
    echo "$shots/$2.png"
}

fail() {
    local dev="$1" name="$2" why="$3"
    echo "!! $dev $why (state: $(state "$dev")); screen: $(shot "$dev" "${dev##*:}-$name")" >&2
    return 1
}

wait_state() {
    local dev="$1" pattern="$2" seconds="${3:-60}" name="${4:-wait}"
    for _ in $(seq 1 "$seconds"); do
        state "$dev" | grep -Eq "$pattern" && return 0
        sleep 1
    done
    fail "$dev" "$name" "timed out waiting for '$pattern'"
}

tap_until() {
    local dev="$1" x="$2" y="$3" pattern="$4" name="$5" tries="${6:-4}"
    for _ in $(seq 1 "$tries"); do
        adb -s "$dev" shell input tap "$x" "$y"
        for _ in $(seq 1 6); do
            sleep 1
            state "$dev" | grep -Eq "$pattern" && return 0
        done
    done
    fail "$dev" "$name" "the tap at $x,$y did not reach '$pattern'"
}

enter_single() {
    local dev="$1"
    wait_state "$dev" "mode=0 " 90 title &&
        tap_until "$dev" 808 330 "mode=1 " players &&
        tap_until "$dev" 997 327 "mode=6 " worlds &&
        tap_until "$dev" 997 318 "mode=10 |^world" loading &&
        wait_state "$dev" "^world" 120 world
}

enter_host() {
    local dev="$1"
    wait_state "$dev" "mode=0 " 90 title &&
        tap_until "$dev" 808 410 "mode=1 " players &&
        tap_until "$dev" 997 327 "mode=446 " lobby &&
        adb -s "$dev" shell input tap 932 210 &&
        tap_until "$dev" 996 398 "mode=448 " host &&
        tap_until "$dev" 1010 695 "mode=10 |^world" loading &&
        wait_state "$dev" "^world net=3" 120 world &&
        adb -s "$dev" forward tcp:7777 tcp:7777 >/dev/null
}

enter_client() {
    local dev="$1"
    wait_state "$dev" "mode=0 " 90 title &&
        tap_until "$dev" 808 410 "mode=1 " players &&
        tap_until "$dev" 997 327 "mode=446 " lobby &&
        adb -s "$dev" shell input tap 800 380 &&
        tap_until "$dev" 996 465 "mode=14 |^world" connecting &&
        wait_state "$dev" "^world net=1" 120 world &&
        sleep 3 &&
        adb -s "$dev" shell input tap 550 532
}

wait_end() {
    local dev="$1" test="$2" seconds="${3:-180}"
    for _ in $(seq 1 "$seconds"); do
        adb -s "$dev" logcat -d | grep -q "\[mod\].* $test END" && return 0
        adb -s "$dev" logcat -d | grep -qE "Forwarding signal|FATAL" && break
        sleep 1
    done
    fail "$dev" "$test-end" "no '$test END' line"
}

results() {
    local dev="$1" test="$2"
    echo "==> $test on $dev $(adb -s "$dev" logcat -d | grep -o '([0-9]* habilitados)' | tail -1)"
    adb -s "$dev" logcat -d | grep -E "BunnyLoader: \[mod\]" | grep -E " $test END( |$)|: FAIL|threw|error" | cut -c50-300 | tail -8
    adb -s "$dev" logcat -d | grep -E "Forwarding signal|FATAL" | head -3
}
