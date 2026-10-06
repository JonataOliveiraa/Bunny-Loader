# Pacotes de textura

Um pacote de textura substitui sprites vanilla usando somente PNGs. Nao precisa
de `main.js`, classe `Mod` nem codigo JavaScript.

```text
MeuPack/
  manifest.json
  icon.png
  content/
    Images/
      Item_1.png
      NPC_4.png
      Projectile_1.png
      Tiles_0.png
      Wall_1.png
      Buff_1.png
      Armor_Head_1.png
```

```json
{
  "uid": "19a01ecc-063f-3e40-a704-ace60cf3b483",
  "id": "meupack",
  "name": "Meu Pack",
  "version": "1.0.0",
  "author": "Seu nome",
  "type": "texture",
  "category": "Texturas",
  "blVersion": 2
}
```

O UUID do exemplo pertence ao pack de teste Un-mechanical Bosses: use um UUID
proprio para um pacote local e a identidade emitida pelo site ao publicar.
Descricao, icone, autores e links usam os mesmos campos dos outros pacotes.

| Arquivo relativo a `content/Images/` | Tabela no jogo |
|---|---|
| `Item_N.png` | `TextureAssets.Item[N]` |
| `NPC_N.png` | `TextureAssets.Npc[N]` |
| `Projectile_N.png` | `TextureAssets.Projectile[N]` |
| `Tiles_N.png` | `TextureAssets.Tile[N]` |
| `Wall_N.png` | `TextureAssets.Wall[N]` |
| `Buff_N.png` | `TextureAssets.Buff[N]` |
| `Gore_N.png` | `TextureAssets.Gore[N]` |
| `Armor_Head_N.png` | `TextureAssets.ArmorHead[N]` |
| `Armor_Body_N.png` | `TextureAssets.ArmorBody[N]` |
| `Armor_Arm_N.png` | `TextureAssets.ArmorArm[N]` |
| `Armor_Legs_N.png` | `TextureAssets.ArmorLeg[N]` |
| `Female_Body_N.png` | `TextureAssets.FemaleBody[N]` |
| `Armor/Armor_N.png` | `TextureAssets.ArmorBodyComposite[N]` |

`N` e o numero vanilla. Exemplos: `Item_1` e a Picareta de Cobre,
`NPC_4` e o Olho de Cthulhu, `Tiles_0` e terra. Use os nomes e a capitalizacao
do resource pack original. Arquivos em `Misc/` e `UI/Bestiary/` nao substituem
NPCs, mesmo quando se chamam `NPC_N.png`.

O loader tambem procura o **caminho completo** dos outros assets de textura,
incluindo campos individuais, outras tabelas e assets guardados pelo repositorio.
Isso permite substituir `Extra_N`, `Background_N`, `Wings_N`, acessorios,
cabecas de NPCs, arvores, inventario, partes de chefes e imagens em subpastas:

```text
content/Images/Inventory_Back.png
content/Images/Arm_Bone_2.png
content/Images/Bone_Eyes.png
content/Images/Background_325.png
content/Images/UI/Minimap/Default/MinimapFrame.png
content/Images/UI/PlayerResourceSets/FancyClassic/Heart_Fill.png
content/Images/TownNPCs/Wizard_Default.png
```

O caminho deve corresponder ao `Name` do asset pedido pelo jogo. A busca por
nome ignora maiusculas/minusculas e normaliza as barras; nao reduz subpastas
ao nome do arquivo. Imagens exclusivas do PC, alternativas em `Misc/` e
elementos da interface que o mobile desenha de outra maneira podem ficar sem
efeito. Aplicar uma moldura de minimapa do PC nao adapta as medidas ou botoes
da interface mobile; o PNG pode exigir ajustes de arte para ficar alinhado.

Na aba Pacotes, o pacote mais acima ganha **por arquivo**. Um PNG que falha
na carga permite usar o mesmo arquivo de um pacote abaixo; sem substituicao
valida, continua o asset original. Indices fora da tabela deixam um aviso.

Os PNGs sao lidos na carga do jogo, depois de as tabelas estarem prontas,
com filtro de pixel e sem repeticao nas bordas. PNGs sobrepostos de pacotes
abaixo nao sao decodificados. A memoria aumenta com os sprites substituidos:
as texturas por nome que ainda nao foram pedidas sao decodificadas na primeira
chamada a `Asset<Texture2D>.ActionUnityLoad` na thread do jogo. Essa carga usa
o PNG diretamente. Os sprites das 13 tabelas acima e os assets por nome ja
existentes continuam sendo aplicados na carga; a carga sob demanda de todos
os sprites de um pack grande ainda nao esta implementada.

Mantenha as dimensoes originais da folha. O loader compara com a largura e
altura logicas do sprite vanilla (nao o tamanho da pagina Unity) e registra
`size mismatch` em ingles quando diferem. E um aviso: o PNG ainda e aplicado.
Se o asset original ainda nao tem `Value`, `Asset.ActionUnityLoad` prepara o
Texture2D logico pelo ContentManager, que conhece o atlas e SourceAssetEntry.
A leitura de Width/Height nao pede a pagina Unity usada no desenho. Se essas
dimensoes nao estiverem disponiveis, o log informa que nao pode conferir.
Pedidos futuros por nome que ainda nao possuem Value recebem o PNG sem
carregar o vanilla primeiro; nesse caso o aviso informa que as dimensoes
originais nao foram verificadas.

As escolhas de pacotes sao aplicadas ao abrir/reiniciar o jogo. Para desligar,
volte ao launcher, desative o pacote e abra o jogo de novo. Os assets originais
sao preservados com referencias do GC enquanto existe substituicao.
Nas substituicoes por nome, o objeto Asset permanece o mesmo, preservando
referencias guardadas pela interface. O valor, a fonte e o estado anterior
sao restaurados antes de liberar a textura Unity criada pelo pacote.

## Importar e empacotar

Para importar um pack do PC, escolha seu ZIP com `pack.json`, `icon.png` e
`Content/Images/`. O launcher converte nome, autor, descricao e versao para
`manifest.json`, define `type: "texture"` e normaliza `Content/` para `content/`.
Tambem aceita esses arquivos dentro de uma pasta no ZIP. Se houver
`workshop.json`, o ID do Workshop preserva a identidade entre atualizacoes;
sem ele, a identidade local deriva do nome e autor. Um `manifest.json` existente
tem prioridade e nunca e recriado pelo conversor.

Para empacotar a pasta do Bunny Loader:

```powershell
python tools/mods/pack.py "ports/textures/Un-mechanical Bosses/bl"
```

O `.bl` sai em `build/mods-packs/`. O mesmo conteudo gera o mesmo arquivo.

Para portar imagens de um resource pack instalado no Workshop local:

```powershell
python tools/mods/port_textures.py 2439853873 2796830227
```

O comando copia os PNGs para `ports/textures/<nome>/bl`, cria o manifesto,
preserva autoria, metadados e icone e gera o `.bl`. Nao altera a pasta Steam.
O UID deriva do ID do Workshop, como no importador do launcher; um port ja
existente mantem sua identidade. O inventario dos packs examinados esta em
`ports/textures/README.md`.

Localizacao, sons e musicas nao sao aplicados por esse carregador. O comando
de port copia somente imagens; `type: "font"` fica para uma implementacao futura.
Compatibilidade de nomes e dimensoes nao garante que todas as imagens do PC
tenham um elemento equivalente visivel na interface mobile.
