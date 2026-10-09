# Revisão de carregamento do launcher — 7 de outubro de 2026

## Resultado

Foram removidos downloads antecipados de ícones do índice antigo, I/O e
decodificação durante a composição, invalidação global na chegada de arquivos
e alocações sem um orçamento compartilhado. O ganho medido foi menor uso de
memória e carregamento de mídia conforme a interface precisa dela.

**As medições não comprovam eliminação dos engasgos de entrada nem um ganho
uniforme de tempo de quadro.** Permanecem quadros longos ao montar telas pela
primeira vez. A ficha de detalhes registrou mais quadros acima de 50 ms no
candidato, inclusive na contraprova; isso fica registrado como limitação,
sem ocultar a piora desse indicador.

## Correções

- O índice antigo baixa apenas o manifesto para listar o mod; os ícones são
  solicitados pelos cards. Metadados e arquivos visíveis têm dois slots de
  rede em cada caminho. Instalação de pacotes mantém seu fluxo anterior.
- Pedidos sobrepostos compartilham o trabalho por arquivo. A chegada de
  mídia publica uma célula por uid, sem substituir a lista inteira nem
  refazer a busca por causa de fotos/ícones. Uma revisão antiga não substitui
  a entrada de um pacote com outro hash.
- Imagens e GIFs compartilham uma cache de 24 MB/256 entradas. Há duas tarefas
  de mídia por vez; pedidos iguais compartilham a decodificação e resultados
  inválidos também são retidos. Exceções liberam os recursos para outra tentativa.
- Bitmaps são amostrados antes da decodificação: ícones até 256 px, avatares
  até 192 px e imagens de página até 1.024 px. GIFs de ícone têm limite de
  4 MB de entrada, 256 px de lado, um milhão de pixels retidos e 256 quadros.
  A composição/disposal usa a tela original antes de reduzir a saída.
- Páginas, avatares, imagens de Markdown, perfil e lista de saves são lidos
  fora da thread principal. Prévias horizontais usam LazyRow. O início rápido
  espera os saves terminarem de carregar antes de corrigir preferências.
- O tamanho de assets é contado por descritor ou buffer de 16 KB, sem alocar
  cada arquivo completo. A vitrine usa o tamanho já informado pelo índice.
- O indicador de carregamento aplica a rotação na camada gráfica.

## Memória: cache real

Emulador Android arm64, display 0, 1.600 × 900, 240 dpi, APK debug. Mediana de
três rodadas por APK, com a mesma cache de 19 mods e as mesmas preferências
restauradas antes de cada abertura. HTTPS foi bloqueado temporariamente para
controlar a variação da rede. PSS foi amostrado após cada fase; **não é pico
de memória**. MiB = 1.024² bytes.

| Fase | Anterior, MiB | Corrigido, MiB | Redução |
|---|---:|---:|---:|
| Entrada em Explorar | 135.0 | 118.1 | 12.5% |
| Rolagem de Explorar | 143.2 | 128.7 | 10.2% |
| Entrada em Config | 141.4 | 126.3 | 10.7% |
| Ficha do Example Mod | 154.2 | 138.8 | 10.0% |
| Repouso na ficha | 141.6 | 127.0 | 10.3% |

No diagnóstico separado com 40 mods sintéticos, cada um com GIF de 512 × 512
e dez quadros, o PSS após entrar em Explorar passou de **208.0 para
144.1 MiB (30.7% menos)**. Houve uma rodada por APK;
essa carga não representa o catálogo público e não entra na mediana principal.
O orçamento da cache também não limita buffers temporários, referências
visíveis, runtime e texturas da GPU.

## Quadros: resultados completos

Valores anteriores → corrigidos; mediana dos três resultados de cada fase.
O p95 é do ring buffer final de até 120 quadros. O histograma agregado inclui
os picos da entrada que podem já ter saído desse buffer. Buckets são
aproximações, não máximos exatos. Os timestamps de GPU do emulador contêm
valores inválidos, portanto esses números não sustentam promessa de FPS físico.

| Fase | p95 do buffer final, ms | Quadros >50 ms no histograma | Maior bucket ocupado, ms |
|---|---:|---:|---:|
| Entrada em Explorar | 19.27 → 35.32 | 4 → 4 | 300 → 300 |
| Rolagem de Explorar | 19.58 → 20.08 | 0 → 0 | 30 → 40 |
| Entrada em Config | 18.55 → 34.89 | 2 → 2 | 113 → 101 |
| Ficha do Example Mod | 18.78 → 18.16 | 2 → 6 | 250 → 250 |
| Repouso na ficha | 4.38 → 7.98 | 0 → 0 | 6 → 8 |

