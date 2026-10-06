# Validacao dos ports de textura

Executada em 04/10/2026 no emulador `127.0.0.1:16384`, com a build Android de debug do Bunny Loader.

| Verificacao | Resultado |
|---|---|
| Inventario local | 24 resource packs; nenhum `.tmod` |
| Ports preparados | 12 packs, 1.524 PNGs; nenhum `main.js` |
| Integridade | PNGs identicos aos arquivos do Workshop; `.bl` deterministico |
| Build e testes Kotlin | Gradle assembleDebug e testDebugUnitTest passaram |
| Teste nativo | Prioridade, arquivo invalido, indices, isolamento de subpastas/symlinks, troca de selecao, restauracao de valor/fonte/estado e liberacao de handles passaram |
| Packs juntos no menu | 1.040 verificacoes, nenhuma falha |
| Packs juntos num mundo clonado | 1.040 verificacoes, nenhuma falha |
| Pack sobreposto | 3 verificacoes, nenhuma falha; PNG de minimapa invalido deixou o pack abaixo ser aplicado |
| Ordem invertida | 3 verificacoes, nenhuma falha; inventario voltou a imagem do pack acima |
| Texturas desativadas | 5 verificacoes, nenhuma falha; assets vanilla e referencia do inventario conferidos |

Total: 2.091 verificacoes no dispositivo. Os testes examinam dimensoes, a textura Unity, a ausencia de fonte/atlas vanilla nas substituicoes e a preservacao da identidade dos Assets individuais. Para as cargas futuras por nome, criam um Asset com nome inicializado e chamam ActionUnityLoad duas vezes, conferindo que a substituicao continua valida.

Na combinacao usada no teste, o loader aplicou 991 sprites das tabelas iniciais e 161 assets por nome durante a carga. Outros caminhos ficaram indexados para pedidos futuros. Essa contagem considera a prioridade por arquivo entre os packs, portanto nao e a soma das imagens de cada port.

A captura do inventario confirmou sprites e componentes da interface em jogo. Nem todos os arquivos de interface do PC aparecem no mobile. Molduras de minimapa, telas e componentes com medidas diferentes podem exigir adaptacao da arte; os ports preservam os PNGs originais. Fonte, traducao e musica continuam fora do carregador de texturas. Ainda nao ha carga sob demanda de todas as tabelas de um pack grande.

Logs e capturas locais:

- `build/workshop-textures/{ports,world,overlay,reverse,vanilla}.log`
- `build/workshop-textures/{world,inventory}.png`
- `build/workshop-textures/build.log`

O mundo e o jogador usados no teste eram copias. As configuracoes originais foram restauradas; os novos ports ficaram instalados e desativados. A fixture, o pack sobreposto e as copias de teste foram removidos.

## Creditos, banners e navegacao

Os 12 `.bl` foram reempacotados com banner e avatar Steam. CRC, manifesto, links de todos os autores e as 1.524 imagens originais foram conferidos novamente. O Legacy mostra os tres criadores listados na descricao original.

Build e 60 testes Kotlin passaram. No emulador, a lista continua em Texturas apos voltar pela seta da capa ou pelo botao Voltar do Android. A selecao tambem permanece ao trocar entre Pacotes e Explorar. O toque no credito abriu `https://steamcommunity.com/sharedfiles/filedetails/?id=2439853873` no navegador, confirmado na barra de endereco. Estados ligado/desligado e ordem dos pacotes permaneceram inalterados.

Capturas e verificacao: `build/texture-credits/{detail-banner,credits-steam,legacy-credits,textures-cover-back,textures-android-back,steam-link-browser}.png` e `build/texture-credits/packs-verified.json`.
