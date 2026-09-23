# _sprites/ — sprites do jogo, por ID

Extraidos de um dump de imagens do Terraria 1.4.5 e renomeados para o **ID**,
que e a chave que o menu de cheats tem em maos:

```
item/98.png     Item_98.png    -> Minishark
npc/50.png      NPC_50.png     -> King Slime
```

`item/` tem 6083 arquivos e `npc/` 697 — 3,9 MB somados. O nome de cada um NAO
vem daqui: vem do proprio jogo, pela Localization (`Lang.GetItemNameValue`),
entao sai no idioma do aparelho e nunca desatualiza.

Por que existe: as texturas do jogo vivem so na GPU. Medido no aparelho — o
atlas de itens e 2048x2048 com `isReadable = 0`, `GetRawImageDataSize()` zero e
`GetWritableImageData()` nulo, e os desvios (`Graphics.Blit`, `ReadPixels`,
`GetNativeTexturePtr`) foram removidos deste binario pelo IL2CPP. Nao ha pixel
para ler em tempo de execucao; ou o JOGO desenha, ou a imagem vem de fora. Esta
pasta e o "de fora".

## Esta pasta nao vai para o git

E arte do Terraria. Fica no `.gitignore`, como `_icons/` — so este README e
versionado. O Gradle copia o conteudo para `assets/sprites/` no APK.
