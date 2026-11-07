#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"

KEYSTORE=bunny-upload.keystore
PROPERTIES=keystore-play.properties

if [[ -e "$KEYSTORE" || -e "$PROPERTIES" ]]; then
    echo "$KEYSTORE or $PROPERTIES already exists; nothing changed" >&2
    exit 1
fi

PASSWORD=$(openssl rand -hex 24)
"$KEYTOOL" -genkeypair -noprompt \
    -keystore "$KEYSTORE" -storetype PKCS12 -alias upload \
    -keyalg RSA -keysize 4096 -validity 10950 \
    -dname "CN=Bunny Loader, O=Bunny Loader" \
    -storepass "$PASSWORD" -keypass "$PASSWORD" 2>/dev/null

(umask 077; cat > "$PROPERTIES" <<EOF
storeFile=$KEYSTORE
storePassword=$PASSWORD
keyAlias=upload
keyPassword=$PASSWORD
EOF
)

"$KEYTOOL" -J-Duser.language=en -list -v -keystore "$KEYSTORE" -storepass "$PASSWORD" 2>/dev/null | grep -E 'Owner:|SHA256:' | head -2
echo "Created $KEYSTORE and $PROPERTIES. Back up both outside this PC."
