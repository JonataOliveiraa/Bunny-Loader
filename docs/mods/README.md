# Criando mods para o Bunny Loader

Um mod do Bunny Loader é JavaScript que roda **dentro** do Terraria Mobile. Ele
lê e escreve os campos do jogo, chama os métodos dele e intercepta (hook)
qualquer método para mudar o que ele faz. Não precisa compilar nada nem
recompilar o jogo: é uma pasta com um `manifest.json` e um `main.js`.

Há dois jeitos de escrever um mod. Dá para misturar os dois no mesmo pacote.

| Guia | Para quê |
|---|---|
| [1. Do zero, com hooks](01-hooks-do-zero.md) | Mudar o que o jogo **já tem**: dano, vida, queda, a Minishark... A ponte JS ↔ jogo inteira: classes, campos, métodos, hooks, structs, texturas. |
| [2. Conteúdo novo: ModItem, ModNPC...](02-moditem-modnpc.md) | **Criar** item, projétil e NPC novos, com textura, nome, receita, drop, spawn natural e Bestiário, no formato do tModLoader. |
| [3. `ref` e `out`](03-ref-e-out.md) | Chamar e hookar método com parâmetro `ref`/`out`: pesca, taxa de spawn, colisão. O `Ref` e o que ele pode e não pode. |

Os mods de `samples/` são exemplos completos, e curtos:

| Mod | O que mostra |
|---|---|
| `samples/DobroDeDano` | O menor mod que faz algo: um hook em `Item.SetDefaults`. |
| `samples/VidaCheia`, `samples/SemQueda` | Hook em `Player.Update`, mexendo em campos do jogador. |
| `samples/HelloMod` | A Minishark mais rápida, e o comentário do topo documenta a API inteira. |
| `samples/ExampleMod` | Item, arma, munição, projétil e NPC novos, com receita, drop, Bestiário e tradução. |

---

## O pacote

Um mod é uma pasta (ou um zip dela) com esta cara:

```
MeuMod/
  manifest.json      quem é o mod (obrigatório)
  icon.png           ícone quadrado, aparece na lista (recomendado)
  banner.png         capa da ficha do mod, ~3,4:1, ex. 384x112 (opcional)
  thumbnails/        imagens extras da ficha, .png ou .jpg (opcional)
  content/           o mod em si
    main.js          o arquivo de entrada (o nome vem do manifest)
    ...              o que mais ele usar: outros .js, texturas, traduções
```

Tudo que o mod lê (outro `.js`, uma textura, um `.json`) fica dentro de
`content/`, e os caminhos são relativos a ela. Um mod não enxerga arquivo fora
da própria pasta.

### manifest.json

```json
{
  "uid": "7c2e9d41-5b3a-4f8e-9a61-2d0c4e8b7f15",
  "id": "meumod",
  "name": "Meu Mod",
  "version": "1.0.0",
  "author": "Seu Nome",
  "category": "Jogabilidade",
  "summary": "Uma linha, para o cartão da lista.",
  "description": "Um parágrafo, para a ficha do mod.",
  "updated": "2026-09-24",
  "blVersion": 1,
  "entry": "main.js"
}
```

| Campo | |
|---|---|
| `uid` | **Obrigatório.** Um UUID minúsculo. É a identidade do mod e o nome da pasta dele: dois mods com o mesmo `uid` são o mesmo mod (instalar um substitui o outro). Gere um uma vez e nunca mude: `python -c "import uuid; print(uuid.uuid4())"`. |
| `id` | Apelido curto, só letras. Não precisa ser único. |
| `name`, `author`, `version` | O que a lista mostra. |
| `category` | Texto livre. `Textura`, `Armas`, `Jogabilidade`, `Cheat`, `Utilidade` e `Itens` ganham cor e ícone próprios. |
| `summary`, `description`, `updated` | O cartão e a ficha do mod. `updated` é `AAAA-MM-DD`. |
| `blVersion` | Versão da API do Bunny Loader que o mod usa. Hoje, `1`. Um mod que pede uma versão maior que a do app é recusado na importação. |
| `entry` | O arquivo de entrada, relativo a `content/`. Padrão: `main.js`. |

## Instalando

Os mods instalados moram em

