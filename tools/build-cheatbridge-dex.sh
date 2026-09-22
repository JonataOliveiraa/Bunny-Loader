#!/usr/bin/env bash
# Compila tools/cheatbridge/bunny/CheatBridge.java -> dex -> header C embutido.
# Rode quando mudar o CheatBridge.java. Gera:
#   app/src/main/cpp/ui/CheatBridgeDex.h  (bytes do classes.dex)
#
#   tools/build-cheatbridge-dex.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
export MSYS_NO_PATHCONV=1
JAVA_HOME="${JAVA_HOME:-/c/Program Files/Android/Android Studio/jbr}"
SDK="${ANDROID_SDK:-$LOCALAPPDATA/Android/Sdk}"
AJAR="$SDK/platforms/android-34/android.jar"
D8JAR="$SDK/build-tools/36.1.0/lib/d8.jar"
SRC="tools/cheatbridge/bunny/CheatBridge.java"
OUT=".cheatbridge"

echo "==> javac"
rm -rf "$OUT"; mkdir -p "$OUT/classes"
"$JAVA_HOME/bin/javac" -source 8 -target 8 -nowarn \
    -classpath "$(cygpath -w "$AJAR")" \
    -d "$(cygpath -w "$OUT/classes")" "$(cygpath -w "$SRC")"

echo "==> d8 (min-api 26)"
CLASSES=$(find "$OUT/classes" -name "*.class" | while read -r f; do cygpath -w "$f"; done)
"$JAVA_HOME/bin/java" -cp "$(cygpath -w "$D8JAR")" com.android.tools.r8.D8 \
    --release --min-api 26 --lib "$(cygpath -w "$AJAR")" \
    --output "$(cygpath -w "$OUT")" $CLASSES

echo "==> header embutido"
python tools/bin2header.py "$OUT/classes.dex" bl_cheatbridge_dex \
    > app/src/main/cpp/ui/CheatBridgeDex.h
echo "    app/src/main/cpp/ui/CheatBridgeDex.h ($(wc -c < "$OUT/classes.dex") bytes de dex)"
