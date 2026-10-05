# ModPlayer em multiplayer

A suíte usa dois processos reais do jogo: um host mobile e um cliente remoto. Instale o mesmo APK nas duas instâncias e use personagens e um mundo de teste já existentes. O roteiro espera telas de 1600 × 900 e o primeiro personagem e mundo da lista.

O cliente precisa ter o servidor `10.0.2.2:7777` salvo e a permissão de sessão remota concedida. Cada emulador tem seu próprio NAT; o roteiro encaminha a porta do host ao computador com `adb forward`.

```bash
export BL_HOST=127.0.0.1:16384
export BL_CLIENT=127.0.0.1:16416
bash tools/tests/mpmodplayer/run.sh install
bash tools/tests/mpmodplayer/run.sh start
bash tools/tests/mpmodplayer/run.sh collect
```

Espere `FIM falhas=0` nos dois lados e `READY_DISCONNECT` no host. Depois execute:

```bash
bash tools/tests/mpmodplayer/run.sh disconnect
bash tools/tests/mpmodplayer/run.sh rejoin
bash tools/tests/mpmodplayer/run.sh collect
bash tools/tests/mpmodplayer/run.sh verify
bash tools/tests/mpmodplayer/run.sh cleanup
```

`disconnect` guarda o log da primeira sessão do cliente antes de encerrar seu processo. `verify` exige sucesso na sessão inicial e na reconexão, nos dois aparelhos. Os logs ficam em `build/mpmodplayer/`.

`install` guarda as configurações do launcher e seleciona abertura rápida até o título. `cleanup` restaura essas configurações, remove somente o pacote desta suíte e encerra o encaminhamento da porta. Use `cleanup` também depois de uma execução interrompida.

## Cobertura

| Grupo | Verificações |
| --- | --- |
| Isolamento | Instâncias, objetos mutáveis, modificadores e contadores separados para cada jogador; cópia profunda do estado do cliente. |
| Rede | `SyncPlayer` com estado inicial do host; `CopyClientState` automático; `SendClientChanges` com duas alterações e ausência de envios extras; origem e confirmação dos pacotes reais. |
| Conexão | `PlayerConnect`, `PlayerDisconnect` após encerrar o cliente à força, nova conexão e nova sincronização. |
| Itens | Crítico, knockback, escala, velocidade, tempo, animação, mana, cura, veto de poção e `PreItemCheck`/`PostItemCheck`. |
| Tiro | Veto de disparo, referências de `ModifyShootStats`, valores recebidos por `Shoot`, veto e consumo de munição. |
| Combate | NPC criado pelo host e replicado; modificadores e callbacks de item e projétil no cliente; ausência de callbacks duplicados no host; veto de dano recebido e esquiva consumível local. |
| Movimento | Atualização local e remota, regeneração natural, acessórios informativos, início/veto/visuais/refresh/fim de salto extra e bônus de armadura. |
| Desenho | Efeitos, informações, camadas, transformação dos dados e renderização; câmera e zoom restritos ao jogador local. |
| Mundo | Nível de pesca por referência e veto de teleporte. |

A suíte registra todos os hooks para testar sua instalação, mas não afirma cobertura funcional de todos. Compras, vendas, enfermeira, recompensas, captura, inventário inicial, persistência e servidor dedicado precisam de cenários adicionais. A passagem de um callback sem alterar seu resultado não verifica todos os seus casos de borda.

## Particularidades verificadas

O host mobile executa com `Main.netMode = 3`; o cliente remoto usa `1`. A suíte reconhece o bit de servidor do host.

Nesta versão nativa, `CheckMana` retorna `true` mesmo sem saldo e sinaliza a condição em `slowMagicUse`. Por isso, os testes verificam o saldo e os callbacks de consumo, incluindo consumo parcial, sem usar o retorno como prova de pagamento. `blockQuickMana` impede `OnMissingMana`, conforme o [fluxo do tModLoader](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/Player.TML.cs).

O NPC de combate recebe vida de teste nas duas instâncias. Alterar `lifeMax` no host não faz o cliente receber esse máximo personalizado pela mensagem vanilla de NPC. Sem preparar o alvo no cliente, o primeiro golpe o matava antes do teste de projétil.

O host mobile pode desativar um jogador sem chamar os helpers de desconexão. `PlayerNetworkHooks` também verifica os jogadores conectados após a atualização do jogador local, mantendo uma única notificação por desconexão. Essa checagem precisa executar com `LocalUserGameState` carregado; fora desse contexto, o jogador local pode parecer inativo. O contrato de `PlayerDisconnect` pode ser consultado na [referência de ModPlayer](https://docs.tmodloader.net/docs/stable/class_mod_player.html).

A conexão guarda o objeto original do jogador. Se o jogo substituir o slot por um `Player` vazio, a notificação usa a instância anterior e seu estado de ModPlayer. O teste de reconexão exige uma única notificação para o remoto e nenhuma para o host local.

Os testes de lógica complementares são executados com:

```bash
node tools/tests/modplayerhooks/check.mjs
```
