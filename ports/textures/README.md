# Resource packs locais do Steam Workshop

Inventario de `C:/Program Files (x86)/Steam/steamapps/workshop/content/105600`, em 04/10/2026. Os 24 diretorios examinados sao resource packs (`pack.json`); nenhum contem `.tmod`. Os originais do Steam foram preservados.

Os ports ficam em `<nome>/bl`, com `type: "texture"`, autoria e link do original. Nao exigem `main.js`. Os `.bl` ficam em `build/mods-packs/`. Fonte, traducao e musica sao classificados separadamente e nao sao aplicados pelo carregador de imagens.

Foram preparados 12 ports com 1.524 PNGs. Veja [a validacao](VALIDATION.md) para os testes e limites do suporte.

| Pack | Conteudo | PNGs | Port local |
|---|---|---:|---|
| [Legacy Texture Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=2439853873) | Texturas | 276 | [Legacy Texture Pack](<Legacy Texture Pack/README.md>) |
| [Custom Fonts (Pescadero)](https://steamcommunity.com/sharedfiles/filedetails/?id=2439864365) | Fonte | 0 | Nao preparado |
| [Stormdark UI](https://steamcommunity.com/sharedfiles/filedetails/?id=2448259079) | Texturas | 161 | [Stormdark UI](<Stormdark UI/README.md>) |
| [Nep Minimap](https://steamcommunity.com/sharedfiles/filedetails/?id=2461637113) | Texturas | 4 | [Nep Minimap](<Nep Minimap/README.md>) |
| [Witch NPC](https://steamcommunity.com/sharedfiles/filedetails/?id=2463649580) | Texturas, Traducao | 10 | [Witch NPC](<Witch NPC/README.md>) |
| [Blue's Textures](https://steamcommunity.com/sharedfiles/filedetails/?id=2493507652) | Texturas | 939 | [Blue's Textures](<Blue's Textures/README.md>) |
| [Polaris Texture Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=2699930018) | Texturas, Traducao | 2721 | Nao preparado |
| [Kokomi Minimap](https://steamcommunity.com/sharedfiles/filedetails/?id=2781929285) | Texturas | 1 | [Kokomi Minimap](<Kokomi Minimap/README.md>) |
| [Rose Inventory](https://steamcommunity.com/sharedfiles/filedetails/?id=2796830227) | Texturas | 12 | [Rose Inventory](<Rose Inventory/README.md>) |
| [Leorio Minimap](https://steamcommunity.com/sharedfiles/filedetails/?id=2819103249) | Texturas | 4 | Nao preparado |
| [Terraria Korean Translation](https://steamcommunity.com/sharedfiles/filedetails/?id=2855376675) | Traducao | 0 | Nao preparado |
| [Dark & Blue Titanium](https://steamcommunity.com/sharedfiles/filedetails/?id=2890641669) | Texturas | 34 | Nao preparado |
| [Moonlit UI](https://steamcommunity.com/sharedfiles/filedetails/?id=3122137229) | Texturas, Fonte | 67 | Nao preparado |
| [ULTRAKILL V1 and V2](https://steamcommunity.com/sharedfiles/filedetails/?id=3198525611) | Texturas | 16 | [ULTRAKILL V1 and V2](<ULTRAKILL V1 and V2/README.md>) |
| [向导的娘化材质包 Guida's Girls Texture Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=3339304829) | Texturas | 850 | Nao preparado |
| [Moonlit UI Addon](https://steamcommunity.com/sharedfiles/filedetails/?id=3513338148) | Texturas | 24 | [Moonlit UI Addon](<Moonlit UI Addon/README.md>) |
| [Astolfo minimap](https://steamcommunity.com/sharedfiles/filedetails/?id=3542118536) | Texturas | 17 | Nao preparado |
| [Teto MiniMap](https://steamcommunity.com/sharedfiles/filedetails/?id=3554412315) | Texturas | 4 | Nao preparado |
| [Boss-Colored Relics (Remastered!)](https://steamcommunity.com/sharedfiles/filedetails/?id=3661349204) | Texturas | 29 | [Boss-Colored Relics (Remastered!)](<Boss-Colored Relics (Remastered!)/README.md>) |
| [Redesigned A field Of Sakura Trees And Crystals In Between Rivers](https://steamcommunity.com/sharedfiles/filedetails/?id=3760991062) | Texturas | 3 | [Redesigned A field Of Sakura Trees And Crystals In Between Rivers](<Redesigned A field Of Sakura Trees And Crystals In Between Rivers/README.md>) |
| [Calamity Texture Pack +](https://steamcommunity.com/sharedfiles/filedetails/?id=3762074948) | Texturas, Traducao | 4740 | Nao preparado |
| [Portrait Styled NPCs](https://steamcommunity.com/sharedfiles/filedetails/?id=3786545374) | Texturas | 103 | Nao preparado |
| [Un-mechanical Bosses](https://steamcommunity.com/sharedfiles/filedetails/?id=3798222774) | Texturas, Traducao, Musica | 49 | [Un-mechanical Bosses](<Un-mechanical Bosses/README.md>) |
| [Better Witch Portrait](https://steamcommunity.com/sharedfiles/filedetails/?id=3811361498) | Texturas | 1 | Nao preparado |

## Compatibilidade

O loader cobre as tabelas de sprites e os assets encontrados pelo caminho completo, inclusive subpastas, inventario, partes de chefes, asas, extras, fundos, arvores e imagens de minimapa. Elementos exclusivos do PC ou que o mobile desenha de outra maneira podem ficar sem efeito. As molduras do PC podem precisar de ajustes para alinhar com o minimapa mobile; nao foram redimensionadas nem redesenhadas.

Os PNGs permanecem identicos aos originais. Um aviso `size mismatch` nao impede a carga. Blue's Textures e Legacy incluem sprites de tamanhos diferentes; revise os avisos antes de escolher um pack para jogar.

As 13 tabelas iniciais e os assets por nome ja existentes sao aplicados na carga. Outros assets por nome recebem o PNG na primeira chamada de ActionUnityLoad na thread do jogo. A carga sob demanda de todas as tabelas de packs grandes ainda esta pendente.

Para repetir o port: `python tools/mods/port_textures.py <ID-do-Workshop>`. Esse comando copia somente as imagens; os conteudos adicionais ficam na pasta original do Steam.

Os 12 ports incluem `banner.png` com a arte original do Workshop e creditos em `authors`, com o icone da Steam e o link do pacote original. O Legacy credita Stryke, Sylvium e TiredGhostDude. Os `.bl` atualizados ficam em `in bl/<nome do pacote>/`.
