#!/usr/bin/env bash
# Roda um pacote de teste C# no celular USB, com backup dos mods, personagem e mundo.
#
#   tools/tests/run-phone.sh <pacote> [outro pacote ligado junto]
#   tools/tests/run-phone.sh restore
set -u
export BL_PKG="${BL_PKG:-com.bunnyloader.beta}"
source "$(dirname "$0")/lib.sh"
dev="${DEVICE:-RXCX405QXCA}"
player="${PLAYER:-abc}"
world="${QWORLD:-Limite_Petrificado}"
root="$(cd "$here/../.." && (pwd -W 2>/dev/null || pwd))"
bak="$data/phonebak"

restore() {
    adb -s "$dev" shell am force-stop $pkg
    adb -s "$dev" shell "[ -d $bak ] || exit 0; cp $bak/$player.plr* $data/Players/; cp $bak/$world.wld* $data/Worlds/; for f in mods mod_order settings; do [ -s $bak/\$f.xml ] || { echo empty backup \$f; exit 1; }; cp $bak/\$f.xml /data/local/tmp/\$f.xml; chmod 644 /data/local/tmp/\$f.xml; run-as $pkg cp /data/local/tmp/\$f.xml shared_prefs/\$f.xml; rm -f /data/local/tmp/\$f.xml; [ -n \"\$(run-as $pkg cat shared_prefs/\$f.xml)\" ] || { echo restore failed \$f; exit 1; }; done; rm -rf $bak" || { echo "!! restore failed, backup kept in $bak" >&2; return 1; }
    adb -s "$dev" shell "run-as $pkg rm -f files/dotnet/.stamp"
    for pack in "${@:-}"; do
        [ -n "$pack" ] || continue
        adb -s "$dev" shell "rm -rf $data/bunny_packs/$(uid "$pack")"
    done
    echo "restored $dev"
}

if [ "${1:-}" = restore ]; then shift; restore "$@"; exit 0; fi

test="$1"
adb -s "$dev" shell dumpsys window | grep -q "isKeyguardShowing=false" || { echo "!! unlock the phone first" >&2; exit 1; }
adb -s "$dev" shell am force-stop $pkg
adb -s "$dev" shell "if [ ! -d $bak ]; then mkdir -p $bak; cp $data/Players/$player.plr* $bak/; cp $data/Worlds/$world.wld* $bak/; for f in mods mod_order settings; do run-as $pkg cat shared_prefs/\$f.xml | cat > $bak/\$f.xml; [ -s $bak/\$f.xml ] || exit 1; done; fi" || { echo "!! backup failed, nothing changed" >&2; exit 1; }

if ! adb -s "$dev" shell "run-as $pkg ls files/dotnet/.stamp" >/dev/null 2>&1; then
    adb -s "$dev" shell monkey -p $pkg -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
    sleep 8
    if [ "$(adb -s "$dev" shell dumpsys display | grep -m1 -o "mCurrentOrientation=[0-9]" | cut -d= -f2)" = 0 ]; then adb -s "$dev" shell input tap 538 2118; else adb -s "$dev" shell input tap 1211 842; fi
    sleep 15
    adb -s "$dev" shell am force-stop $pkg
fi
adb -s "$dev" push "$root/app/src/main/csharp/BunnyLoader/bin/Release/net10.0/BunnyLoader.dll" /data/local/tmp/BunnyLoader.dll >/dev/null
adb -s "$dev" shell "chmod 644 /data/local/tmp/BunnyLoader.dll; run-as $pkg cp /data/local/tmp/BunnyLoader.dll files/dotnet/BunnyLoader.dll; rm -f /data/local/tmp/BunnyLoader.dll"

