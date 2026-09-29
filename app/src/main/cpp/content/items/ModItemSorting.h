#pragma once

namespace bl::runtime {

/**
 * Os itens de mod no botao "Ordenar" do inventario e do bau (ItemSorting).
 *
 * Cada camada de ordenacao (Armas - Corpo a corpo, Ferramentas...) so aceita
 * os tipos da sua lista branca, que ItemSorting.SetupWhiteLists monta de -48
 * ate ItemID.Count. Item de mod nao cai em lista nenhuma, sobra no fim do
 * Sort sem contagem de camada, e o `_sort_counts[0]` lanca
 * ArgumentOutOfRange DEPOIS de o jogo ter esvaziado os slots: o item some.
 * O tModLoader troca o limite do SetupWhiteLists por ItemLoader.ItemCount;
 * aqui os tipos de mod sao classificados pelas mesmas camadas e somados as
 * listas, na instalacao e de novo a cada SetupWhiteLists (o celular o chama
 * duas vezes no carregamento, e ele limpa as listas).
 *
 * Na thread do jogo, depois de os tipos estarem nas tabelas (SetDefaults).
 */
void installModItemSorting(int from, int to);

} // namespace bl::runtime
