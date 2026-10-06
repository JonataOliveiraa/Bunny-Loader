# ModPlayer, ModItem e DrawDataCache em multiplayer

O [resultado de 6 de outubro de 2026](RESULTADOS-DRAW-MODITEM.md) registra 274 verificações em jogo, com 8 falhas de PvP. Os cenários de desenho e reconexão passaram. O [resultado anterior](RESULTADOS.md) conserva a execução de ModPlayer de 5 de outubro.

A suíte usa dois processos reais do jogo: um host mobile e um cliente remoto. Instale o mesmo APK nas duas instâncias e use personagens e um mundo de teste já existentes. O roteiro espera telas de 1600 × 900 e o primeiro personagem e mundo da lista.

O cliente precisa ter o servidor `10.0.2.2:7777` salvo e a permissão de sessão remota concedida. Cada emulador tem seu próprio NAT; o roteiro encaminha a porta do host ao computador com `adb forward`.

```bash
export BL_HOST=127.0.0.1:16384
export BL_CLIENT=127.0.0.1:16416
export BL_RUN_ID=20261006-minha-execucao
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

`disconnect` guarda os logs da primeira sessão dos dois processos antes de encerrar o cliente. Essa cópia evita perder o resultado inicial quando o buffer do logcat gira durante a reconexão. Espere também `REJOIN_DRAW FIM falhas=0` nos dois lados antes de verificar. `verify` exige sucesso na sessão inicial e na reconexão. Os logs ficam em `build/mpmodplayer/$BL_RUN_ID/`.

Para investigar falhas já registradas sem abandonar o teste de reconexão:

```bash
BL_ALLOW_KNOWN_FAILURES=1 bash tools/tests/mpmodplayer/run.sh disconnect
bash tools/tests/mpmodplayer/run.sh rejoin
bash tools/tests/mpmodplayer/run.sh collect
bash tools/tests/mpmodplayer/run.sh verify-lifecycle
bash tools/tests/mpmodplayer/run.sh verify
bash tools/tests/mpmodplayer/run.sh cleanup
```

Essa opção permite desconectar um cliente que terminou com falhas. `verify-lifecycle` verifica somente a reconexão e seus desenhos; `verify` continua reprovando a rodada completa. Erros de carregamento do fixture e sinais fatais não são aceitos.

`install` guarda as configurações do launcher, seleciona abertura rápida até o título e desativa temporariamente os outros pacotes para manter os dois processos com o mesmo fixture. `cleanup` restaura apenas as chaves alteradas, preserva as outras preferências atuais, remove somente o pacote desta suíte e encerra o encaminhamento da porta. Os backups permanecem disponíveis em disco. Use o mesmo `BL_RUN_ID` nas tentativas e na limpeza; escolha outro identificador para uma nova sessão após a limpeza. Use `cleanup` também depois de uma execução interrompida.

No Windows, execute o roteiro com o Bash do Git, com `adb`, `python` e `rg` no PATH. O `bash.exe` do WSL não recebe automaticamente essas ferramentas Windows.

## Cobertura

| Grupo | Verificações |
| --- | --- |
| Isolamento | Instâncias, objetos mutáveis, modificadores e contadores separados para cada jogador; cópia profunda do estado do cliente. |
| Rede | `SyncPlayer` com estado inicial do host; `CopyClientState` automático; `SendClientChanges` com duas alterações e ausência de envios extras; origem e confirmação dos pacotes reais. |
| Conexão | `PlayerConnect`, `PlayerDisconnect` após encerrar o cliente à força, nova conexão e nova sincronização. |
| Itens | Crítico, knockback, escala, velocidade, tempo, animação, mana, cura, veto de poção e `PreItemCheck`/`PostItemCheck`. |
| ModItem | Os 27 métodos das etapas 1 a 3, com estado separado por item, alterações de valores nativos, colisão, hitbox, frames, seleção e consumo de munição, última unidade, cura normal/rápida e composição com ModPlayer. |
| PvP de ModItem | Ataques e vetos nas duas direções; observa a cópia do remoto, `SendPlayerHurt` e `OnHurt` no processo da vítima; verifica callbacks na vítima e dano efetivamente recebido. |
| Tiro | Veto de disparo, referências de `ModifyShootStats`, valores recebidos por `Shoot`, veto e consumo de munição. |
| Combate | NPC criado pelo host e replicado; modificadores e callbacks de item e projétil no cliente; ausência de callbacks duplicados no host; veto de dano recebido e esquiva consumível local. |
| Movimento | Atualização local e remota, regeneração natural, acessórios informativos, início/veto/visuais/refresh/fim de salto extra e bônus de armadura. |
| Desenho | Efeitos, informações, camadas, transformação dos dados e renderização; câmera e zoom locais; escrita sem contador, contador manual e `ModPlayer.AddDrawData`, para local/remoto nos dois processos e após reconexão. |
| Mundo | Nível de pesca por referência e veto de teleporte. |

A suíte registra todos os hooks para testar sua instalação, mas não afirma cobertura funcional de todos. Compras, vendas, enfermeira, recompensas, captura, inventário inicial, persistência e servidor dedicado precisam de cenários adicionais. A passagem de um callback sem alterar seu resultado não verifica todos os seus casos de borda.

Os métodos de ModItem ainda não implementados ficam fora desta rodada. Cura, munição e consultas de arma são exercitadas no jogador local de cada processo; a replicação de cada recurso não é inferida desses resultados. O PvP e o desenho têm verificações explícitas entre os processos.

`draw.js` cria marcadores magenta para o jogador 0 e ciano para o jogador 1 com `MagicPixel`, sem chamar `DrawData.Draw`. Um hook de diagnóstico verifica o cache após o renderer, incluindo reordenação de camadas. Os marcadores permanecem até encerrar o jogo para conferência visual. Esses hooks e contadores pertencem somente ao fixture.

## Particularidades verificadas

O host mobile executa com `Main.netMode = 3`; o cliente remoto usa `1`. A suíte reconhece o bit de servidor do host.

Nesta versão nativa, `CheckMana` retorna `true` mesmo sem saldo e sinaliza a condição em `slowMagicUse`. Por isso, os testes verificam o saldo e os callbacks de consumo, incluindo consumo parcial, sem usar o retorno como prova de pagamento. `blockQuickMana` impede `OnMissingMana`, conforme o [fluxo do tModLoader](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/Player.TML.cs).

O NPC de combate recebe vida de teste nas duas instâncias. Alterar `lifeMax` no host não faz o cliente receber esse máximo personalizado pela mensagem vanilla de NPC. Sem preparar o alvo no cliente, o primeiro golpe o matava antes do teste de projétil.

O host mobile pode desativar um jogador sem chamar os helpers de desconexão. `PlayerNetworkHooks` também verifica os jogadores conectados após a atualização do jogador local, mantendo uma única notificação por desconexão. Essa checagem precisa executar com `LocalUserGameState` carregado; fora desse contexto, o jogador local pode parecer inativo. O contrato de `PlayerDisconnect` pode ser consultado na [referência de ModPlayer](https://docs.tmodloader.net/docs/stable/class_mod_player.html).

A conexão guarda o objeto original do jogador. Se o jogo substituir o slot por um `Player` vazio, a notificação usa a instância anterior e seu estado de ModPlayer. O teste de reconexão exige uma única notificação para o remoto e nenhuma para o host local.

Os testes de lógica complementares são executados com:

```bash
node tools/tests/modplayerhooks/check.mjs
node tools/tests/moditemhooks/check.mjs
```
