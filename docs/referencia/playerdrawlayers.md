# PlayerDrawLayer

Classes exportadas em `Content/`, `Common/` ou no arquivo de entrada do mod
podem estender `PlayerDrawLayer`. O registro ocorre automaticamente, após
os itens e equipamentos e antes de `Mod.Load`. Cada classe possui um
template próprio, acessível por `ModContent.GetInstance(Classe)`.

```js
export class MyLayer extends PlayerDrawLayer {
    GetDefaultPosition() {
        return PlayerDrawLayer.AfterParent(PlayerDrawLayers.Wings);
    }

    GetDefaultVisibility(drawInfo) {
        const player = drawInfo.drawPlayer;
        return !player.dead && !player.invis && player.wings > 0;
    }

    Draw(drawInfo) {
        const drawData = Terraria.DataStructures.DrawData.new();
        drawData['void .ctor(Texture2D texture, Vector2 position, Rectangle sourceRect, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effect, int inactiveLayerDepth)'](
            Terraria.GameContent.TextureAssets.MagicPixel.Value,
            Vector2.new(drawInfo.Position.X - Terraria.Main.screenPosition.X, drawInfo.Position.Y - Terraria.Main.screenPosition.Y),
            Rectangle.new(0, 0, 1, 1), Color.new(255, 255, 255, 255),
            0, Vector2.new(0, 0), 8, drawInfo.playerEffect, 0);
        drawData.shader = drawInfo.cWings;
        ModPlayer.AddDrawData(drawInfo, drawData);
    }
}
```

## Métodos e estado

| Membro | Contrato |
|---|---|
| `GetDefaultPosition()` | Obrigatório. Retorna `BeforeParent(pai)` ou `AfterParent(pai)`. Consultado na construção do plano, com resultado em cache. |
| `GetDefaultVisibility(drawInfo)` | Padrão `true`. Consultado em cada desenho. `false` suprime a camada e seus filhos. |
| `Draw(drawInfo)` | Obrigatório. Executado no ponto do pai nativo ou personalizado. Use o cache para desenhar. |
| `SetStaticDefaults()` | Executado uma vez com conteúdo pronto. Registro tardio executa-o antes do primeiro desenho. |
| `Hide()` | Suprime a camada e seus filhos no desenho atual. |
| `Visible` | Getter da visibilidade própria. Um pai oculto também impede a execução. |
| `Name` | Por padrão, o nome da classe. `super('Nome')` permite outro nome. |
| `FullName` | `uuid-do-mod/Name`, ou apenas o nome para descritores nativos. |
| `Mod` | Mod responsável pelo registro. |
| `BeforeParent(pai)` / `AfterParent(pai)` | Cria uma posição antes/depois do pai. |
| `register(Classe)` | Registro manual. Valida os métodos obrigatórios, rejeita duplicatas e retorna o template. |

O pai pode ser um descritor de `PlayerDrawLayers`, uma classe personalizada
registrada, sua instância ou seu `FullName`. Nomes simples são aceitos
quando identificam uma camada sem ambiguidade. Camadas do mesmo mod precisam
ter nomes distintos. Classes com `static Autoload = false` não são
registradas automaticamente e podem chamar `PlayerDrawLayer.register`.

Filhos são desenhados na ordem de registro: filhos anteriores, o pai,
filhos posteriores. O mesmo mecanismo aceita filhos de camadas
personalizadas e dos pontos nativos aninhados, como `FrontAccFront`.
Ocultar o pai suprime toda a árvore. Visibilidades são restauradas ao terminar.
O renderer fecha o acesso aos hooks de camada fora desse desenho. Chamadas
aninhadas restauram os filtros do desenho anterior.

`Draw` pode ser chamado para pós-imagens. Use `drawInfo.shadow === 0` na
visibilidade quando o efeito deve aparecer somente no jogador principal.
Falhas de callbacks são reportadas uma vez e isoladas. Dados válidos já
adicionados antes de uma falha permanecem no cache. Pais desconhecidos,
posições inválidas e ciclos desativam as ramificações afetadas com aviso.

## Ordenação por jogador

