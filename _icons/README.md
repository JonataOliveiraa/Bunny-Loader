# _icons/ — texturas de origem

Aqui ficam os arquivos **de onde saem** os recursos do app, na resolução e no
recorte originais. O app não lê nada daqui: quem ele usa é
`app/src/main/res/drawable-nodpi/`, para onde a textura é copiada quando passa
a ser usada de verdade.

Os dois lados nomeiam por critérios diferentes, de propósito:

- **aqui, pelo que a imagem É** (`arvore.png`, `nuvem_coelho.png`), porque o
  material sobrevive à tela que o usa;
- **em `res/`, pelo PAPEL** (`ic_tab_inicio`, `ic_start`), porque é assim que o
  código pede.

Foi o que faltava quando a aba Perfil virou Configurações e o recurso continuou
`ic_tab_perfil`: papel mudou, nome não.

```
launcher/   interface do launcher: abas, botões, título
menu/       menu de cheats dentro do jogo
sprites/    coisas animadas: coelho, moeda, girassol
tiles/      atlas de blocos (18x18: 16px de tile + 2px de borda)
fonte/      a fonte pixelada
```

## Esta pasta não vai para o git

O conteúdo é arte do Terraria, extraída dos assets do jogo. Fica no
`.gitignore` — só este README é versionado. O que já está em `res/` entrou
antes desta regra existir.
