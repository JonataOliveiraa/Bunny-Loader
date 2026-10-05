# tools/: ferramentas de PC

Nenhuma roda sozinha no build; todas são chamadas à mão (menos o `wiki.py`,
que o GitHub roda a cada push na `main`).

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
| [`wiki.py`](wiki.py) | Gera a [wiki](https://github.com/JonataOliveiraa/Bunny-Loader/wiki) a partir do `README.md` e de `docs/`. Roda sozinho no workflow `.github/workflows/wiki.yml` a cada push na `main`; à mão, só para conferir. |

```bash
tools/ui.sh                                     # iterar em tela
tools/dump.sh                                   # pipeline completo
tools/dump.sh --skip-pull                       # reaproveita refs/base.apk
tools/dumpgrep.sh Projectile Terraria           # inspecionar uma classe
```

Não haverá empacotador de `.bmod`: o pacote é montado à mão pelo autor, com a
documentação como referência.

## Catálogo online

Os mods que a aba Explorar baixa moram neste mesmo repositório, sem servidor:

| Onde | O quê |
|---|---|
| branch `mods-index` | `index.json` (a lista) e `mods/<uid>/` (a vitrine de cada mod: manifesto, ícone, capa, `description.md`, fotos dos autores). Branch órfão, sem o código. |
| Releases `mod-<id>-v<versão>` | O `.bl` de cada versão. Nunca marcadas como *Latest*. |

O app lê tudo sem login (`raw.githubusercontent.com` e o link de download da
Release) e confere o sha256 do índice antes de instalar
([`RemoteCatalog.kt`](../app/src/main/kotlin/dev/bunnyloader/mods/RemoteCatalog.kt)).
Não há token no APK. Só publicar pede um, no PC.

**O jeito fácil:** dois cliques em [`mods/gerenciar.bat`](mods/gerenciar.bat) (ou
`python tools/mods/manager.py`). Abre uma página no navegador
(`http://127.0.0.1:8770`) que mostra os mods de `samples/` com o status de cada
um (em dia, versão nova pronta, mudou sem subir versão), publica com um clique,
sobe a versão, confere e publica um `.bl` arrastado, e tira mods do catálogo.
O token é colado na página e fica só na memória do gerenciador. A página só
atende este computador e exige uma chave sorteada a cada execução.

Por baixo, ela chama os mesmos scripts da linha de comando.
[`mods/publish.py`](mods/publish.py) confere o pacote com as regras do import do
app, cria a Release, monta a vitrine em `build/mods-index/` (um worktree do
branch) e faz o push do índice:

Só o ExampleMod vai dentro do APK (`BUNDLED_SAMPLES` no `app/build.gradle.kts`).
Os outros de `samples/` são publicados aqui. [`mods/pack.py`](mods/pack.py) gera o
`.bl` de uma pasta, sempre com os mesmos bytes, em `build/mods-packs/`.

O token pode vir de `BL_GITHUB_TOKEN`; sem ela, o script pede na hora, sem
mostrar na tela.

```bash
python tools/mods/pack.py samples/VidaCheia      # -> build/mods-packs/vidacheia-1.0.0.bl
python tools/mods/publish.py --dry-run Mod.bl    # confere e monta, sem rede
python tools/mods/publish.py Mod.bl Outro.bl     # publica
python tools/mods/publish.py --remove <uid>      # tira do catálogo (a Release fica)
```

Atualizar um mod é publicar o `.bl` com a `version` maior no `manifest.json`:
o app compara com a instalada e mostra "Atualizar". Republicar a mesma versão
com outro conteúdo é recusado. O `raw.githubusercontent.com` guarda cache por
alguns minutos, então a mudança pode demorar um pouco para aparecer no app.

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

[`bench/repeat.sh`](bench/repeat.sh) repete uma rodada até o jogo congelar, para
travamento intermitente: vigia o `FIM` de um teste no logcat e guarda o logcat
da rodada que travou. Apague antes os pacotes de teste que ficaram no aparelho.

```bash
tools/bench/repeat.sh 12 modfurniture samples/ExampleMod tools/tests/modfurniture tools/tests/prefix
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
| `optarg` | Método solto de um objeto (`const f = Main.spriteBatch['...']; f()`) chama no dono; `null` num struct com valor padrão vira o `default(T)` (chamada e `original()`), struct obrigatório e número recusam. | |
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
| `tilesettled` | Prontidão dos blocos de mod, incluindo zero blocos registrados: evita que o início rápido espere para sempre sem o Example Mod. Binário independente (`tilesettled/run.sh`), sem abrir o jogo nem alterar saves. | não |
| **Conteúdo** | | |
| `moditems` | As tabelas de item de mod. | |
| `modsave` | Save de item de mod com o mod ligado e desligado (rodar mais de uma vez). | |
| `projectiles`, `npcs`, `buffs`, `tiles` | Cada tipo de conteúdo de mod. | |
| `trailcache` | Rastro de projétil de mod (`TrailingMode` 0 e 2, `TrailCacheLength`): `oldPos`, `oldRot` e `oldSpriteDirection` com o tamanho pedido e preenchidos, ao lado de projéteis do jogo. | |
| `tilesave` | Três rodadas: com o mod, sem ele e com ele de novo. | |
| `tileautosave` | Com o Example Mod: dispara a gravação automática (`WorldGen.saveAndPlay`, numa thread) e confere a cada quadro que os tiles de mod não somem enquanto ela grava. Rodar junto com o `tilesave`. | |
| `bossbag` | Com o Example Mod: a `ExampleBossBag` é abrível, abrir pelo `ItemSlot.TryOpenContainer` gasta uma e dá o `ItemLoot` (ExampleItem e as moedas do chefe), e as regras do chefe têm o `NotExpert` e a bolsa. | |
| `modmount` | `ModMount`: o tipo depois dos do jogo (`MountID.Count`, `Mount.mounts` e `MountID.Sets` crescidos), a textura `_Back`, e os hooks numa montaria do teste (`SetMount`/`Dismount` com `skipDust` e o dado por jogador, `UpdateEffects`, `UpdateFrame`, `JumpHeight`, `JumpSpeed`, `Draw`). Com o Example Mod: o carro (duas camadas, o buff, os balões) e o carrinho de mina (`Cart`, `SetAsMinecart`, os `delegations` copiados, montado pelo buff via `BuffID.Sets.MountType`) e o botão de montaria do toque (`Player.QuickMount`, pelo `miscEquips[3]`). No fim deixa o jogador montado no carro, para olhar na tela. | |
| `hookchain` | Com o Example Mod (`samples/ExampleMod`): o gancho de exemplo no espaço de gancho, lançado a cada 90 quadros para o céu; o `PreDrawExtras` chamado. Na tela: só a corrente do mod, sem a do jogo por baixo. Do quadro 400 em diante, `PreDrawExtras` true e `PreDraw` false: a corrente do jogo sem a cabeça do gancho, e o `PostDraw` roda. | sim |
| `frametime` | Tempo de quadro: o `DoUpdate` e o `DoDraw` do jogo e o intervalo entre quadros, com zoom 1, 2 e 0,75, e os hooks que mais custam (`bl.hookStats`: chamadas e tempo de JS por quadro), as coletas do QuickJS e o custo de uma coleta. O resumo sai também no chat do jogo: empacotado como `.bl` (`out/frametime.bl`), mede no celular sem adb. Para comparar o app com e sem mods, e versões dele (ver `bisect/`). Rodar com `BL_KEEP_GRAVES=1` e sem outros pacotes no aparelho. | não |
| `bgreset` | Com o Example Mod: uma cena sempre ativa põe um fundo de superfície de mod (texturas do exemplo) e loga, a cada segundo, o estilo, os arrays de textura e de transparência e o `LocalUserGameState`. Para comparar antes e depois de um evento (segundo plano, troca de usuário). | não |
| `tooltipdraw` | O tooltip de mod e a raridade de mod na tela: põe um item de teste (raridade e prefixo de mod) no `HoverItem` e pede o `MouseText` por uns quadros; confere os nomes das linhas do tModLoader, o `ModifyTooltips` (linha nova, `Hide`, `OverrideColor`), os `Pre/PostDrawTooltip(Line)`, o `yOffset`, a cor do nome e o `GetPrefixedRarity`. Com o Example Mod, mostra depois o `ExampleTooltipItem`; o tooltip fica uns 15 s na tela para uma captura. | não |
| `oldpos` | O rastro do projétil lido de dois jeitos: `oldPos[i]` e `oldPos.get_Item(i)` (que no celular devolve `ref Vector2`), com `oldRot` e `oldSpriteDirection`. | |
| `hookcost` | O custo de um hook que só repassa ao `original()`, em métodos de uma chamada por quadro, medido pelo `bl.hookStats` e por dentro do JS. | não |
| `perfloader` | Desempenho do loader dos mods: na carga, os padrões dos loaders em JS puro; no mundo, com o Example Mod, o custo por entidade de cada hook com 60 projéteis e 15 slimes de mod, e o tempo de quadro com e sem elas. Ver `docs/historico/DESEMPENHO-LOADER.md`. | não |
| `basecls` | O exemplo de classe base da documentação (`04-conteudo-novo.md`): a base com `static Autoload = false` e a base fora de `Content/` e `Common/` não são registradas; as filhas entram com o `SetDefaults` da base pelo `super`, os campos e os métodos herdados, e o `instanceof` pela base. | |
| `getcontent` | `Mod.GetContent(Base)` e `ModContent.GetContent(Base)`: os `ModItem` do próprio mod na ordem do registro e com o `Type`, outra base (`ModBuff`, `ModSystem`), nada de outro mod, todos os mods juntos e erro com base que não é classe. | |
| `dupnamesa`, `dupnamesb`, `dupnamesc` | Rodam juntos: A e B têm `ModPlayer` e `ModItem` com a mesma classe (`Abc`). Os dois registram; pelo nome cada um acha o seu; `'mod/Abc'` acha o do outro; o save grava uma chave por mod e o load devolve a cada um o seu (o A faz a volta e limpa). O C, sem `Abc`, recebe `undefined`/0 pelo nome e acha os dois por `'mod/Abc'`. | |
| `itempostupdate` | `ModItem.PostUpdate`: a Alma de Exemplo jogada no chão chama o `PostUpdate` com a `WorldItem` (com `Center`), e a da hotbar (casa 48) desenha sem erro no `GetAlpha`. Limpa as duas no fim. | sim |
| `modwall` | `ModWall` com a ExampleWall: tipo depois dos do jogo, `WallID.Count` aumentado, textura, `SetStaticDefaults` (`Main.wallHouse`), `wallBlend`, o item que coloca; `PlaceWall`, `KillWall` com drop; o mapa (`wallLookup` e a cor); a conversão da corrupção com `WallID.Sets.Conversion.Stone`; com a ExampleWallAdvanced, o `AnimateWall` trocando o `Main.wallFrame` e o `ModifyLight`; o save leva a parede e a tinta para o `.walls.bl`. Rodar duas vezes: a segunda confere que a parede e a tinta voltaram no load e limpa. | sim |
| `achievements` | As conquistas do jogo com as de mod registradas: o `_completedCount` de cada uma igual às condições completas (a releitura do arquivo o dobrava, e as do jogo já completas apareciam bloqueadas), e a TIMBER completando pelo aviso de coleta de madeira. Zera a TIMBER do aparelho. | |
| `menusky` | Com o Example Mod: o `DrawSunAndMoon` do tema de mod (filtro `flag: 'menu.sky'`) fora do JS no mundo e religado de volta aos menus (`SaveAndQuit`). | não |
| `tilename` | Com o Example Mod: a estação de trabalho de mod no guia de criação, com o nome (`Recipe.GetRequiredTileName`, o do mapa, e o texto do `GUICraftGuidePopup.UpdateText` com a receita da `ExampleLamp`) e o ícone (`TileID.Sets.CraftingStationItemId`, tabela só do celular). | não |
| `bgwatch` | Diagnóstico, não mexe em nada: loga o estado do fundo de superfície a cada 10 s e sempre que muda (estilo, soma das transparências das camadas — zero é a tela só com o céu —, arrays, `LocalUserGameState`, cena). Feito para rodar no celular do usuário com o logcat capturando. | não |
| `cloudforce` | Diagnóstico: 5 s depois de entrar no mundo, e a cada 30 s, metade das nuvens vira o primeiro tipo de nuvem de mod, e loga se a máscara do horizonte (`TextureMaskManager.CloudMasks`) desse tipo existe. Para o desenho do horizonte do celular, que quebrava o fundo inteiro (ver `docs/historico/FUNDO-SUMINDO-NUVEM-DE-MOD.md`). | não |
| `soltos` | A etapa 13, com conteúdo próprio e o do Example Mod: `ModCommand` pelo processador do chat (argumentos, apelido, `UsageException`, o que não é de mod segue, o `/help`; `/heal` e `/addtime` do exemplo), `ModHair` (tipo e textura, a lista da criação de personagem, o save pelo nome com 0 no arquivo do jogo), `ModCloud` (comum no `addCloud` com `OnSpawn`, peso 0, rara no `RollRareCloud`), `ModEmoteBubble` (`NewBubble` com `OnSpawn`, o menu por categoria, o desenho da bolha), `ModAchievement` (registro com nome, condição completa com `OnCompleted`, `OnNPCKilled`; o ManyWormsKilled do exemplo) e o peixe de missão (na lista, a fala do Pescador, fora do modo difícil não sorteia, `CatchFish` de ponta-cabeça). **Salva o personagem** (o teste do cabelo). | sim |
| `tileframes` | Duas rodadas: o quadro de um 3x3 de mod e de um de pedra ao reabrir o mundo. | |
| `localization` | `ModLocalization.Translate` devolve o texto (chave funda, queda para o inglês), `Language.GetText('Mods.<id>.…')` sem passo extra, `Key`/`GetText`/`Exists`, os `{$chave}` (inteira, relativa, do jogo, `@n`, circular, no `Register`), as categorias do jogo (`RandomFromCategory`), a variante `Chave$Variante` e a troca de idioma (ida e volta, no mesmo `LocalizedText`; com o Example Mod, também o nome de NPC e de item). | opcional |
| `multitile` | Duas rodadas: a Pia de Exemplo (2x2, `TileObjectData`) colocada, quebrada por uma célula e pelo chão (um drop só), salva e reaberta. | sim |
| `modfurniture` | Os móveis do Example Mod pelos caminhos do jogo: a casa com porta, mesa, cadeira e tocha de mod passa no `RoomNeeds`; porta abre e fecha (pelo toque, sozinha com o jogador encostando, com o morador passando, no fio; o goblin guerreiro arromba e o peão derruba, soltando o item da porta de mod); luz firme da tocha, fogueira, lustre e luminária; caixa de música no chão e equipada; toque na cadeira senta; fio apaga a luminária; buff da fogueira. Não salva. | sim |
| `tileperf` | Tempo de quadro com 150 tochas do jogo e com 150 tochas e 10 fogueiras de mod na tela, e o custo de cada método do `ModTile` por quadro. Não salva. | sim |
| `wallperf` | Tempo de quadro com 2130 paredes na tela (71x30): sem parede, parede do jogo, ExampleWall e ExampleWallAdvanced (luz, animação, `WallFrame`); o tempo de colocar, o custo de cada método do `ModWall` e o do save com 2130 paredes do jogo e de mod. Salva o mundo no fim já sem as paredes (a área cavada fica, a mesma do `tileperf`). | sim |
| `armor` | Texturas vestidas (slots, `Count` e tabelas crescidos, `AddEquipTexture`, `EquipTexture` própria, `AutoloadEquip`), conjunto por item e por `GlobalItem`, vaidade e sombras, `FrameEffects` (fantasia seca e molhada, ordem Pre/EquipTexture/Update), `SetMatch` (manto), asas (`WingStats`, as três velocidades e `WingUpdate`), manequim. Veste e tira; não salva. | sim |
| `loadorder/A`, `B`, `C`, `D` | Quatro mods no mesmo `Player.Update`: logam carga, entrada e saída da cadeia no primeiro quadro. Reordene pelas setas em Pacotes e confira com `python tools/tests/loadorder/check.py --device SERIAL --expected ACBD`. | não |
| `armorsave` | Duas rodadas: veste e salva o personagem; depois confere que as peças voltaram, com os slots desenhados, e limpa. | sim |
| `mparmor` | Multijogador (host e cliente, `mpa ...`): cada lado veste o conjunto, as asas e a barba e vê os slots de mod e o bônus do outro. | não (mp-session) |
| `prefix` | Duas rodadas: status, nome, tooltip, rolagem e categorias dos prefixos do Example Mod; salva itens com prefixo de mod no inventário e num baú. Na 2ª um prefixo a mais desloca os números, e o save devolve pelo nome. Limpa no fim. | sim |
| `mpbiome` | Multijogador (`mpb ...`, etapa B4 do ModBiome): o bioma do host chega a quem entra, o do cliente chega ao host ligado e desligado, a troca do host chega ao cliente, e ninguém recebe `OnEnter`/`OnLeave` de jogador remoto. | não (mp-session) |
| `mpprefix` | Multijogador (`mpp ...`): cada lado põe itens com prefixo de mod e o outro confere prefixo e status; o host confere a defesa do acessório do cliente. | não (mp-session) |
| `hooks` | Os hooks das classes: IA, spawn natural, Bestiário, tooltip, receita, uso de item. | |
| `recipes` | Receitas e grupos. | |
| `globals` | `GlobalItem`, `GlobalNPC`, `GlobalProjectile` (filtro, instância por entidade, `Clone`), drops por tipo e globais com o Bestiário, e o `ModSystem` (mundo, atualização, dados salvos). Rodar duas vezes: a segunda carrega o que a primeira salvou. | sim |
| `modplayer` | `ModPlayer` (quadro, dano, morte, dados salvos). | |
| `modplayerhooks` | Registra todos os hooks de jogador; verifica referências, mana, tempos, vetos, clone, crafting e callbacks de atualização e desenho. `node tools/tests/modplayerhooks/check.mjs` executa os testes de comportamento e confere assinaturas usando o dump local. | não |
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
| `biomecontent` | O resto do ModBiome: `DisplayName`/`TownNPCDialogueName` (padrão, da classe por cultura e do `Localization` do Example Mod), `SetBiomeAffection` com bioma de mod (o Guia, com casa no lugar, cobra menos e a fala cita o bioma), o Bestiário (o bioma do `SpawnModBiomes` na entrada, o ícone 30 x 30, o fundo 115 x 65 e o filtro; e o do ExampleSlimeNPC e do ExamplePerson, que também ama o bioma de exemplo), `GlobalNPC.EditSpawnFlags` e o `SpawnCondition` (somam 1 no ponto). Precisa do Example Mod. Não salva. | não |
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
| [`bisect/`](bisect) | `buildat.sh <commit>` compila o APK de um commit antigo numa worktree fora do repositório (QuickJS, assets e `.so` do jogo ligados por junção e desfeitos antes de apagar) e deixa em `out/bisect/app-<commit>.apk`: a busca binária de uma regressão. Uma compilação de cada vez. Cuidado: o MuMu só tem tela de 60 Hz, e outra instância ligada no PC distorce a medição. |
| [`crash-trials/`](crash-trials) | Cria mundos em série no MuMu, reiniciando o app a cada tentativa, e conta crashes ([histórico](../docs/historico/AVALIACAO-PONTE-E-CRASH.md)). |
| [`disasm/`](disasm) | Desassembly anotado da `libil2cpp.so` (`prep.sh` gera os caches a partir de `refs/`). Limites compilados: `scan_limits.py` (`cmp #N`), `scan_loops.py` (fim de laço em bytes), `scan_movs.py` (`new T[N]`) e `scan_halved.py` (o `0 < x < N` feito pela metade, do drop). Ver [conteúdo por dentro](../docs/nucleo/conteudo.md#os-limites-compilados). |
