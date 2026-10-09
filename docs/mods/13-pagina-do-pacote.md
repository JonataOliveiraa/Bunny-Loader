# 13. A página do pacote: descrição, novidades, licença e autores

A ficha de um mod no launcher (o que abre quando alguém toca nele) é montada
**pelo pacote**. O texto vem de arquivos Markdown, as fotos dos autores de uma
pasta, e as cores do próprio manifesto. O launcher só desenha, no estilo dele:
fonte pixelada com contorno, painéis com bisel e imagem sem borrar.

```
MeuMod/
  authors/            fotos dos autores (opcional)
    potato.png
  content/
    main.js           o mod em si (guias anteriores)
  manifest.json       quem é o mod
  description.md      aba Descrição
  changelog.md        aba Novidades (opcional)
  license.md          aba Licença (opcional)
  icon.png            ícone quadrado, na lista
  icon.gif            o ícone animado, no launcher (opcional)
  banner.png          capa da ficha, ~3,4:1 (ex.: 384x112); banner.gif anima
  thumbnails/         imagens extras, no pé da Descrição (opcional)
  docs/               o que mais você quiser mostrar (opcional)
    fundo.png         o fundo da ficha, se o theme pedir (opcional)
```

**Formatos de imagem.** Em toda a ficha (capa, vitrine, fotos dos autores,
fundo e imagens do Markdown) valem PNG, GIF, WebP, JPG e BMP. **GIF anima**
na capa, no fundo, na vitrine e no Markdown (as fotos dos autores mostram só o
primeiro quadro). WebP animado aparece parado. A capa pode ser `banner.gif`,
`banner.png`, `banner.webp` ou `banner.jpg`; com mais de uma, vale nessa ordem.

A ficha fica assim, de cima para baixo:

1. a **capa** (`banner.png`), com voltar e favoritar;
2. o **cabeçalho**: ícone, nome, as fotos e os nomes dos autores, a categoria,
   a ficha técnica (versão, data, tamanho, licença), os botões (baixar, ligar,
   remover, exportar) e os **links** do manifesto;
3. as **abas**: Descrição, Novidades, Licença, Créditos e as que você criar.
   Descrição aparece sempre; as outras dependem dos arquivos e campos do pacote.

### Ícone animado

Um `icon.gif` ao lado do `icon.png` deixa o ícone animado no launcher (lista,
destaque e ficha). O GIF tem a preferência; o `icon.png` continua sendo o
ícone do mod **dentro do jogo** (o menu de mods e as Config. dos Mods não
animam), então mande os dois. Sem filtro, como o PNG: o pixel fica quadrado.

- quadrado, de até 512x512 (passou disso, o launcher usa o `icon.png`);
- o tempo de cada quadro vem do GIF; quadro sem tempo, ou de 10 ms, fica 100 ms;
- transparência e quadros parciais funcionam;
- um GIF muito longo é cortado nos primeiros quadros (uns 4 milhões de pixels
  ao todo: 60 quadros de 256x256, ou 975 de 64x64).

Salve os documentos em **UTF-8**, com os nomes em minúsculas como no exemplo.
Ao exportar o `.bl`, esses documentos e a pasta `authors/` vão junto com o mod.

## O `manifest.json`

