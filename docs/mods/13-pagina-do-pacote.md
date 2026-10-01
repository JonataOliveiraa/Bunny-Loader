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
  banner.png          capa da ficha, ~3,4:1 (ex.: 384x112)
  thumbnails/         imagens extras, no pé da Descrição (opcional)
  docs/               o que mais você quiser mostrar (opcional)
```

A ficha fica assim, de cima para baixo:

1. a **capa** (`banner.png`), com voltar e favoritar;
2. o **cabeçalho**: ícone, nome, as fotos e os nomes dos autores, a categoria,
   a ficha técnica (versão, data, tamanho, licença), os botões (baixar, ligar,
   remover, exportar) e os **links** do manifesto;
3. as **abas**: Descrição, Novidades, Licença, Créditos e as que você criar.
   Descrição aparece sempre; as outras dependem dos arquivos e campos do pacote.

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
    "heading": "#FFFFFF"
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
| `theme` | **Novo.** As cores da ficha deste mod (abaixo). |
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
(`"Bunny Loader"` → `authors/bunny-loader.png`); também aceita `.jpg`, `.jpeg` e
`.webp`. Sem foto nenhuma, aparece a inicial do nome.

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

Todas são `#RRGGBB`, `#AARRGGBB` ou `#RGB`. Uma cor que falta (ou que não se
lê) fica a do launcher, então dá para mudar só o `accent`. O tema vale **só
dentro da ficha do seu mod**; a lista e as outras telas seguem a paleta do app.

> [!TIP]
> Escolha um `panel` escuro: o texto tem contorno preto e branco por cima, e
> num painel claro ele fica pesado.

## `description.md`

A aba Descrição. É Markdown comum (a [lista inteira](#o-markdown) está mais
abaixo). As imagens de `thumbnails/` aparecem no fim dela.

Se o pacote não tem `description.md`, a aba mostra o `description` do
manifesto, e se ele também não existe, o `summary`.

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
- PNG, JPG e WebP. Imagem que não existe mostra o texto alternativo.

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
