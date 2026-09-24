#!/usr/bin/env bash
# Cria N mundos pequenos no Bunny Loader, reiniciando o jogo depois de crash.
#   trials2.sh <n> <log>
D=127.0.0.1:16384
PKG=com.and.games505.TerrariaPaid; N=$1; LOG=$2
export MSYS_NO_PATHCONV=1
count() { adb -s $D shell "ls /sdcard/Android/data/$PKG/Worlds/ 2>/dev/null | grep -c wld"; }
boot_to_create() {
    adb -s $D shell am force-stop $PKG
    adb -s $D shell monkey -p $PKG -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
    sleep 4
    sleep 30; adb -s $D shell input tap 808 330         # Um Jogador
    sleep 3; adb -s $D shell input tap 997 327          # Jogar (personagem)
    sleep 3; adb -s $D shell input tap 1010 811         # Novo (mundo)
    sleep 2; adb -s $D shell input tap 1100 402         # Tamanho
    sleep 1; adb -s $D shell input tap 1000 340         # Pequeno
    sleep 1
}
: > "$LOG"
crashes=0
for t in $(seq 1 "$N"); do
    # Processo novo a cada tentativa: o crash so pode acontecer na PRIMEIRA
    # criacao de mundo do processo, quando o jogo inicializa as classes.
    boot_to_create
    n0=$(count)
    adb -s $D logcat -c
    start=$(date +%s)
    adb -s $D shell input tap 1020 682                  # Criar
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
    [ "$result" = OK ] || crashes=$((crashes + 1))
done
echo "FIM: $crashes crash(es) em $N" | tee -a "$LOG"
