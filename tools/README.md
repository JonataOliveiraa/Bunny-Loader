# tools/: ferramentas de PC

Nenhuma roda sozinha no build; todas são chamadas à mão.

## Desenvolvimento

| Script | O que faz |
|---|---|
| [`ui.sh`](ui.sh) | Ciclo curto de interface: compila sem os assets e as `.so` do jogo, instala, abre e printa (~6 s). |
| [`dump.sh`](dump.sh) | Regenera `refs/` a partir do jogo instalado (pull + extração + Il2CppDumper). |
| [`dumpgrep.sh`](dumpgrep.sh) | Extrai o bloco de uma classe, struct ou enum do `refs/dump.cs`. |
| [`gen-cheatdata.py`](gen-cheatdata.py) | Gera `cheatbridge/bunny/CheatData.java` (os NPCs escolhidos a mão) a partir do `refs/dump.cs`. Os itens não passam por aqui: o menu os separa pelos campos do próprio jogo. |
| [`build-cheatbridge-dex.sh`](build-cheatbridge-dex.sh) | Compila o menu de cheats em dex e embute como header C. |
| [`bin2header.py`](bin2header.py) | Binário → array C. Usado pelo script acima. |
| [`ui-sprites.py`](ui-sprites.py) | Recorta as texturas de interface do jogo que o menu usa (horas da Jornada, setas) e o "?" do item de mod ausente (`content/items/UnloadedIcon.h`). Recebe a pasta `Images` do jogo. |
| [`mod-art.py`](mod-art.py) | As capas e os ícones dos mods de exemplo. |

```bash
tools/ui.sh                                     # iterar em tela
tools/dump.sh                                   # pipeline completo
tools/dump.sh --skip-pull                       # reaproveita refs/base.apk
tools/dumpgrep.sh Projectile Terraria           # inspecionar uma classe
```

Não haverá empacotador de `.bmod`: o pacote é montado à mão pelo autor, com a
documentação como referência.

## Testes

Os testes são **mods**: cada pasta de [`tests/`](tests) instala no jogo, faz o
que testa de verdade (usa o item, invoca o chefe, salva e recarrega o mundo) e
escreve no log uma linha por checagem, terminando em `<teste> FIM: tudo ok` ou
`<teste> FIM: N falha(s)`.

[`bench/run.sh`](bench/run.sh) roda um ciclo no emulador: instala os mods
pedidos, abre o jogo, entra no **primeiro** mundo da lista, espera o `FIM` de
cada teste e salva as linhas de resultado.

```bash
tools/bench/run.sh saida.txt samples/ExampleMod tools/tests/boss
BL_DEVICE=127.0.0.1:16416 tools/bench/run.sh saida.txt tools/tests/refs   # outro emulador
```

- Pré-condições: o APK instalado, um mundo e o personagem de teste ("Bench")
  primeiro da lista.
- **Um teste por vez.** O `run.sh` não tira os mods de rodadas anteriores, e
  juntos eles se atrapalham (o tiro do `hooks` mata o slime do `npcs`): apague
  os `test-*` de `bunny_packs/` entre um e outro.
- Os poderes do Mod Menu ficam salvos; o `run.sh` os apaga antes de abrir.
- **Lápides:** o jogador que morre num teste deixa uma, e com algumas por
  perto o jogo liga o cemitério (música, luz, névoa e spawn mudam). O
  `run.sh` põe o `tests/cleanworld` em toda rodada: ao entrar no mundo, ele
  tira as lápides (as paradas e as caindo) a 200 blocos do spawn e do
  jogador e salva o mundo. `BL_KEEP_GRAVES=1` desliga (e tira o pacote dele do
  aparelho).
- Com mais de um emulador, `BL_DEVICE` escolhe qual. O `run.sh` só toca na
  tela com o Bunny Loader na frente (um toque cego numa tela inicial do
  emulador já instalou um jogo da loja).