`ModPlayer.ModifyDrawLayerOrdering(player, positions)` recebe um `Map`
inicializado com as posições das camadas personalizadas. Use descritores,
classes, instâncias ou nomes como chaves. As posições pertencem ao desenho
atual; modificá-las não altera os padrões nem o desenho de outro jogador.

```js
ModifyDrawLayerOrdering(player, positions) {
    positions.set(MyLayer, PlayerDrawLayer.BeforeParent(PlayerDrawLayers.Head));
}

HideDrawLayers(player, drawInfo) {
    ModContent.GetInstance(MyLayer).Hide();
}
```

Remover a posição de uma instância com `positions.delete(instance)` suprime
sua árvore naquele desenho. Quando uma camada nativa é reordenada, seus
filhos personalizados são movidos junto com os dados produzidos por ela.
Relações entre camadas nativas e filhos personalizados são resolvidas pelo
pai nativo da árvore, preservando o grupo completo.

## Glowmask de asas

O [exemplo WingsGlowmask](../../samples/ExampleMod/content/Common/Players/WingsGlowmask.js)
implementa textura e cor por slot, posição junto das asas, quadro de
animação e shader `cWings`. Registre os dados com conteúdo pronto, por
exemplo no `PostSetupContent` do mod:

```js
import { WingsGlowmask } from './Common/Players/WingsGlowmask.js';
import { ExampleWings } from './Content/Items/Accessories/ExampleWings.js';

PostSetupContent() {
    WingsGlowmask.RegisterData(ModContent.GetInstance(ExampleWings).Item.wingSlot, {
        Texture: ModContent.Request('Content/Items/Accessories/ExampleWings_Wings_Glow').Value,
        Color: Color.new(255, 255, 255, 255),
        Frames: 4
    });
}
```

Forneça sua textura `_Wings_Glow`; ela não está incluída no ExampleMod.
`Frames` precisa corresponder à animação. O deslocamento padrão
`{ X: -9, Y: 2 }` reproduz a geometria do exemplo apresentado e pode ser
substituído por `Offset`. Asas com desenho especial podem precisar de outro
deslocamento. `RegisterData` conserva a primeira configuração de cada slot.

O dado inserido deve ser o `DrawData` construído para a textura, e não o
`Ref` que contém a configuração. `ModPlayer.AddDrawData` atualiza o contador
nativo. O renderer aplica transformações, ordem e shaders após montar as
camadas. Chamar `DrawData.Draw(Main.spriteBatch)` diretamente antecipa o
desenho e evita esse fluxo.

## Compatibilidade e custo

O comportamento foi conferido na cópia local do tModLoader, em
`PlayerDrawLayer.cs`, `PlayerDrawLayer.Positions.cs` e
`ExamplePlayerDrawLayer.cs`. A
[fonte pública](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/PlayerDrawLayer.cs)
descreve o mesmo contrato de filhos e cache. O Bunny Loader usa o
`DrawData[]` móvel com contador, em vez de `List<DrawData>`.

O plano padrão é reconstruído quando novas camadas são registradas.
Visibilidade e ordenação dinâmica são avaliadas por desenho. Apenas os
hooks nativos que precisam executar filhos, ocultar uma camada ou registrar
dados para reordenação entram no JavaScript. Uma camada anexada somente às
asas ativa o hook das asas. Camadas invisíveis não ativam hooks dos seus pais.
Os filtros individuais são atualizados somente quando o conjunto de
camadas ativas muda. Fora do renderer, a condição nativa `whileIn` mantém
os hooks fora do JavaScript mesmo quando seus filtros estão em cache.
A inserção no cache usa o array existente. Reordenação nativa reutiliza a
cópia em blocos e só clona o trecho ativo quando a ordem efetiva muda.

Esta implementação cobre `BeforeParent`, `AfterParent`, visibilidade,
desenho, registro e ordenação compartilhada com ModPlayer. `Between`,
`Multiple`, `Transformation`, `IsHeadLayer` e o desenho do ícone de minimapa
não estão implementados.

Testes e resultados: [suíte PlayerDrawLayer](../../tools/tests/playerdrawlayers/README.md).
