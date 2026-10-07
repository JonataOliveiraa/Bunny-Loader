# Execução em 2026-10-06

Jogo Android 301720, APK debug arm64-v8a, dois emuladores MuMu acessíveis por
ADB em `127.0.0.1:16384` e `127.0.0.1:16416`.

| Camada | Resultado |
|---|---|
| Helpers JS reais, chamadas nativas simuladas | 62 casos passaram, incluindo 200 round trips determinísticos |
| Formato C++ de produção com AddressSanitizer e UndefinedBehaviorSanitizer | 103 verificações passaram |
| Terraria nativo, persistência e transferências locais | 35 verificações, zero falhas |
| Terraria multiplayer, host | 4 verificações, zero falhas |
| Terraria multiplayer, cliente | 3 verificações, zero falhas |
| Regressão ModItem | 138 verificações, 29 assinaturas nativas verificadas |
| Regressão ModPlayer | 55 verificações, 153 assinaturas nativas verificadas |
| Regressão ModSystem | 32 verificações, 27 hooks e 9 estágios |
| Build `:app:assembleDebug` | BUILD SUCCESSFUL |
| `git diff --check` | Sem erros de whitespace |

A rodada nativa executou `SaveData` 20 vezes e `LoadData` 85 vezes, incluindo
as exceções intencionais. As mensagens `EXPECTED SaveData failure` e
`EXPECTED LoadData failure` são injeções de falha previstas. O teste confirma
que o save complementar anterior sobrevive e que um item cujo load falhou
não sobrescreve os dados originais. O outro tipo de item continua carregando.

Cobertura nativa: `Clone`, `DeepClone`, `clientClone`, `DropSelectedItem`,
`GUIPageIcons.DropUIItem`, conversão `ChestItem`, expansão de tag vazio,
resize/clone/limpeza de baú, inventário, equipamentos, dyes, acessórios
diversos, quatro bancos, seis arrays dos três loadouts, tag vazio, payload de
16 KB, save/load do personagem e arquivo complementar do baú do mundo.

Cobertura multiplayer com TCP real: cliente → host; coleta nativa por outro
jogador; host → cliente; segunda coleta nativa; alteração somente de dados do
ModItem no inventário; baú host → cliente e cliente → host. Todos preservaram
o dono original.

Evidência local preservada em `build/moditemdata/native.log`, `host.log` e
`client.log`. Linhas finais:

```text
22:06:10.103 moditemdata FIM role=native checks=35 falhas=0 saves=20 loads=85
22:07:22.198 moditemdata FIM role=host checks=4 falhas=0 saves=21 loads=22
22:07:22.187 moditemdata FIM role=client checks=3 falhas=0 saves=14 loads=7
```

APK: `app/build/outputs/apk/debug/app-debug.apk`.

SHA-256:

```text
A2A285BB55E16C760291D97FA84ABF253DA6738C4F2778567F35184BF114605D
```

As preferências originais dos dois emuladores foram restauradas, a pasta do
fixture foi removida e o encaminhamento TCP 7777 foi desfeito. Os testes
usaram cópias locais dos saves; os arquivos originais não foram usados como
destino de gravação.

## Limites da evidência

Os testes demonstram os cenários acima na versão indicada. Não representam
uma prova para toda combinação possível de mods. O código do Valentine Ring
da imagem não está neste repositório, portanto seu callback de escolha do
dono não foi alterado nem testado diretamente.

O teste JS de rede simula as chamadas IL2CPP; os sete testes multiplayer
adicionais usam o jogo e o transporte reais. A restauração nativa de baús usa
o hook de leitura real, sem encerrar/reabrir o processo. Desconexão, mensagens
malformadas, mods ausentes e arquivos antigos são cobertos nos testes de
helpers/formato, sem rodada multiplayer adicional de reconexão.

Campos de pessoas distintas exigem itens sem empilhamento ou regras próprias
de merge. O clone padrão é superficial; arrays/objetos mutáveis precisam de
um override de `Clone`. Saves na nuvem e a persistência de itens no chão não
fazem parte deste contrato.
