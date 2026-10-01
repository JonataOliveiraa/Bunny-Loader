#!/usr/bin/env bash
# Repete uma rodada do run.sh ate o jogo congelar, para caçar travamento
# intermitente (corrida entre threads). Congelou = o FIM do teste vigiado nao
# aparece no logcat ate 60 s depois de o run.sh voltar (o run.sh pode voltar
# antes, contando o FIM de outros testes). Guarda o logcat da rodada que travou.
#
#   tools/bench/repeat.sh <rodadas> <teste-vigiado> <pasta-de-mod>...
#   tools/bench/repeat.sh 12 modfurniture samples/ExampleMod tools/tests/tilename tools/tests/modfurniture tools/tests/prefix tools/tests/boss
#
# Os pacotes de teste ficam no aparelho entre rodadas: apague os de rodadas
# anteriores antes, ou eles entram na mistura.
set -u
D="${BL_DEVICE:-127.0.0.1:16384}"
ROUNDS="$1"; WATCH="$2"; shift 2
OUT="${TMPDIR:-${TEMP:-.}}"
for k in $(seq 1 "$ROUNDS"); do
    timeout 450 bash "$(dirname "$0")/run.sh" "$OUT/repeat$k.txt" "$@" >/dev/null 2>&1
    ok=0
    for i in $(seq 1 30); do
        if adb -s "$D" logcat -d -s BunnyLoader | grep -aq "$WATCH FIM"; then ok=1; break; fi
        sleep 2
    done
    if [ "$ok" = 0 ]; then
        adb -s "$D" logcat -d > "$OUT/repeat-travou.txt"
        echo "TRAVOU na rodada $k (logcat em $OUT/repeat-travou.txt)"
        exit 1
    fi
    echo "rodada $k ok"
done
echo "nao travou em $ROUNDS rodadas"