Explorar manteve quatro quadros acima de 50 ms na mediana e bucket máximo de
300 ms. O candidato teve um bucket de 500 ms em uma das rodadas; todos os
valores individuais permanecem no JSON. Na ficha, a contagem passou de dois
para seis. Retirar parsing/I/O da UI não remove o custo de compor e medir o
conteúdo completo de uma página Markdown longa; imagens que chegam depois
podem exigir novos layouts. Esse custo continua existindo.

Duas rodadas do candidato tiveram p95 de aproximadamente 35 ms em Explorar.
Para investigar a repetibilidade, foram feitas mais três rodadas na ordem
**candidato → anterior → candidato**, restaurando a mesma cache em cada uma.
Essas rodadas não substituem os resultados anteriores nem entram nas medianas.

| Rodada adicional, entrada em Explorar | PSS, MiB | p95, ms | Quadros >50 ms | Maior bucket, ms |
|---|---:|---:|---:|---:|
| after-check1 | 117.9 | 18.61 | 3 | 250 |
| before-check | 133.9 | 19.27 | 4 | 300 |
| after-check2 | 119.1 | 19.67 | 4 | 350 |

A piora de p95 de Explorar não se repetiu de forma consistente nessa
contraprova; isso não identifica sua causa nem prova ausência de regressão.
Na ficha, o candidato registrou quatro quadros acima de 50 ms em ambas as
rodadas adicionais, contra dois do anterior; o maior bucket foi de 150 ms
no candidato e 200 ms no anterior. Há uma troca entre distribuir o trabalho
e ainda precisar montar o conteúdo. A revisão não deve ser apresentada
como solução de todo custo de composição/layout.

## Verificação online, separada da medição

Com a rede liberada, a cache de vitrines foi esvaziada, preservando só índice
/ETag e os backups originais. Resultado:

- 19 mods remotos mais o Example Mod embutido: 20 pacotes listados.
- 11 manifestos antigos baixados; cinco ícones em cache após a primeira
  visita a Explorar. Só dois dos 11 mods antigos tinham ícone baixado.
- Ao abrir Better Achievements, a descrição baixada apareceu ao rolar, e
  capa/avatar foram conferidos visualmente. Os sete arquivos da ficha
  estavam presentes, incluindo changelog e foto do autor.
- Após Config, retorno a Explorar e gesto de arrastar para atualizar, o
  catálogo continuou mostrando 20 pacotes, sem mensagem de falha. O número
  de ícones continuou em cinco, sem iniciar a carga dos demais.
- quickStart, quickPlayer e quickWorld permaneceram iguais aos backups.
- Nenhum FATAL EXCEPTION foi encontrado. Nenhum pacote foi instalado por
  essas ações e GameActivity não foi iniciada.

Isso verifica o fluxo real de download/publicação, sem misturar a velocidade
da rede com a comparação offline. Não foi feito benchmark online do APK
anterior, nem medição no celular do relato.

## Testes e identificação do build

Build completo e **106 testes JVM, zero falhas, erros ou ignorados**. Dos
14 testes novos: sete de cache (LRU/orçamento, nulos, exceção, concorrência
igual/distinta), quatro de GIF (redução, limites, frame corrompido, teto de
quadros) e três de estado por uid, incluindo SnapshotStateObserver real.
Houve 11 rodadas Android de cinco fases cada, além da verificação online.
Os testes JVM não medem latência do hardware nem renderização Android.

- Anterior: `8e987bc6d390166cd574b759bb203cf8ebad892470d80eb52472f313d89c51a1`.
- Corrigido: `951607814816b1f4473769b9cee2346c3325db4270c51cb4be0797f61e6e18b8`.
- APK instalado ao final confere com o hash do corrigido.
- libbunny.so é idêntica byte a byte entre os APKs; o runtime nativo não foi
  alterado por esta revisão do launcher.
- Build: `build/launcherperf/final-utf8-build.log`, BUILD SUCCESSFUL.
- Dados estruturados e hashes dos dumps: [measurements.json](measurements.json).
- Método, alternativas e comandos: [README.md](README.md).

As oito preferências e os 102 arquivos originais da vitrine foram restaurados
e conferidos byte a byte. As regras temporárias de rede foram removidas.
O launcher ficou fechado após a restauração; o APK corrigido ficou instalado.
Os testes não iniciaram o jogo nem modificaram saves.

## Limites da conclusão

O hardware é um emulador e o build é debug. A amostra principal tem três
rodadas consecutivas por APK; a contraprova adicional não é uma amostra
estatística suficiente para atribuir toda variação ao código. Há ganho
consistente de PSS, eliminação do download antecipado observado e testes de
limites/isolamento. Não há evidência suficiente para declarar ganho uniforme
de fluidez, e ainda existe trabalho de composição/layout na primeira montagem.
