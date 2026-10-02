# Histórico

Registros de como o Bunny Loader chegou onde está: decisões, investigações e
medições. Ficam aqui como foram escritos, com o que se sabia **na época**. Onde
algo mudou depois, há uma nota no próprio documento; para o estado atual, vale
a [documentação do núcleo](../nucleo/README.md).

| Documento | O que registra |
|---|---|
| [UNITY-HOSTING.md](UNITY-HOSTING.md) | O que o `classes.dex` do Terraria revelou sobre hospedar a `UnityPlayer`, e o muro do PairIP. |
| [DECISAO-ARQUITETURA.md](DECISAO-ARQUITETURA.md) | Os caminhos avaliados para pôr a `libbunny.so` dentro do jogo, e o que foi medido em cada um (inclusive a descoberta de que o hook funciona no emulador). O desenho final foi outro: o runtime do jogo integrado ao próprio app (ver [núcleo](../nucleo/README.md#o-processo)). |
| [AVALIACAO-PONTE-E-CRASH.md](AVALIACAO-PONTE-E-CRASH.md) | A primeira medição da ponte JS → IL2CPP, a comparação com o V8, e a caça ao crash ao criar mundo. |
| [PONTE-OTIMIZACAO.md](PONTE-OTIMIZACAO.md) | A otimização da ponte passo a passo (raízes, identidade, cache de método, índice rápido, wrapper barato), a causa real do crash ao criar mundo, e a pilha por thread do motor JS. |
| [FPS-45-TELA-90HZ.md](FPS-45-TELA-90HZ.md) | Os 45 fps cravados numa tela de 90 Hz: o arredondamento do `targetFrameRate` do Unity, a lista de jogos da Samsung (que ignora o manifest) e o alvo de 61. |
| [FUNDO-SUMINDO-NUVEM-DE-MOD.md](FUNDO-SUMINDO-NUVEM-DE-MOD.md) | O fundo que sumia no celular: a máscara de nuvem do horizonte (`CloudMasks`) sem os tipos de mod, lida sem conferir o limite. **Confirmação no celular pendente.** |
| [DESEMPENHO-LOADER.md](DESEMPENHO-LOADER.md) | Quanto o loader dos mods custa por quadro (o custo fixo de entrar no JS, os hooks de métodos que a classe não escreve, o desenho em duas partes) e as três otimizações: marcas por tipo, o filtro `flag` e o JS de todo quadro sem closure. **Medição no celular pendente.** |
| [TRAVAMENTO-SAVE-DO-MAPA.md](TRAVAMENTO-SAVE-DO-MAPA.md) | O jogo congelando ao entrar no mundo: o hook do save do mapa segurava o `LockObject` por fora da trava de cada pedaço, invertendo a ordem das travas com a thread do jogo. |

Os dados brutos (logs de crash, tentativas, rodadas de benchmark) estão em
[`dados/`](dados/).
