# Templo Pirâmide

O Templo da Selva assume a forma de uma pirâmide de tijolos Lihzahrd, inspirada na referência fornecida: estreita no topo, larga na base, com câmaras abobadadas em andares separados por grandes faixas de tijolos.

- Cinco andares em mundos pequenos, seis em médios e sete em grandes, contando a base. Se faltar profundidade, a quantidade é reduzida.
- Pisos principais separados por 64 a 70 blocos, com desníveis em algumas câmaras.
- Oito tipos de sala: colunatas, fossos de espinhos, anfiteatros, tesouros com nichos, salões altos, galerias baixas, corredores de armadilhas e ruínas com estrados. Larguras, alturas e tetos variam conforme a sala.
- Corredores horizontais e descidas em degraus alternando os lados a cada andar.
- Grande câmara central na base, com 170 a 210 blocos de largura, 58 a 68 de altura, colunas suspensas, sacadas e altar em um estrado.
- Entrada com a porta Lihzahrd trancada, montada diretamente com o estilo correto.
- Baús Lihzahrd em todos os andares, criados pela rotina de loot do jogo com Células de Energia. A quantidade planejada é 35% do número de câmaras, arredondada para cima.
- Circuitos de armadilhas planejados: placas Lihzahrd conectadas por fios vermelhos, azuis ou verdes a superdardos, lança-chamas, bolas de espinhos e lanças. Há também fossos com espinhos.
- Margem excedente do contorno externo 38% menor, preservando o formato de pirâmide. A espessura da base foi reduzida de 12 para 7 blocos.
- A etapa posterior `templePart2` do jogo continua podendo acrescentar decoração, móveis e perigos.

## Como usar

1. Importe o pacote na aba **Pacotes** do Bunny Loader e ative **Templo Pirâmide**.
2. Abra ou reinicie o jogo.
3. **Crie um mundo novo**. O mod altera a geração do templo; mundos existentes mantêm o templo que já têm.

A posição é escolhida pelo Terraria na Selva. O mod ajusta o desenho para ficar dentro dos limites do mundo e acima do Submundo.

O gerador original de `makeTemple` nunca é chamado. Se ocorrer um erro, ele é registrado no log e fica contido no mod, sem gerar outro templo sobre a pirâmide.

Use apenas uma versão do gerador: o pacote ou o script do Editor. Outros mods que substituam `makeTemple` também disputam essa geração.
