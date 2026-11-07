#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"

"$(gradle_bin)" :app:bundleRelease -Pbl.play=true -Pbl.beta="$BETA_PROPERTY" --console=plain

mkdir -p out
cp app/build/outputs/bundle/release/app-release.aab "$AAB"
(cd out && sha256sum -b "$AAB_NAME" > "$AAB_NAME.sha256")
echo "==> $AAB"

tools/play/check-aab.sh "$CHANNEL"
