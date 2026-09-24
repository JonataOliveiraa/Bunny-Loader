#!/usr/bin/env bash
# Cria mundos em sequencia e conta crashes.
#   trials.sh <pacote> <n> <log>
# Pre-condicao: o jogo esta na tela "Mundo Novo" (primeira tentativa) com o
# tamanho ja escolhido. Entre tentativas ele volta a "Selecionar Mundo" e
# tocamos em Novo.
D=127.0.0.1:16384
PKG=$1; N=$2; LOG=$3
export MSYS_NO_PATHCONV=1
count() { adb -s $D shell "ls /sdcard/Android/data/$PKG/Worlds/ 2>/dev/null | grep -c wld"; }
: > "$LOG"
for t in $(seq 1 "$N"); do
    n0=$(count)
    adb -s $D logcat -c
    if [ "$t" -gt 1 ]; then adb -s $D shell input tap 1010 811; sleep 2; fi
    start=$(date +%s)
    adb -s $D shell input tap 1020 682
    result=TIMEOUT
    for i in $(seq 1 80); do
        sleep 3
        if adb -s $D logcat -d | grep -q "Forwarding signal"; then
            result=CRASH
            adb -s $D logcat -d > "${LOG%.txt}_crash$t.txt"
            break
        fi
        if [ "$(count)" -gt "$n0" ]; then result=OK; break; fi
    done
    echo "tentativa $t: $result em $(( $(date +%s) - start )) s" | tee -a "$LOG"
    [ "$result" = OK ] || break
    sleep 4
done
echo FIM >> "$LOG"
