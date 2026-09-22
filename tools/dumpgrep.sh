#!/usr/bin/env bash
# Extrai o bloco de uma classe/struct/enum do refs/dump.cs.
#
#   tools/dumpgrep.sh Projectile            # qualquer namespace
#   tools/dumpgrep.sh Entity Terraria       # namespace exato
#   tools/dumpgrep.sh ItemID Terraria.ID | grep -i minishark
set -euo pipefail

NAME="${1:?uso: dumpgrep.sh <Nome> [Namespace]}"
WANT_NS="${2:-}"
DUMP="$(cd "$(dirname "$0")/.." && pwd)/refs/dump.cs"

[ -f "$DUMP" ] || { echo "refs/dump.cs nao existe — rode o Il2CppDumper (ver refs/README.md)" >&2; exit 1; }

awk -v name="$NAME" -v want="$WANT_NS" '
  /^\/\/ Namespace:/ { ns = (NF >= 3 ? $3 : "<global>"); next }
  !inblock && $0 ~ ("(class|struct|enum|interface) " name "([ :<]|$)") {
      if (want == "" || ns == want) {
          inblock = 1
          print "// Namespace: " ns
          print
      }
      next
  }
  inblock { print; if ($0 ~ /^}/) { inblock = 0; print "" } }
' "$DUMP"
