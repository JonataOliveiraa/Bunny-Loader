#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"

MIN_TARGET_SDK=36
MAX_DOWNLOAD_BYTES=200000000
PAGE_ALIGN=16384

[[ -f "$AAB" ]] || { echo "$AAB not found: run tools/play/build-aab.sh $CHANNEL" >&2; exit 1; }
fetch_bundletool

FAILURES=0
pass() { echo "  ok    $1"; }
fail() { echo "  FAIL  $1"; FAILURES=$((FAILURES + 1)); }

TEMP="$WORK_DIR/check-$CHANNEL"
rm -rf "$TEMP"
mkdir -p "$TEMP"

echo "==> $AAB ($(stat -c %s "$AAB") bytes)"

if bundletool validate --bundle="$AAB" > "$TEMP/validate.txt" 2>&1; then
    pass "bundletool validate"
else
    fail "bundletool validate (see $TEMP/validate.txt)"
fi

manifest() { bundletool dump manifest --bundle="$AAB" --xpath="$1" | tr -d '\r'; }

APP_ID=$(manifest /manifest/@package)
if [[ "$APP_ID" == "$PACKAGE" ]]; then pass "package $APP_ID"; else fail "package $APP_ID, expected $PACKAGE"; fi
echo "        version $(manifest /manifest/@android:versionName) (code $(manifest /manifest/@android:versionCode))"

TARGET=$(manifest /manifest/uses-sdk/@android:targetSdkVersion)
if (( TARGET >= MIN_TARGET_SDK )); then pass "targetSdk $TARGET"; else fail "targetSdk $TARGET, Play requires $MIN_TARGET_SDK"; fi

DEBUGGABLE=$(manifest /manifest/application/@android:debuggable)
if [[ "$DEBUGGABLE" == "true" ]]; then fail "debuggable"; else pass "not debuggable"; fi

CERT=$("$KEYTOOL" -J-Duser.language=en -printcert -jarfile "$AAB" 2>/dev/null | tr -d '\r')
OWNER=$(grep -m1 'Owner:' <<<"$CERT" || true)
if [[ -z "$OWNER" ]]; then
    fail "unsigned"
elif grep -q 'Android Debug' <<<"$OWNER"; then
    fail "debug certificate: $OWNER"
else
    pass "signed by $OWNER"
    echo "        $(grep -m1 'SHA256:' <<<"$CERT" | sed 's/^[[:space:]]*//')"
fi

unzip -qo "$AAB" -d "$TEMP/aab" 'base/lib/arm64-v8a/*'
MISALIGNED=0
for lib in "$TEMP"/aab/base/lib/arm64-v8a/*.so; do
    while read -r align; do
        if (( align < PAGE_ALIGN )); then
            MISALIGNED=$((MISALIGNED + 1))
            echo "        $(basename "$lib"): LOAD align $align"
        fi
    done < <("$READELF" -lW "$lib" | tr -d '\r' | awk '$1=="LOAD"{print strtonum($NF)}')
done
if (( MISALIGNED == 0 )); then pass "16 KB pages"; else fail "16 KB pages"; fi

cat > "$TEMP/device.json" <<EOF
{"supportedAbis":["arm64-v8a"],"supportedLocales":["en-US"],"screenDensity":420,"sdkVersion":34}
EOF
bundletool build-apks --bundle="$AAB" --output="$TEMP/device.apks" --device-spec="$TEMP/device.json" --overwrite
DOWNLOAD=$(bundletool get-size total --apks="$TEMP/device.apks" --device-spec="$TEMP/device.json" --modules=base | tr -d '\r' | tail -1 | cut -d, -f2)
if (( DOWNLOAD <= MAX_DOWNLOAD_BYTES )); then
    pass "base module download $DOWNLOAD bytes (limit $MAX_DOWNLOAD_BYTES)"
else
    fail "base module download $DOWNLOAD bytes, limit is $MAX_DOWNLOAD_BYTES"
fi

unzip -qo "$TEMP/device.apks" -d "$TEMP/apks"
BASE_APK="$TEMP/apks/splits/base-master.apk"
for asset in assets/bin/Data/data.unity3d assets/bin/Data/resources.resource; do
    METHOD=$(unzip -v "$BASE_APK" "$asset" | awk -v a="$asset" '$NF==a{print $2}')
    if [[ "$METHOD" == "Stored" ]]; then pass "$asset stored in base"; else fail "$asset: '${METHOD:-missing}' in base, Unity needs it stored"; fi
done

PACK_APK=$(ls "$TEMP"/apks/asset-slices/play_assets-*.apk 2>/dev/null | head -1 || true)
if [[ -n "$PACK_APK" ]]; then
    PACK_ENTRIES=$(unzip -Z1 "$PACK_APK" | cut -d/ -f1,2 | grep '^assets/' | sort -u | tr '\n' ' ')
    pass "install-time pack: $PACK_ENTRIES($(stat -c %s "$PACK_APK") bytes)"
else
    fail "install-time pack play_assets not found"
fi

rm -rf "$TEMP/aab" "$TEMP/apks"

if (( FAILURES > 0 )); then
    echo "==> $FAILURES check(s) failed: Play would refuse this bundle"
    exit 1
fi
echo "==> All checks passed"