Os campos novos estão marcados. O resto está no [README](README.md#o-manifestjson).

```json
{
  "uid": "7c2e9d41-5b3a-4f8e-9a61-2d0c4e8b7f15",
  "id": "meumod",
  "name": "Meu Mod",
  "version": "1.2.0",
  "authors": [
    { "name": "Potato", "role": "Código", "avatar": "potato.png",
      "color": "#E8C170", "link": "https://github.com/JonataOliveiraa" },
    "Ciclano"
  ],
  "category": "Armas",
  "summary": "Uma linha, para o cartão da lista.",
  "license": "MIT",
  "updated": "2026-10-01",
  "blVersion": 2,
  "links": [
    { "title": "Código-fonte", "url": "https://github.com/voce/meumod" },
    { "title": "Discord", "url": "https://discord.gg/..." }
  ],
  "pages": [
    { "title": "Receitas", "file": "docs/receitas.md" }
  ],
  "theme": {
    "accent": "#7FE07A",
    "panel": "#24413A",
    "text": "#D8F0DD",
    "heading": "#FFFFFF",
    "button": "#2F6B4F",
    "buttonText": "#FFFFFF",
    "background": "docs/fundo.png",
    "backgroundMode": "tile",
    "backgroundScale": 2,
    "backgroundDim": 0.3
  }
}
```

| Campo | |
|---|---|
| `authors` | **Novo.** Quem fez o mod. Cada um é um nome solto (`"Ciclano"`) ou um objeto (abaixo). Um texto só também vale: `"authors": "Fulano"`. |
| `author` | A forma antiga, um nome só. Ainda funciona; com `authors`, é ignorado. |
| `summary` | Continua aqui: é a linha do cartão na lista, e não muda. |
| `license` | **Novo.** O nome curto da licença (`MIT`, `CC BY-NC 4.0`, `Todos os direitos reservados`). Aparece na ficha técnica e no topo da aba Licença. O texto inteiro vai em `license.md`. |
| `links` | **Novo.** Botões no cabeçalho. Só `https://` e `http://` aparecem. |
| `pages` | **Novo.** Abas a mais, cada uma um `.md` do pacote. Uma página cujo arquivo não existe não aparece. |
| `theme` | **Novo.** O tema da ficha deste mod: as cores do texto, dos painéis e dos botões, e o fundo da tela (abaixo). |
| `description` | A forma antiga da descrição. Só vale quando o pacote **não** tem `description.md`. |

### Os autores

```json
{
  "name": "Animatak_",
  "role": "ExMod do TL Pro",
  "avatar": "animatak.png",
  "color": "#841FFF",
  "link": "https://discord.com/invite/..."
}
```

| Campo | |
|---|---|
| `name` | **Obrigatório.** O nome que aparece. |
| `role` | O que a pessoa fez: "Código", "Arte", "Música", "Tradução"... Aparece embaixo do nome na aba Créditos. |
| `avatar` | O arquivo da foto **dentro de `authors/`**. |
| `color` | A cor do nome (`#RRGGBB`). Sem foto, também é a cor do quadrado com a inicial. |
| `link` | Tocar no autor, na aba Créditos, abre o link. |

**A foto.** Com `avatar`, vale `authors/<avatar>`. Sem ele, o app procura
`authors/<nome>.png` e depois o nome em minúsculas com hífen
(`"Bunny Loader"` → `authors/bunny-loader.png`); também aceita `.jpg`, `.jpeg`, `.webp` e
`.gif` (parado). Sem foto nenhuma, aparece a inicial do nome.

Use uma imagem **quadrada** (o que sobra é cortado). Pixel art pequena (32x32,
64x64) é ampliada sem borrar; foto grande é reduzida com suavização. Não passe
de 256x256: a foto aparece com no máximo 56 dp e o resto é peso no pacote.

A aba Créditos lista todos os autores, com a foto grande. No cabeçalho
aparecem até quatro fotos, uma por cima da outra, e "por A, B e C".

### As cores (`theme`)

| Cor | Onde |
|---|---|
| `accent` | A aba escolhida, os links, o fio embaixo dos títulos, os marcadores de lista, as caixas de tarefa e a versão atual no changelog. Padrão: o amarelo do toque. |
| `panel` | O fundo dos painéis da ficha (cabeçalho, conteúdo, abas). Padrão: o azul dos painéis do Terraria. |
| `text` | O texto corrido. |
| `heading` | Os títulos (`#`, `##`...) e o nome do mod. |
| `button` | O fundo dos botões: baixar, atualizar, remover, exportar e os links. O botão apertado e a caixa de ligar/desligar usam a mesma cor, mais escura. Padrão: os botões do launcher (e os links no azul de seleção). |
| `buttonText` | O texto dos botões, links inclusive. Padrão: branco nos botões e o `accent` nos links. |

Todas são `#RRGGBB`, `#AARRGGBB` ou `#RGB`. Uma cor que falta (ou que não se
lê) fica a do launcher, então dá para mudar só o `accent`. O tema vale **só
dentro da ficha do seu mod**; a lista e as outras telas seguem a paleta do app.

> [!TIP]
> Escolha um `panel` escuro: o texto tem contorno preto e branco por cima, e
> num painel claro ele fica pesado.

### O fundo (`background`)

Com a ficha aberta, o cenário do launcher (a floresta, a neve...) dá lugar ao
fundo do seu mod:

| Campo | |
|---|---|
| `background` | Uma cor (`"#1B2430"`) ou uma imagem do pacote, pelo caminho a partir da raiz (`"docs/fundo.png"`). GIF anima. |
| `backgroundMode` | `"cover"` (o padrão): a imagem enche a tela, cortando o que sobra. `"tile"`: repete a imagem lado a lado, como um papel de parede — bom para textura de bloco do jogo. |
| `backgroundScale` | Só no `tile`: quantos pontos da tela cada pixel da imagem ocupa (padrão `2`; um bloco de 16x16 vira um quadrado de 32). |
| `backgroundDim` | De `0` a `1`: uma camada preta por cima do fundo, para os painéis se destacarem. `0.3` já ajuda numa imagem clara. |

O fundo fica parado atrás da ficha (não rola junto) e cobre a tela inteira,
atrás da barra de baixo também. Sem `background`, continua o cenário do launcher.

- Para `cover`, use uma imagem em pé, perto da proporção de um celular (por
  exemplo 270x480 em pixel art, ou 1080x1920 em foto). Os lados mais largos que
  a tela são cortados.
- Para `tile`, use uma imagem pequena que encaixe nas bordas (16x16, 32x32, 48x48).
- GIF animado vai até 512x512 em qualquer lugar da ficha; maior que isso ele
  aparece parado, no primeiro quadro. Um GIF muito longo é cortado nos
  primeiros quadros (uns 2 milhões de pixels ao todo: 30 quadros de 256x256).

## `description.md`

A aba Descrição. É Markdown comum (a [lista inteira](#o-markdown) está mais
abaixo). As imagens de `thumbnails/` aparecem no fim dela.

Se o pacote não tem `description.md`, a aba mostra o `description` do
manifesto, e se ele também não existe, o `summary`.

Um modelo para copiar, com um pouco de cada recurso:
[`samples/modelo-description.md`](../../samples/modelo-description.md).

## `changelog.md`

A aba Novidades. Cada título `##` que **começa com um número de versão** vira um
cartão que abre e fecha:

````markdown
# Novidades

## Ainda sem versão

- O que já está pronto mas não saiu.

## 1.2.0 - 2026-10-01

- Espada nova.
- **Correção:** o arco não atirava no multijogador.

## [1.1.0] - 2026-09-20

- Primeira versão pública.
````

- O número aceita `1.2.0`, `v1.2.0` e `[1.2.0]` (o formato do *Keep a
  Changelog*). A data, opcional, é `AAAA-MM-DD`, depois de um `-` ou entre
  parênteses, e aparece por extenso ("1 de out. de 2026").
- Texto depois da data vira o título da versão: `## 1.2.0 - 2026-10-01 - A atualização do gelo`.
- O primeiro cartão vem aberto. O da versão **instalada** (a `version` do
  manifesto) também vem aberto e ganha a etiqueta *Atual*.
- O que vem antes da primeira versão (como o "Ainda sem versão" acima) aparece
  em cima, como Markdown comum. O `# Novidades` do topo some: é o nome da aba.
- Um changelog sem títulos de versão sai como Markdown comum.

## `license.md`

A aba Licença: o `license` do manifesto numa etiqueta, e o texto do
`license.md` embaixo. Com só um dos dois, a aba aparece do mesmo jeito.

Se o seu mod é um port ou usa arte de outro, é aqui que vai o aviso de
copyright que a licença do original pede (o `ExampleMod` é um port do
tModLoader e leva o aviso MIT dele).

## Abas a mais (`pages`)

```json
"pages": [
  { "title": "Receitas", "file": "docs/receitas.md" },
  { "title": "Perguntas", "file": "docs/faq.md" }
]
```

Cada uma vira uma aba depois de Créditos, na ordem da lista. O caminho é
relativo à raiz do pacote. A barra de abas rola de lado, então cabem quantas
você quiser, mas nomes curtos ficam melhores.

## O Markdown

Tudo abaixo funciona em qualquer `.md` da ficha.

### Texto

| Você escreve | Sai |
|---|---|
| `**negrito**` ou `__negrito__` | **negrito**, na cor dos títulos |
| `*itálico*` ou `_itálico_` | *itálico* |
| `~~riscado~~` | ~~riscado~~, apagado |
| `==marcado==` | fundo na cor de destaque |
| `` `código` `` | fonte de código, num fundo escuro |
| `[texto](https://...)` | link (toque abre o navegador) |
| `https://...` ou `<https://...>` | link solto |
| `\*` | o caractere `*` mesmo (vale para `` ` _ [ ] # | `` etc.) |
| dois espaços ou `\` no fim da linha | quebra de linha |

Links só abrem `https://`, `http://` e `mailto:`; os outros aparecem coloridos,
mas não fazem nada.

### Do Terraria

| Você escreve | Sai |
|---|---|
| `[c/FF8800:texto]` | o texto nessa cor (a tag de cor do chat do jogo) |
| `[i:757]` | o ícone do item 757 (a Lâmina da Terra), do tamanho da letra |
| `[b:2]` | o ícone do buff 2 (Regeneração) |

Os IDs são os do jogo (`ItemID`, `BuffID`). Item de mod não tem ícone aqui:
para ele, use a imagem do seu pacote (abaixo).

### Imagens

```markdown
![Legenda](content/Content/Items/Espada.png)

Pegue a ![](content/Content/Items/Moeda.png) Moeda e troque na loja.
```

- O caminho é **relativo à raiz do pacote**: `content/...`, `thumbnails/...`,
  `docs/...`. Caminho com `..` ou que sai do pacote não carrega.
- **Sozinha na linha**, a imagem vira um bloco: ampliada por número inteiro
  (2x, 3x, até 4x) para o pixel continuar quadrado, com moldura, e com o texto
  entre colchetes como legenda. A que é maior que a tela encolhe para caber.
- **No meio do texto**, vira um ícone do tamanho da letra. É o jeito de mostrar
  um item do seu mod numa frase.
- PNG, GIF (animado), WebP, JPG e BMP. Imagem que não existe mostra o texto
  alternativo.

#### Tamanho e alinhamento

Um `{...}` **colado** logo depois da imagem diz o tamanho e onde ela fica:

```markdown
![Chefe](docs/chefe.gif){width=200 align=center}

![](docs/mapa.png){width=50% align=right}

![](content/Content/Items/Espada.png){float=left width=96}
Este parágrafo fica **ao lado** da espada, e não embaixo dela.

Pegue a ![](content/Content/Items/Moeda.png){h=24} Moeda maior que a letra, e [i:757]{32} também.
```

| Dentro do `{}` | |
|---|---|
| `width=128` (ou `w=128`) | A largura, em pontos da tela (o "pixel" do launcher: a ficha tem uns 330 de largura no celular em pé). `px` e `dp` no fim são aceitos e dão no mesmo. |
| `width=50%` | A largura em porcentagem do texto. |
| `height=64` (ou `h=64`) | A altura. Só a largura **ou** só a altura mantém a proporção da imagem; as duas juntas esticam. |
| `128x64`, `128x`, `x64` | Largura e altura de uma vez. Um número sozinho (`{128}` ou `{50%}`) é a largura. |
| `align=left` / `center` / `right` | Onde a imagem sozinha na linha fica. Sem ele, à esquerda (ou no centro, dentro de `::: center`). Também vale só a palavra: `{right}`. |
| `float=left` / `right` | Põe a imagem **ao lado do bloco seguinte** (um parágrafo, uma lista, uma tabela). A largura é a do `width` (até 60% do texto); sem ele, 40%. |

- Os nomes em português também valem: `largura`, `altura`, `alinhar`,
  `flutuar`, `esquerda`, `centro`, `direita`.
- A imagem nunca passa da largura do texto: maior que isso, ela encolhe.
- Ampliada (maior que o arquivo), a imagem fica sem filtro, com o pixel
  quadrado; reduzida, ela é suavizada. Para pixel art nítida, use múltiplos do
  tamanho do arquivo (uma arte de 32 com `width=64` ou `width=96`).
- No meio do texto, só `width`/`height` em pontos valem (o `%` é ignorado); a
  linha cresce para caber a imagem.
- O `float` leva **um** bloco para o lado. Para pôr vários (dois parágrafos e
  uma lista), junte-os num `::: group` logo depois da imagem:

```markdown
![](docs/guia.png){float=right width=40%}
::: group
Primeiro parágrafo ao lado.

- e uma lista
- também ao lado
:::
```

### Blocos

````markdown
# Título grande        (com um fio de destaque embaixo)
## Título médio
### Título pequeno

- lista com marcador
  - sublista (recuo de 2 espaços)
1. lista numerada
- [x] tarefa feita
- [ ] tarefa por fazer

> Citação, com uma barra na cor de destaque.

---                    (linha separadora)

```js
// bloco de código, rola de lado
```

| Coluna | Outra |
|:---|---:|
| à esquerda | à direita |
````

Na tabela, `:---` alinha à esquerda, `---:` à direita e `:---:` no centro.

### Avisos

```markdown
> [!NOTE]
> Informação neutra.

> [!TIP]
> Uma dica.

> [!WARNING] Cuidado com o chefe
> Texto depois do [!...] vira o título.
```

| Tipo | Cor |
|---|---|
| `[!NOTE]` (ou `[!NOTA]`) | azul |
| `[!TIP]` (`[!DICA]`) | verde |
| `[!IMPORTANT]` (`[!IMPORTANTE]`) | roxo |
| `[!WARNING]` (`[!AVISO]`) | amarelo |
| `[!CAUTION]` (`[!CUIDADO]`) | vermelho |

### Contêineres

Uma linha com `:::` e o tipo abre um bloco especial; outra com só `:::` fecha:

```markdown
::: center
Tudo aqui fica **centralizado**, imagens inclusive.
:::

::: spoiler Como derrotar o chefe
Escondido até tocar no título.
:::

::: panel Requisitos
Um painel afundado, com título opcional.
:::

::: group
Só junta os blocos, sem desenhar nada: serve para pôr vários ao lado de
uma imagem com `float`.
:::
```

Dá para pôr um dentro do outro, e qualquer bloco (lista, tabela, imagem)
dentro deles.

### O que não entra

HTML (`<b>`, `<img>`, `<details>`) não é interpretado: aparece como texto. Os
comentários `<!-- ... -->` somem. Para o que o HTML faria, use os contêineres acima.

## Exemplos de verdade

Todos os mods de [`samples/`](../../samples) têm descrição, novidades, licença
e autores. O [`ExampleMod`](../../samples/ExampleMod) usa quase tudo deste guia:
quatro autores (três com foto), links, uma aba extra (`docs/receitas.md`) com
tabelas e um spoiler, avisos, cores e ícones de item no meio do texto.
