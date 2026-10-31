#!/usr/bin/env bash
set -u
source "$(dirname "$0")/lib.sh"
host="${HOST:-127.0.0.1:16384}"
client="${CLIENT:-127.0.0.1:16416}"
adb connect "$client" >/dev/null 2>&1

run() {
    local test="$1" env=()
    [ "${NOENV:-0}" = 1 ] || env=(csenv)
    prepare "$host" 1 "$test" "${env[@]}"
    prepare "$client" 0 "$test" "${env[@]}"
    launch "$host"
    launch "$client"
    enter_host "$host" || { results "$host" "$test"; return 1; }
    enter_client "$client" || { results "$client" "$test"; return 1; }
    wait_state "$host" "players=2" 60 joined
    wait_end "$host" "$test" "${WAIT:-180}"
    wait_end "$client" "$test" "${WAIT:-180}"
    results "$host" "$test"
    results "$client" "$test"
    [ "${KEEP:-0}" = 1 ] || { adb -s "$client" shell am force-stop $pkg; adb -s "$host" shell am force-stop $pkg; }
}

for t in "$@"; do run "$t"; done
