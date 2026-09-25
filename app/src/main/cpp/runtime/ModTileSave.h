#pragma once

namespace bl::runtime {

/**
 * Tile de mod no mundo salvo, sem corromper o `.wld`.
 *
 * O mundo gravado NUNCA leva tipo de mod: na hora de gravar os tiles
 * (WorldFile.SaveWorldTilesFast), as definicoes de tipo de mod viram ar — tipo
 * 0, inativo, com parede, liquido e fios intactos — e voltam logo depois. As
 * posicoes vao para `<mundo>.wld.tiles.bl`, pela chave "<uid>/<nome>" (o id
 * muda com a ordem dos mods). Ao carregar (WorldFile.LoadWorldTiles), cada tile
 * volta pelos setters do proprio jogo, que cuidam do internamento.
 *
 * Sem o mod: o mundo abre no jogo puro (ali ha ar), e a entrada fica no
 * arquivo para quando o mod voltar. Se alguem construiu no lugar enquanto
 * isso, vale o que foi construido.
 */
void installModTileSave();

} // namespace bl::runtime
