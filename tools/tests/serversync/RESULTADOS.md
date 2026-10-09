# Sincronização por versão — 2026-10-07

## Resultado

Com sincronização ligada, versões diferentes do mesmo mod são recusadas;
versões iguais continuam entrando. O núcleo reconhece `mod.json` e
`manifest.json`, e versões vazias deixam de ser aceitas como compatíveis.
Downloads e a sessão final também exigem os metadados exatos do host.

## Reprodução antes da correção

Os testes foram executados contra os arquivos de produção anteriores à
mudança. Não foi feita uma comparação multiplayer com APK antigo.

| Teste | Resultado anterior |
|---|---|
| `native.cpp`: carregar `mod.json` com versão 1.0.0 | Falhou: `expected version 1.0.0, got` (vazio) |
| `check.mjs`: duas versões vazias | Falhou: nenhuma recusa, quando se esperava uma |
| `check.mjs`: 1.1.0 contra 1.0.0 com metadados preenchidos | Já passava: a comparação existia |

Isso reproduz a lacuna: os dois mods com `mod.json` podiam chegar à
comparação com versões vazias. Logs: `build/serversync/native-before.log`
e `build/serversync/handshake-before.log`.

## Verificação automatizada após a correção

| Camada | Resultado |
|---|---|
| Handshake JS de produção em VM | 7 casos passaram |
| Metadados do ModLoader.cpp de produção, arquivos reais | 7 verificações passaram, com ASan/UBSan |
| Política Kotlin `ServerModSyncTest` | 16 testes novos passaram |
| Suíte JVM completa | 122 testes, zero falhas, erros ou ignorados |
| `:app:assembleDebug`, incluindo código nativo | BUILD SUCCESSFUL em 2m42s |

A suíte Kotlin cobre cliente mais antigo/mais novo, offline, versão exata
instalada/desligada, versão em branco, prerelease/build, índice divergente,
admissão por uid/versão/tipo/formato, alteração após planejamento, ordem,
texturas, lista vazia e uid inválido/duplicado. O JS cobre também UUID normal
e a opção explícita de desligar a sincronização.

Logs em `build/serversync/handshake-after.log`, `native-after.log` e
`build.log`. Resultados JUnit em `app/build/test-results/testDebugUnitTest`.

## Multiplayer Android real

MuMu, host `127.0.0.1:16384`, cliente `127.0.0.1:16416`, Terraria móvel
1.4.5.8.1 build 1120, resolução 1600 × 900. Mesmo uid do fixture nos dois
lados; nenhum outro pacote de usuário habilitado durante as rodadas.
O encaminhamento ADB `tcp:7777` já existia e foi preservado.

| Host | Cliente | Observado |
|---|---|---|
| `manifest.json`, 1.1.0 | `mod.json`, 1.0.0 | Recusado; tela exige 1.1.0, sem entrada no mundo |
| `manifest.json`, 1.1.0 | `mod.json`, 1.1.0 | Cliente entra; os dois lados registram o segundo jogador |
| `mod.json`, 1.1.0 | `mod.json`, 1.0.0 | Recusado; mesma exigência de 1.1.0, sem entrada no mundo |

Evidência nos subdiretórios `build/serversync/127.0.0.1-16384` e
`build/serversync/127.0.0.1-16416`:

- `different.native.txt` / `.png`: primeiro bloqueio, às 20:20:47–48.
- `equal-final.native.txt` / `.png`: host vê `CONNECT player=1 local=0
  mode=3`; cliente registra `WORLD version=1.1.0 mode=1` e os dois jogadores,
  às 20:27:42–46. O convite para editar controles aparece após a entrada.
- `legacy-different.native.txt` / `.png`: ambos carregam suas versões de
  `mod.json`; host recusa às 20:29:25, cliente recebe a oferta às 20:29:26.
- Arquivos `.logcat.txt` correspondentes, sem crash fatal nessas coletas.

A primeira tentativa de entrada com versões iguais ficou em “Sessão
encontrada”. Havia erros de autosave porque o runner tinha criado cópias
dos saves como root sem escrita para o app. O runner foi corrigido, as
permissões das cópias foram ajustadas e o host reiniciado antes da rodada
positiva acima. Esses erros eram da preparação; os originais ficaram na
pasta de backup durante todo o teste. Toques de 200 ms foram usados para
evitar perder eventos quando as duas instâncias reduziam a taxa de quadros.

## APK e restauração

APK testado: `build/serversync/candidate.apk`, build completo do workspace,
incluindo as alterações anteriores do launcher. SHA-256:

```text
b1e27ef1db9bc7287c86dfef62566356c328a29c453d6cf77cbc9290a9cd4f4c
```

O hash do APK instalado foi conferido nos dois aparelhos; coincide com o
arquivo acima (`installed-apk.json` em cada subdiretório).

As duas restaurações terminaram com sucesso. Todos os arquivos originais
dos diretórios Players/Worlds passaram na comparação SHA-256; arquivos de
preferências/cache da loja passaram na comparação dos bytes com os backups.
O arquivo de sessão original foi restaurado quando existia; o fixture e os
diretórios temporários foram removidos. Evidências: `restore.log` e
`restored.json` de cada aparelho. A segunda instância foi desligada ao fim,
retornando ao seu estado inicial.

## Limites da evidência

- A transferência HTTP/instalação de um ZIP da loja não foi executada nesta
  rodada Android. A política de admissão foi testada em JVM; o callback de
  produção está antes da remoção da instalação existente.
- A versão precisa estar disponível na loja ou instalada para sincronizar.
  Não foi acrescentado envio de pacotes pelo host nem histórico de versões.
- A opção do host para desligar sincronização continua permitindo listas
  divergentes. O protocolo compara uid/versão/ordem, sem hash de conteúdo.
- Esta correção não é uma medição de desempenho: as verificações adicionais
  ficam na carga/entrada/sincronização, sem um novo trabalho por quadro.
