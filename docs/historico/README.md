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

Os dados brutos (logs de crash, tentativas, rodadas de benchmark) estão em
[`dados/`](dados/).