printf '<?xml version='"'"'1.0'"'"' encoding='"'"'utf-8'"'"' standalone='"'"'yes'"'"' ?>\n<map>\n' > "$tmp/mods.xml"
order=""
for pack in "$root/samples/ExampleModCSharp" "$here/$test"; do
    tar --force-local --exclude=bin --exclude=obj --exclude=src -C "$pack" -cf "$tmp/pack.tar" .
    u=$(uid "$pack")
    adb -s "$dev" push "$tmp/pack.tar" /data/local/tmp/blpack.tar >/dev/null 2>&1
    adb -s "$dev" shell "mkdir -p $data/bunny_packs/$u; tar -xf /data/local/tmp/blpack.tar -C $data/bunny_packs/$u 2>/dev/null; rm -f /data/local/tmp/blpack.tar"
    printf '    <boolean name="%s" value="true" />\n' "$u" >> "$tmp/mods.xml"
    order="$order${order:+,}&quot;$u&quot;"
done
printf '</map>\n' >> "$tmp/mods.xml"
printf '<?xml version='"'"'1.0'"'"' encoding='"'"'utf-8'"'"' standalone='"'"'yes'"'"' ?>\n<map>\n    <string name="uids">[%s]</string>\n</map>\n' "$order" > "$tmp/mod_order.xml"
adb -s "$dev" shell "run-as $pkg cat shared_prefs/settings.xml" > "$tmp/settings.xml"
sed -i '/name="quickStart"/d; /name="quickPlayer"/d; /name="quickWorld"/d; /name="enableOnInstall"/d' "$tmp/settings.xml"
sed -i "s#</map>#    <boolean name=\"quickStart\" value=\"true\" />\n    <boolean name=\"enableOnInstall\" value=\"false\" />\n    <string name=\"quickPlayer\">$player.plr</string>\n    <string name=\"quickWorld\">$world.wld</string>\n</map>#" "$tmp/settings.xml"
for f in mods mod_order settings; do
    adb -s "$dev" push "$tmp/$f.xml" /data/local/tmp/$f.xml >/dev/null 2>&1
    adb -s "$dev" shell "chmod 644 /data/local/tmp/$f.xml; run-as $pkg cp /data/local/tmp/$f.xml shared_prefs/$f.xml; rm -f /data/local/tmp/$f.xml"
done

adb -s "$dev" logcat -G 5M >/dev/null 2>&1
adb -s "$dev" logcat -c
adb -s "$dev" shell cmd statusbar collapse >/dev/null 2>&1
adb -s "$dev" shell monkey -p $pkg -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
sleep "${LAUNCH_WAIT:-8}"
adb -s "$dev" shell cmd statusbar collapse >/dev/null 2>&1
orientation=$(adb -s "$dev" shell dumpsys display | grep -m1 -o "mCurrentOrientation=[0-9]" | cut -d= -f2)
if [ "$orientation" = 0 ]; then PLAYX=${PLAYX:-538}; PLAYY=${PLAYY:-2118}; else PLAYX=${PLAYX:-1211}; PLAYY=${PLAYY:-842}; fi
adb -s "$dev" shell input tap "${PLAYX:-1211}" "${PLAYY:-845}"
[ "${NOWAIT:-0}" = 1 ] && exit 0
blog() { adb -s "$dev" shell "logcat -d -s BunnyLoader:I" | tr -d ''; }
ended=0
for _ in $(seq 1 $(( ${WAIT:-420} / 5 ))); do
    sleep 5
    log=$(blog)
    for name in $(echo "$log" | grep -o "$test SHOT [a-z]*" | awk '{print $3}'); do
        case " ${shots_done:-} " in *" $name "*) ;; *) shot "$dev" "phone-$test-$name" >/dev/null; shots_done="${shots_done:-} $name" ;; esac
    done
    if echo "$log" | grep -q " $test END"; then ended=1; break; fi
    if adb -s "$dev" shell "logcat -d -b crash -t 5" | grep -q "com.bunnyloader"; then echo "!! crash"; break; fi
done
[ "$ended" = 1 ] || { echo "!! no '$test END' line; screen: $(shot "$dev" "phone-$test")" >&2; adb -s "$dev" shell "logcat -d -b crash -t 40" | cut -c1-200 | tail -30; }
echo "$log" | grep -E "\[mod\]" | grep -E " $test |threw|Exception|error|Error" | cut -c30-260 | tail -"${TAIL:-90}"
