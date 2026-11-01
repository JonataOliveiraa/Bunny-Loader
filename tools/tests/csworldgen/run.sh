#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/../lib.sh"
dev="${DEVICE:-127.0.0.1:16416}"
prepare "$dev" 1 csworldgen
launch "$dev"
wait_mods "$dev"
wait_state "$dev" "mode=0 " 120 title
tap_until "$dev" 808 330 "mode=1 " players
adb -s "$dev" shell input tap 997 327
wait_end "$dev" csworldgen "${WAIT:-900}"
results "$dev" csworldgen || true
log="$(adb -s "$dev" logcat -d)"
grep -qE 'csworldgen END pass=[0-9]+ fail=0' <<< "$log"
