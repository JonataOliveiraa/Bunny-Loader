# Resultado da execução multiplayer

Execução em 5 de outubro de 2026, com dois processos reais do APK debug em emuladores MuMu. Host `127.0.0.1:16384`, jogador 0, `netMode = 3`; cliente `127.0.0.1:16416`, jogador 1, `netMode = 1`. O cliente entrou pela rede, teve o processo encerrado à força e reconectou ao mesmo host.

| Etapa | Verificações | Falhas |
| --- | ---: | ---: |
| Sessão inicial do host | 48 | 0 |
| Sessão inicial do cliente | 50 | 0 |
| Desconexão e reconexão no host | 4 | 0 |
| Total multiplayer | 102 | 0 |

O cliente confirmou a reconexão. Foram observados 73 callbacks distintos no jogador local do host e 82 no jogador local do cliente; esses números incluem hooks antigos e novos e não representam cobertura funcional completa de ModPlayer.

Trechos dos logs da execução final:

```text
14:14:29.857 mpmodplayer host FIM falhas=0 casos=48 hooks=73
14:14:31.400 mpmodplayer cliente FIM falhas=0 casos=50 hooks=82
14:17:42.235 mpmodplayer host DESCONECTOU jogador=1
14:18:25.364 mpmodplayer host reconexao chama PlayerConnect: ok
14:18:25.364 mpmodplayer host desconexao chama PlayerDisconnect uma vez: ok
14:18:25.364 mpmodplayer host desconexao nao dispara para o host local: ok
14:18:25.364 mpmodplayer host SyncPlayer repete na reconexao: ok
14:18:25.364 mpmodplayer host REJOIN FIM falhas=0
14:18:27.270 mpmodplayer cliente REJOIN FIM
```

As regressões em JavaScript passaram: 34 verificações de comportamento, 150 assinaturas nativas conferidas e quatro estágios nativos registrados. As verificações de sintaxe JavaScript, Bash e Python e `git diff --check` passaram. `:app:assembleDebug` terminou com sucesso.

SHA-256 do APK instalado nas duas instâncias:

```text
c53aeda1ce336267c97df9f99511571548c8af004a3fa7793e505c25e8b1b04b
```

## Correções decorrentes dos testes

- A desconexão mobile pode substituir o slot por um jogador vazio sem passar pelos helpers nativos de desconexão. O acompanhamento conserva o objeto conectado, detecta a desativação ou substituição durante a atualização local e notifica sua instância original de ModPlayer uma única vez.
- `OnMissingMana` respeita `blockQuickMana`. `OnConsumeMana` depende do sucesso de `CheckMana`, de pagamento solicitado e de consumo positivo. A suíte também verifica o consumo parcial específico desta versão nativa.

O [roteiro e os limites de cobertura](README.md) descrevem os cenários exercitados e os que ainda precisam de testes específicos. A execução não valida um servidor dedicado real nem todas as interações de loja, enfermeira, inventário e persistência.
