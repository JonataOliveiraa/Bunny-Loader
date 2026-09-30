#pragma once

namespace bl::runtime {

/**
 * Tile de mod no mundo salvo, sem corromper o `.wld`.
 *
 * O mundo gravado NUNCA leva tipo de mod: o WorldFile.SaveWorldTilesFast le
 * uma COPIA das tabelas de definicao em que o tipo de mod e ar — tipo 0,
 * inativo, com parede, liquido e fios intactos. O mundo de verdade nao muda:
 * a gravacao automatica roda numa thread com o jogo andando, e trocar as
 * definicoes vivas fazia os tiles de mod sumirem ~0,5 s (o jogador caia
 * dentro deles). As posicoes vao para `<mundo>.wld.tiles.bl`, pela chave
 * "<uid>/<nome>" (o id muda com a ordem dos mods). Ao carregar (WorldFile.LoadWorldTiles), cada tile
 * volta pelos setters do proprio jogo, que cuidam do internamento.
 *
 * Sem o mod: o mundo abre no jogo puro (ali ha ar), e a entrada fica no
 * arquivo para quando o mod voltar. Se alguem construiu no lugar enquanto
 * isso, vale o que foi construido.
 */
void installModTileSave();

} // namespace bl::runtime