| Teste | O que confere | Precisa do Example Mod |
|---|---|---|
| **A ponte** | | |
| `nullable` | Tipos, structs, arrays, `Nullable<T>`, hooks com struct. | |
| `wrappers` | Objeto segurado só pelo JS sobrevive ao coletor; identidade (`===`). | |
| `structindex` | `proj.ai[0]`, `localAI.get_Item`, `oldPos[i].X`, `hideMisc[i]` e os limites; uma linha de log de 6 KB inteira no arquivo. | |
| `extrafields` | `bl.defineField`, `bl.defineMethod`. | |
| `strictnames` | Membro que a classe não tem é erro (com sugestão), `in`, consultas do motor sem erro; assinatura só com os nomes exatos dos parâmetros. | |
| `refs` | `ref`/`out` na chamada e no hook, struct por `ref`, `Ref` repassado e solto. | |
| `files` | `bl.file`, `bl.directory`, `bl.path`. | |
| `hookslots` | Centenas de hooks de uma vez, e a profundidade de hooks encadeados no mesmo método. Com 25 hooks nos nomes, as listas do menu demoram a aceitar toque: se parar na seleção de mundo, toque em Jogar. | sim |
| `ifbusy` | Hook que não espera o motor JS preso noutra thread (`ifBusy`). | **não**: rodar sem |
| `enginethreads` | A thread do jogo e a do save no `original()` ao mesmo tempo, cada uma com a sua pilha JS. | |
| `wrappermap` | Fuzz do `WrapperMap` contra `std::unordered_map` (roda como binário, `wrappermap/run.sh`). | |
| **Conteúdo** | | |
| `moditems` | As tabelas de item de mod. | |
| `modsave` | Save de item de mod com o mod ligado e desligado (rodar mais de uma vez). | |
| `projectiles`, `npcs`, `buffs`, `tiles` | Cada tipo de conteúdo de mod. | |
| `tilesave` | Três rodadas: com o mod, sem ele e com ele de novo. | |
| `tileframes` | Duas rodadas: o quadro de um 3x3 de mod e de um de pedra ao reabrir o mundo. | |
| `localization` | `ModLocalization.Translate` devolve o texto (chave funda, queda para o inglês), `Language.GetText('Mods.<id>.…')` sem passo extra, `Key`/`GetText`/`Exists`, os `{$chave}` (inteira, relativa, do jogo, `@n`, circular, no `Register`), as categorias do jogo (`RandomFromCategory`), a variante `Chave$Variante` e a troca de idioma (ida e volta, no mesmo `LocalizedText`; com o Example Mod, também o nome de NPC e de item). | opcional |
| `multitile` | Duas rodadas: a Pia de Exemplo (2x2, `TileObjectData`) colocada, quebrada por uma célula e pelo chão (um drop só), salva e reaberta. | sim |
| `modfurniture` | Os móveis do Example Mod pelos caminhos do jogo: a casa com porta, mesa, cadeira e tocha de mod passa no `RoomNeeds`; porta abre e fecha (pelo toque, sozinha com o jogador encostando, com o morador passando, no fio; o goblin guerreiro arromba e o peão derruba, soltando o item da porta de mod); luz firme da tocha, fogueira, lustre e luminária; caixa de música no chão e equipada; toque na cadeira senta; fio apaga a luminária; buff da fogueira. Não salva. | sim |
| `tileperf` | Tempo de quadro com 150 tochas do jogo e com 150 tochas e 10 fogueiras de mod na tela, e o custo de cada método do `ModTile` por quadro. Não salva. | sim |
| `armor` | Texturas vestidas (slots, `Count` e tabelas crescidos, `AddEquipTexture`, `EquipTexture` própria, `AutoloadEquip`), conjunto por item e por `GlobalItem`, vaidade e sombras, `FrameEffects` (fantasia), `SetMatch` (manto), asas (`WingStats`, as três velocidades e `WingUpdate`), manequim. Veste e tira; não salva. | sim |
| `armorsave` | Duas rodadas: veste e salva o personagem; depois confere que as peças voltaram, com os slots desenhados, e limpa. | sim |
| `mparmor` | Multijogador (host e cliente, `mpa ...`): cada lado veste o conjunto, as asas e a barba e vê os slots de mod e o bônus do outro. | não (mp-session) |
| `prefix` | Duas rodadas: status, nome, tooltip, rolagem e categorias dos prefixos do Example Mod; salva itens com prefixo de mod no inventário e num baú. Na 2ª um prefixo a mais desloca os números, e o save devolve pelo nome. Limpa no fim. | sim |
| `mpbiome` | Multijogador (`mpb ...`, etapa B4 do ModBiome): o bioma do host chega a quem entra, o do cliente chega ao host ligado e desligado, a troca do host chega ao cliente, e ninguém recebe `OnEnter`/`OnLeave` de jogador remoto. | não (mp-session) |
| `mpprefix` | Multijogador (`mpp ...`): cada lado põe itens com prefixo de mod e o outro confere prefixo e status; o host confere a defesa do acessório do cliente. | não (mp-session) |
| `hooks` | Os hooks das classes: IA, spawn natural, Bestiário, tooltip, receita, uso de item. | |
| `recipes` | Receitas e grupos. | |
| `globals` | `GlobalItem`, `GlobalNPC`, `GlobalProjectile` (filtro, instância por entidade, `Clone`), drops por tipo e globais com o Bestiário, e o `ModSystem` (mundo, atualização, dados salvos). Rodar duas vezes: a segunda carrega o que a primeira salvou. | sim |
| `modplayer` | `ModPlayer` (quadro, dano, morte, dados salvos). | |
| `modcontent` | `ModContent` e o que fica fora do Mod Menu. | sim |
| `exmod1`, `exmod2` | O Example Mod inteiro, usado. | sim |
| `summons` | Pets, lacaio e sentinela usados como o dedo usaria. | sim |
| `townnpc` | Morador de mod em quatro rodadas: com o mod, com, sem e com de novo. | sim |
| `townshop` | Conversa, loja, felicidade, retrato e perfil. | sim |
| `townfight` | Ataque e gore do morador. | sim |
| `boss` | O chefe nascendo pelo item de invocação. | sim |
| `drops` | Drop de item de mod pelas regras do jogo; 400 slots no chão, primeira criação e recriação de EmergencyStacking.GroupLookup, e 120 mortes no mesmo quadro. Usar mundo de teste: esvazia os drops existentes. | sim |
| `fishline` | A linha da vara de mod. | sim |
| `biomescan` | A contagem de blocos dos biomas (etapa B0 do ModBiome): todo `SceneMetrics._tileCounts` do jogo com o tamanho novo, a ordem e a frequência da varredura, e a contagem exata de `ExampleTile` com 0/39/40/41 blocos. Loga também as medições (`biomescan medida ...`). | sim |
| `modbiome` (+ `modbiomeb`) | `ModBiome` e `ModSceneEffect` (etapa B1): `Type` e `ModContent.GetInstance`, `player.InModBiome`, `OnEnter`/`OnInBiome`/`OnLeave` uma vez por troca, dois biomas ao mesmo tempo, a música da cena pela prioridade, `IsBiomeActive` que lança, a mesma classe em dois mods e o bioma por 40 `ExampleTile` (`TileCountsAvailable`). Rodar as duas pastas juntas. | sim |
| `arrays` | Arrays do jogo pelo JS: `lista.makeGeneric(tipo)` (nomes do C#, nome completo, classe, ajudante), `Uint8Array` numa cópia só, `Classe.newArray(n \| lista)`, `System.Array.Reverse` num deles, `fill`/`empty`/`find` (também num campo do jogo), e os erros. | sim |
| `bgscan` | A medição dos fundos (etapa B5.0): tamanho dos arrays (`TextureAssets.Background`, `bgAlphaFrontLayer`...), ordem e thread de cada passo do desenho do fundo, e três faixas desenhadas pelo JS nas camadas longe, meio e perto (conferir no print). | sim |
| `modbg` | Fundos de mod (etapa B5): `BackgroundTextureLoader`, os números dos estilos, `Priority` `None` sem efeito, `BiomeLow` ganhando da floresta (superfície e subsolo, com as 4 texturas trocadas em `backTexture`/`backTextureValues`) e a volta ao do jogo. Rodar de dia, numa floresta. | sim |
| `modwater` | Água de mod (etapa B6): `ModWaterStyle`/`ModWaterfallStyle` com os números depois dos do jogo e as tabelas crescidas, a troca da água pela cena com o fade, o `CalculateWaterStyle`, o respingo, a chuva e a luz através da água, e a cachoeira de mod pedida ao jogo. Faz um lago na frente do jogador e loga `modwater: tela ...` para a captura (a textura do teste é vermelha). Não salva. | não |
| `modmap` | Fundo do mapa de mod (etapa B6): `MapBackground` de uma cena no mapa em tela cheia (a textura cobre a tela no lugar do fundo do jogo, com a cor do céu na superfície), `MapBackgroundFullbright`, `MapBackgroundColor` e a volta ao fundo do jogo. Abre o mapa com o zoom longe (a borda do mundo à vista) e loga `modmap: tela ...` para a captura (a textura do teste é vermelha). Não salva. | não |
| `modspawn` | Spawn natural como no tModLoader: `GlobalNPC.EditSpawnRate` (o jogador-alvo, a taxa do jogo, e a forçada vale), `EditSpawnInfo` antes do `EditSpawnPool` no mesmo ponto, o `SpawnChance` com o ponto, o chão, o bloco e os campos do `NPC.Spawner`, o pool só com o de mod, vazio e com um tipo do jogo (`GlobalNPC.SpawnNPC` depois), `EditSpawnRange` na área do `GetSpawnArea`, e o spawn do jogo sem mudanças. Tira os inimigos por perto: **não rodar junto do `hooks`** (apaga o NPC que ele observa). Não salva. | não |
| `cleanworld` | Não é um teste: tira as lápides (bloco 85 e as que ainda caem) a 200 blocos do spawn e do jogador, no 2º quadro dentro do mundo, e salva o mundo. O `run.sh` o põe em toda rodada (`BL_KEEP_GRAVES=1` desliga). | sim |
| `biomeworld` | O ciclo de vida das flags (etapa B2): o bioma por blocos liga e desliga no próprio `Teleport`, e a saída do mundo (`WorldGen.SaveAndQuit`) chama o `OnLeave`, zera as flags e a cena e chama o `ResetNearbyTileEffects`. **Sai do mundo no fim**: rodar sozinho. | sim |
| `biomemusic` | A música da cena contra a do jogo, degrau por degrau (etapa B3): superfície, silêncio, `Music -1`, `Priority None`, chuva de slime, um chefe do jogo (Geleia Rainha), a caixa de música do jogo, o Otherworld, e uma faixa de mod tocando e parando. Com "Sem inimigos" ligado no Mod Menu, o chefe some e o teste falha. | sim |
| `exmodfixes` | Correções do Example Mod pelo caminho do jogo: o botão de gancho (`QuickGrapple`), `Item.material`, a luz do bloco de gemas, fogo vivo sobre fogo vivo, a origem e o **toque de verdade** no relógio (contra o do jogo), o conjunto do 1.4.5 (`ArmorSetBonuses`), escudo/bota do mesmo tipo e a troca pelo toque, e o chicote (curva sorteada, e acerto fora da linha de mira contra o de espinhos). O passo do relógio espera um `adb shell input tap` nas coordenadas que ele loga. | sim |
| `sounds` | `SoundStyle`, `SoundEngine.PlaySound`, `MaxInstances`, `UseSound` de item usado. | sim |
| `music` | O chefe trocando a música do jogo pela dele, cada troca conferida no meio do fade. | sim |
| `crossmod` (+ `crossmodtarget`) | `ModLoader.TryGetMod` e `Call`, nas duas ordens de carga. | |
| `autoload` (+ `nomodclass`) | A estrutura do mod: a classe `Mod` do `export default` (`Load`, `bl.mod`), o registro automático de `Content/` e `Common/` (ordem, `Autoload = false`, o que não é de mod), a textura espelho e pelo nome em `Assets/Textures/`, gore, tradução, e um mod sem a classe `Mod` que não carrega. | |
| **Multijogador** | | |
| `mprecipes`, `mpplayer`, `mpitems`, `mptiles`, `mpglobals`, `mpnet` | Um cliente e um host (ver `mp-session.sh`): itens de mod usados pelo cliente e conferidos no host, o dash com toque de verdade. | sim |
| `mpshop` | Loja de morador de mod no multijogador: o host põe a Pessoa ao lado do cliente; o cliente conversa, toca em Loja e compra em moedas e em moeda própria (e solta o item no inventário, como o dedo); o host confere a conversa, os itens e o pagamento pela rede, e que a Pessoa mantém a vida máxima. | sim |

[`mp-session.sh`](mp-session.sh) sobe uma sessão de multijogador entre duas
instâncias do MuMu: uma hospeda, a outra entra, sem inimigos novos e de dia.

## Medição e investigação

| Pasta | O que é |
|---|---|
| [`bench/`](bench) | O mod de benchmark da ponte JS → IL2CPP (cada operação 20 mil vezes, melhor de 7) e o `compare.py`, que compara rodadas. Os números estão no [guia de custo](../docs/mods/03-custo-e-desempenho.md) e no [histórico da otimização](../docs/historico/PONTE-OTIMIZACAO.md). |
| [`crash-trials/`](crash-trials) | Cria mundos em série no MuMu, reiniciando o app a cada tentativa, e conta crashes ([histórico](../docs/historico/AVALIACAO-PONTE-E-CRASH.md)). |
| [`disasm/`](disasm) | Desassembly anotado da `libil2cpp.so` (`prep.sh` gera os caches a partir de `refs/`). Limites compilados: `scan_limits.py` (`cmp #N`), `scan_loops.py` (fim de laço em bytes), `scan_movs.py` (`new T[N]`) e `scan_halved.py` (o `0 < x < N` feito pela metade, do drop). Ver [conteúdo por dentro](../docs/nucleo/conteudo.md#os-limites-compilados). |
