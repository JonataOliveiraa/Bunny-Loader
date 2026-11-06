#!/usr/bin/env bash
set -u
source "$(dirname "$0")/lib.sh"
dev="${DEVICE:-127.0.0.1:16384}"

run() {
    local test="$1" env=()
    [ "${NOENV:-0}" = 1 ] || env=(csenv)
    [ "${SAMPLE:-0}" = 1 ] && env+=("$(cd "$here/../.." && (pwd -W 2>/dev/null || pwd))/samples/ExampleModCSharp")
    prepare "$dev" 1 "$test" "${env[@]}"
    launch "$dev"
    wait_mods "$dev" && enter_single "$dev" && wait_end "$dev" "$test" "${WAIT:-180}"
    results "$dev" "$test"
}

for t in "$@"; do run "$t"; done
