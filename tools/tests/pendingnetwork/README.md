# PvP de item e banners entre processos

Execução em 08/10/2026, Terraria Mobile 1.4.5.8/301720 ARM64, em duas
instâncias distintas do MuMu. Host: `127.0.0.1:16384`, jogador 0,
`Main.netMode = 3`. Cliente: `127.0.0.1:16416`, jogador 1, modo 1.
`emulator-5554` é um alias do primeiro aparelho e não foi contado como
terceiro processo.

## Correção e contratos

Referência: tModLoader 1.4.5, commit
`39e7995fa519a94468ebaf4699508d25903e143e`, arquivos ModItem.cs,
Player.cs.patch, CombinedHooks.cs e PlayerLoader.cs.

CanHitPvp executa no atacante, após elegibilidade nativa e antes de status,
Hurt e envio. ModifyHitPvp/OnHitPvp executam na vítima. O PlayerDeathReason
mobile transmite tipo/prefixo; PvpItemNetwork acrescenta a identidade e o
payload NetSend/NetReceive antes da mensagem 117, no mesmo fluxo TCP.
O servidor repassa o complemento antes do golpe. A vítima não depende do
slot atualmente selecionado no atacante. As filas têm limite e expiração,
e o escopo de recepção é restaurado em finally e limpo ao trocar de mundo.

A ordem de combate também foi alinhada à referência: ModifyIncomingHit,
atacante, alvo, ModPlayer geral e específico. OnHit geral do ModPlayer
precede o específico. O caso JS compõe multiplicação e adição para
detectar inversões nessa ordem.

## Resultado

| Etapa | Verificações | Falhas |
|---|---:|---:|
| Host, sessão inicial | 12 | 0 |
| Cliente, sessão inicial | 12 | 0 |
| Estado recebido na reconexão, conferido no host | 2 | 0 |
| Total no jogo | 26 | 0 |
| JS de produção, chamadas nativas simuladas | 8 | 0 |

Nas duas direções, CanHitPvp falso impediu o envio nativo e os callbacks
remotos. Com permissão, houve um envio e a vítima recebeu um único golpe
de dano 7. O estado padrão da arma é 3; o teste muda para 7 via NetSend e
retira a arma do slot imediatamente após o ataque. ModifyHitPvp e
OnHitPvp rodaram uma vez na vítima e zero no atacante.

O cliente recebeu 49 abates e zero resgates na entrada. O 50º abate no host
criou um resgate no cliente. RequestBannerClaim entregou uma unidade e
zerou a quantidade nos dois lados. Repetir o pedido não duplicou o item nem
produziu contador negativo. Após encerrar o processo cliente e reconectar,
ele recebeu 51 abates e zero pendências. Os dois checks da reconexão são
feitos no host sobre os valores lidos pelo cliente; o cliente apenas
confirma a conclusão, sem ser contado novamente.

Trechos da rodada final:

```text
22:52:05.369 pendingnetwork host FIM checks=12 falhas=0
22:52:06.143 pendingnetwork client FIM checks=12 falhas=0
22:52:30.136 pendingnetwork host REJOIN FIM checks=14 falhas=0
22:52:30.905 pendingnetwork client REJOIN FIM checks=0 falhas=0
```

Logs/capturas: `build/pendingnetwork/127.0.0.1-16384/verified.*`,
`127.0.0.1-16416/verified.*` e `rejoin.*`. Os saves, preferências e cache
originais dos dois emuladores foram restaurados e conferidos por hashes.
A segunda instância, iniciada para o teste, foi desligada.

APK instalado nos dois lados: `app/build/outputs/apk/debug/app-debug.apk`,
204975273 bytes, SHA-256
`247e0085978390efcb42f3d51ce397f09b18c336294737bfe5bec023505fcf48`.

## Reproduzir

Requer ADB, APK debug instalado, Python e emuladores com `su 0`.
Feche o jogo antes de preparar. O runner guarda os diretórios originais de
Players/Worlds e cria cópias do primeiro .plr/.wld disponível. Nunca
substitua `prepared.json` ou os backups de uma preparação em andamento.

```powershell
node tools/tests/pendingnetwork/check.mjs
adb -s 127.0.0.1:16384 shell am force-stop com.bunnyloader
adb -s 127.0.0.1:16416 shell am force-stop com.bunnyloader
python tools/tests/pendingnetwork/device.py prepare --device 127.0.0.1:16384 --role host
python tools/tests/pendingnetwork/device.py prepare --device 127.0.0.1:16416 --role client
python tools/tests/pendingnetwork/device.py launch --device 127.0.0.1:16384 --role host
adb -s 127.0.0.1:16384 forward tcp:7777 tcp:7777
# Espere pendingnetwork HOST READY no log; o fixture usa GUIMultiplayerHost.HostServer.
python tools/tests/pendingnetwork/device.py launch --device 127.0.0.1:16416 --role client
# Espere FIM nos dois lados e colete antes de encerrar o cliente.
python tools/tests/pendingnetwork/device.py collect --device 127.0.0.1:16384 --label verified
python tools/tests/pendingnetwork/device.py collect --device 127.0.0.1:16416 --label verified
python tools/tests/pendingnetwork/device.py launch --device 127.0.0.1:16416 --role client
# Espere REJOIN FIM e colete novamente com --label rejoin.
python tools/tests/pendingnetwork/device.py restore --device 127.0.0.1:16416
python tools/tests/pendingnetwork/device.py restore --device 127.0.0.1:16384
adb -s 127.0.0.1:16384 forward --remove tcp:7777
```

Execute restore nos dois lados mesmo após interrupção. O runner restaura e
confere saves, preferências, cache e eventual sessão de servidor anterior.

## Limites

O cenário usa corpo a corpo, defesa e endurance zerados, sem crítico e sem
esquiva; testa uma arma de mod com NetSend e um banner compartilhado. Não
certifica todas as combinações de buffs, críticos, projéteis PvP, três ou
mais jogadores, perda de conexão durante o resgate ou clientes adulterados.
O caso de repasse a um terceiro jogador tem teste JS simulado; o teste
nativo usa dois processos. Os testes não medem FPS.
