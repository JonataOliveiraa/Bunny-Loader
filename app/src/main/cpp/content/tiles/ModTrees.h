#pragma once

struct Il2CppObject;

namespace bl::runtime {

/**
 * Árvores de mod (ModTree): crescem como o tile 5 do jogo, e é o bloco de
 * baixo do tronco (o chão) que diz de quem são. O desenho roda em várias
 * threads, a cada quadro, e fica aqui:
 *
 *   - o tronco: TileDrawing.GetTileDrawTexture(TileDrawInfo, x, y, out pass)
 *     devolve a textura do mod quando o chão é de uma árvore de mod. O quadro
 *     já sai certo: o jogo soma 176 * (bioma + 1) e o bioma de um chão que ele
 *     não conhece é -1;
 *   - a copa e os galhos: WorldGen.GetCommonTreeFoliageData devolve o estilo
 *     da árvore de mod (o índice dela em TextureAssets.TreeTop/TreeBranch, que
 *     o JS cresce), com o chão achado pelo tronco.
 *
 * `trunk` (um Texture2D) fica preso por gchandle. Os ganchos entram na
 * primeira árvore registrada.
 */
void setModTree(int groundType, Il2CppObject* trunk, int topStyle);

/**
 * Chamado pelo hook do TileDrawing.GetTileDrawData (ModTiles.cpp) para cada
 * célula desenhada, com o quadro já calculado: marca nesta thread se a célula
 * é tronco de árvore de mod (o SpriteBatch.Draw seguinte troca a textura).
 */
void noteTreeDrawTile(int x, int y, int type, int frameX, int frameY);

} // namespace bl::runtime
