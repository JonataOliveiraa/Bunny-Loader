#!/usr/bin/env bash
# Sobe uma sessao de multijogador entre duas instancias do MuMu (1600x900),
# so com toques em coordenadas fixas — nada de print no meio:
#   H hospeda o primeiro mundo com o primeiro personagem;
#   C entra no servidor salvo na lista dele (10.0.2.2:7777).
# No host liga: Sem inimigos (limpa e fica em "Novos"), Imortal e meio-dia.
#
#   tools/mp-session.sh
#   BL_HOST=127.0.0.1:16384 BL_CLIENT=127.0.0.1:16416 tools/mp-session.sh
#
# Pre-condicoes (uma vez so):
#  - APK de debug instalado nas duas: sem conta Google a segunda instancia nao
#    tem o Terraria da Play, e so a build de debug passa sem ele (Eligibility).
#  - Cada instancia do MuMu fica atras do proprio NAT (as duas sao 10.0.2.15).
#    A porta 7777 do host vai para o PC por `adb forward` (feito aqui), e o
#    cliente chega ao PC por 10.0.2.2.
#  - No cliente, o servidor 10.0.2.2:7777 salvo na lista (Conectar com
#    "Lembre-se" ligado) e o aviso de sessao remota respondido com "Sempre".
set -u
H="${BL_HOST:-127.0.0.1:16384}"
C="${BL_CLIENT:-127.0.0.1:16416}"
adb connect "$H" >/dev/null
adb connect "$C" >/dev/null
launch() {
    local d=$1
    adb -s $d shell am force-stop com.bunnyloader
    adb -s $d logcat -c
    adb -s $d shell monkey -p com.bunnyloader -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
    for i in $(seq 1 40); do
        sleep 1
        adb -s $d logcat -d | grep -q "Displayed com.bunnyloader/dev.bunnyloader.LauncherActivity" && break
    done
    sleep 2
    adb -s $d shell input tap 800 805          # JOGAR
}
launch $H
launch $C
sleep 38
# host: Multijogador > personagem > aba Host > Host > Jogar
adb -s $H shell input tap 808 410; sleep 3
adb -s $H shell input tap 997 327; sleep 3
adb -s $H shell input tap 932 210; sleep 3
adb -s $H shell input tap 996 398; sleep 3
adb -s $H shell input tap 1010 695
# cliente: Multijogador > personagem (fica na lista de servidores)
adb -s $C shell input tap 808 410; sleep 3
adb -s $C shell input tap 997 327
sleep 26
adb -s $H forward tcp:7777 tcp:7777 >/dev/null
# host: poderes
adb -s $H shell input tap 194 500; sleep 2
for i in 1 2; do adb -s $H shell input tap 570 680; sleep 0.8; done   # Todos (limpa)
sleep 1.5
for i in 1 2; do adb -s $H shell input tap 570 680; sleep 0.8; done   # desliga, Novos
adb -s $H shell input tap 866 250; sleep 0.8                          # Imortal
adb -s $H shell input tap 144 164; sleep 1                            # meio-dia
adb -s $H shell input tap 1540 56
# cliente: seleciona o servidor salvo e entra
adb -s $C shell input tap 700 516; sleep 2
adb -s $C shell input tap 996 598
sleep 25
adb -s $C shell input tap 590 517                                     # "Mais tarde"
echo pronto
