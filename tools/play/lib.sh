#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."

BUNDLETOOL_VERSION=1.18.3
BUNDLETOOL_SHA256=a099cfa1543f55593bc2ed16a70a7c67fe54b1747bb7301f37fdfd6d91028e29
BUNDLETOOL_JAR="build/bundletool/bundletool-all-$BUNDLETOOL_VERSION.jar"
WORK_DIR=build/play

export JAVA_HOME="${JAVA_HOME:-/c/Program Files/Android/Android Studio/jbr}"
JAVA="$JAVA_HOME/bin/java"
KEYTOOL="$JAVA_HOME/bin/keytool"
SDK_DIR=$(sed -n 's/^sdk.dir=//p' local.properties | tr -d '\r')
NDK_VERSION=$(sed -n 's/^ *ndkVersion = "\(.*\)"/\1/p' app/build.gradle.kts | tr -d '\r')
READELF="$SDK_DIR/ndk/$NDK_VERSION/toolchains/llvm/prebuilt/windows-x86_64/bin/llvm-readelf.exe"

CHANNEL="${1:-beta}"
case "$CHANNEL" in
    beta) BETA_PROPERTY=true; AAB_NAME=bunny-loader-beta.aab; PACKAGE=com.bunnyloader.beta ;;
    stable) BETA_PROPERTY=false; AAB_NAME=bunny-loader.aab; PACKAGE=com.bunnyloader ;;
    *) echo "unknown channel: $CHANNEL (use beta or stable)" >&2; exit 2 ;;
esac
AAB="out/$AAB_NAME"

gradle_bin() {
    if [[ -n "${BL_GRADLE:-}" ]]; then echo "$BL_GRADLE"; return; fi
    echo "$HOME"/.gradle/wrapper/dists/gradle-8.9-bin/*/gradle-8.9/bin/gradle
}

fetch_bundletool() {
    if [[ -f "$BUNDLETOOL_JAR" ]] && sha256sum "$BUNDLETOOL_JAR" | grep -q "^$BUNDLETOOL_SHA256"; then return; fi
    mkdir -p "$(dirname "$BUNDLETOOL_JAR")"
    curl -fsSL -o "$BUNDLETOOL_JAR" \
        "https://github.com/google/bundletool/releases/download/$BUNDLETOOL_VERSION/bundletool-all-$BUNDLETOOL_VERSION.jar"
    if ! sha256sum "$BUNDLETOOL_JAR" | grep -q "^$BUNDLETOOL_SHA256"; then
        rm -f "$BUNDLETOOL_JAR"
        echo "bundletool: SHA-256 mismatch" >&2
        exit 1
    fi
}

bundletool() {
    MSYS_NO_PATHCONV=1 "$JAVA" -jar "$BUNDLETOOL_JAR" "$@"
}
