#pragma once

namespace bl::runtime {

/**
 * Estilos de agua de mod (ModWaterStyle): numeros depois dos 15 do jogo.
 *
 * O Main.DrawWaters do celular anda pelos estilos em dois lacos com o 15
 * compilado: o que sobe e desce o Main.liquidAlpha e monta a lista
 * activeLiquidAlpha (a agua atras de rampas e meios blocos, no TileDrawing), e
 * o que desenha os estilos que estao sumindo. O primeiro indexa o array pelo
 * endereco (`cmp x, #15 + 8`, 8 = o cabecalho em floats) e sai no b.ne; o
 * segundo testa no comeco (`cmp x, #15; b.eq`). Trocando os dois, o proprio
 * jogo faz o fade e o desenho dos estilos de mod, como o laco
 * `WaterStylesLoader.TotalCount` do tModLoader.
 *
 * As tabelas por estilo (texturas, liquidAlpha, activeLiquidAlpha) crescem no
 * JS (Loaders/WaterStyleLoader.js) ANTES desta chamada: o laco maior indexa
 * alem do fim se elas ainda tiverem 15.
 */
constexpr int kVanillaWaterStyleCount = 15;   // Main.maxLiquidTypes

/**
 * Os lacos do Main.DrawWaters passam a ir ate `total` (>= 15). Pode ser
 * chamado de novo com um total maior. Na thread do jogo. Devolve false se o
 * codigo nao for o esperado (outra versao do jogo): nada e escrito.
 */
bool setWaterStyleCount(int total);

} // namespace bl::runtime