```
Android/data/com.bunnyloader/bunny_packs/<uid>/
```

a mesma pasta dos saves (`Players/`, `Worlds/`), e é **dali** que o jogo carrega.
Três jeitos de pôr um mod lá:

1. **Importar** — na aba *Pacotes*, "Importar pacote", escolha o zip. O zip
   (`.bmod` ou `.zip`) tem `manifest.json`, `icon.png` e `content/` na raiz, ou
   dentro de uma pasta só, que o app ignora. Se o manifesto estiver errado, o
   app diz o quê e não instala nada.
2. **Copiar a pasta** — com um gerenciador de arquivos, ponha a pasta do mod em
   `bunny_packs/`. Ela aparece na lista quando o app volta à frente. Com outro
   nome (`bunny_packs/MeuMod/`), o app renomeia para o `uid` do manifesto.
3. **adb**, o mais rápido para desenvolver. Mande um `.tar` e extraia no
   aparelho — em alguns aparelhos e emuladores o `adb push` de uma pasta com
   subpastas para `Android/data` chega pela metade:

   ```bash
   tar -C MeuMod -cf mod.tar .
   adb push mod.tar /data/local/tmp/mod.tar
   adb shell "P=/sdcard/Android/data/com.bunnyloader/bunny_packs/7c2e9d41-5b3a-4f8e-9a61-2d0c4e8b7f15; rm -rf \$P; mkdir -p \$P && tar -xf /data/local/tmp/mod.tar -C \$P && chmod -R 777 \$P"
   ```

   O `chmod` importa: arquivo posto pelo adb pertence ao usuário `shell`, e sem
   ele o app lê o mod mas não consegue apagá-lo nem atualizá-lo depois.

Para mudar um mod instalado, edite os arquivos dentro de `bunny_packs/<uid>/` e
**reabra o jogo**: os mods são lidos quando o jogo sobe, não enquanto ele roda.
O interruptor na aba *Pacotes* liga e desliga o mod para o próximo boot.

## Vendo o que o mod faz

Tudo que o mod escreve com `bl.log(...)`, e todo erro de JavaScript, vai para
um arquivo de texto, um por vez que o jogo abre:

```
Android/data/com.bunnyloader/logs/bunny_2026-09-25_14-03-27.txt
```

Ficam os 3 mais recentes (o mais velho é apagado), então o log da vez em que o
jogo fechou sozinho ainda está lá quando você reabre. Cada linha tem a hora e o
nível (`I` informação, `W` aviso, `E` erro):

```
14:03:31.204 I [mod] Dano em Dobro: ativo
```

Pelo computador, o mesmo sai no logcat, com a tag `BunnyLoader`:

```bash
adb logcat -s BunnyLoader
```

`bl.log` aceita vários valores, como o `console.log`, e objeto ou array vira
JSON: `bl.log('vida', player.statLife, { x: 1 })` escreve `vida 400 {"x":1}`.

Um erro no topo do `main.js` impede o mod de carregar e aparece ali com a linha.
Um erro dentro de um hook não derruba o jogo:

- é logado (`hook: excecao no callback: ...`) — a **cada** chamada, então um
  erro num hook de todo quadro enche o log rápido;
- se o callback quebrou antes de chamar `original`, o Bunny Loader chama o
  método original por você, e o jogo segue como se o hook não existisse.

Nos métodos das classes (`SetDefaults` de um `ModItem`, `AI` de um `ModNPC`...)
o erro é logado **uma vez** por método e o resto segue.

Com "Mostrar erro dentro do jogo" ligado (aba *Config*), o erro também aparece
na tela, dentro do jogo.

## Multijogador

Os mods rodam em cada aparelho, o de quem hospeda e o de quem entra. Duas
regras:

- **Todos precisam dos mesmos mods de conteúdo.** Item e NPC de mod ganham um
  número na ordem em que são registrados; se um lado tiver um mod a mais ou a
  menos, o número de um item aponta para outra coisa no outro lado.
- **Hook roda para todos os jogadores.** `Player.Update` é chamado para cada
  jogador do mundo, não só para o seu. Para mexer só no seu, confira o índice:
  `if (i !== Terraria.Main.myPlayer) return;` (ver o guia 1).
