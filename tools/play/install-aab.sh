#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"

[[ -f "$AAB" ]] || { echo "$AAB not found: run tools/play/build-aab.sh $CHANNEL" >&2; exit 1; }
fetch_bundletool

DEVICE="${BL_DEVICE:-}"
if [[ -z "$DEVICE" ]]; then
    DEVICE=$(adb devices | tr -d '\r' | awk 'NR>1 && $2=="device"{print $1}' | head -1)
fi
[[ -n "$DEVICE" ]] || { echo "no device connected" >&2; exit 1; }

property() { sed -n "s/^$1=//p" keystore.properties | tr -d '\r'; }

ADB=$(cygpath -w "$(command -v adb)")
APKS="$WORK_DIR/install-$CHANNEL.apks"
mkdir -p "$WORK_DIR"

bundletool build-apks --bundle="$AAB" --output="$APKS" --overwrite \
    --connected-device --device-id="$DEVICE" --adb="$ADB" \
    --ks="$(property storeFile)" --ks-pass="pass:$(property storePassword)" \
    --ks-key-alias="$(property keyAlias)" --key-pass="pass:$(property keyPassword)"
bundletool install-apks --apks="$APKS" --device-id="$DEVICE" --adb="$ADB"

adb -s "$DEVICE" shell pm path "$PACKAGE"
adb -s "$DEVICE" shell dumpsys package "$PACKAGE" | grep -E 'versionCode|pkgFlags=' | head -2
