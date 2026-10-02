# 1.1.0

Adiciona baús por andar com Células de Energia e loot do próprio jogo, circuitos com placas Lihzahrd e fios de três cores, superdardos, lança-chamas, bolas de espinhos, lanças e fossos com espinhos.

Substitui as câmaras repetidas por oito tipos, com larguras, alturas, abóbadas, pisos, galerias e nichos variados.

Reduz 38% da margem excedente do contorno externo; a base de 12 blocos passa para 7, arredondando a mesma proporção. Mantém a silhueta de pirâmide e as passagens.

Remove toda chamada ao gerador original, inclusive em caso de erro. As exceções ficam contidas no gerador, pois a ponte executa o original automaticamente se uma exceção escapar do callback.

A porta Lihzahrd é montada diretamente como um multitile 1x3, com o enquadramento do estilo 11 usado por `WorldGen.PlaceDoor`, eliminando a falha de `PlaceTile` que acionava a geração do templo padrão por cima da pirâmide.

# 1.0.1

Corrige o acesso aos tiles durante a geração: `get_Item` recebe apenas as coordenadas, com a instância no receptor da chamada. Elimina o erro `System.Int32 espera um numero, recebeu um objeto do jogo`.

O teste de integração agora rejeita a passagem duplicada da instância e verifica também a chamada com receptor explícito.
