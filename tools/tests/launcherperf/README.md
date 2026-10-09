# Carregamento e engasgos do launcher

## Análise

Relato: pico ao abrir Explorar após atualizar o APK, além de engasgos em
outras telas. A revisão inclui Explorar, Início, ficha do pacote e Config.
Não foi fornecido o modelo do celular; medições Android são do emulador,
não uma promessa de tempos iguais no aparelho.

Fatos encontrados no código anterior:

- O caminho de índice antigo baixava manifesto **e ícone** de todos os
  mods antigos, mesmo fora da tela. Só o manifesto é necessário para listá-los.
- Cada arquivo baixado substituía a lista remota e incrementava uma versão
  global, invalidando busca/ordenação e páginas de outros mods.
- Havia decodificação/leitura síncrona em avatares, prévias, Markdown,
  perfil e lista de personagens/mundos. As abas da ficha eram lidas e
  analisadas durante a composição.
- GIFs podiam reter cerca de 16 MB de pixels cada, além da cópia em bitmaps;
  caches estática e animada tinham 24 MB cada. Vários GIFs podiam ser
  decodificados simultaneamente. Falhas não eram memorizadas.
- O cálculo do tamanho do pacote embutido alocava cada arquivo inteiro.
  Entradas remotas percorriam a vitrine recursivamente, apesar de o índice
  já informar o tamanho do pacote.

Hipótese: limitar alocação/concorrência e retirar I/O da composição deve
reduzir picos. Ela é avaliada por testes de comportamento e medições
separadas de cache real e de GIFs sintéticos. Quadros longos na entrada
exigem observar o histograma agregado, além dos percentis da janela final.

Casos cobertos: pedidos concorrentes da mesma imagem, formatos misturados,
falha repetida, exceção no carregador, LRU, orçamento de bytes/entradas,
GIF corrompido, transparência/descarte/entrelaçamento, excesso de quadros,
atualização de arquivo sem mudança nos caminhos, mudança de hash do pacote,
consumidores de outros uids e preferência de início rápido durante carga.

## Design e implementação

| Alternativa | Custo e manutenção |
|---|---|
| Só mover cada operação para IO | UI deixa de esperar, mas muitos trabalhos e alocações podem continuar concorrendo; não corrige invalidação global |
| Trocar o pipeline por biblioteca externa | Pode resolver cache/carga, mas adiciona dependência e exige adaptar GIF/pixel art e regras de arquivos do pacote |
| Limitar e reutilizar o pipeline existente | Escolhida: cache de produção testável, dispatcher com duas tarefas, estado por uid e leituras assíncronas; mantém os formatos e caminhos existentes |

`MediaCache` compartilha 24 MB e no máximo 256 entradas entre imagens e
animações; lookup LRU é O(1) amortizado, e a admissão pode evictar entradas
até caber no orçamento. Pedidos da mesma chave compartilham a decodificação.
Resultados nulos também são retidos; a chave inclui revisão do arquivo e
dimensão pretendida. Valores acima do orçamento não expulsam a cache útil.

`MediaDispatcher` admite duas tarefas bloqueantes de mídia por vez. Isso
evita ocupar uma thread IO por imagem aguardando o semáforo da cache. A
cache também limita a duas decodificações caso seja chamada por outra
thread. Bitmaps usam leitura de dimensões e inSampleSize; ícones até 256 px,
avatares até 192 px e imagens de página até 1.024 px no maior lado. As
texturas são preparadas antes de publicar o resultado para a UI.

GIFs de ícone aceitam até 4 MB de entrada, saída de até 256 px no maior
lado, um milhão de pixels retidos (~4 MB) e 256 quadros. O orçamento de
processamento da fonte continua com quatro milhões de pixels e lado 512.
A composição/disposal acontece na tela original antes da amostragem.
Animações que excedem o orçamento podem terminar antes; não se promete
preservar todos os quadros de arquivos arbitrariamente longos.

`StoreMedia` usa uma célula observável independente por uid. Um único
SnapshotStateMap ainda invalidaria leitores de outras chaves; o teste
observa os consumidores reais com SnapshotStateObserver. A revisão por
uid muda inclusive quando só chega texto/foto e os caminhos de ícone e
capa continuam iguais. Um hash antigo não pode substituir a entrada nova.
A lista de catálogo/busca não é substituída na chegada de imagens.

Leituras de páginas, imagens inline, avatares e prévias saem da composição;
prévias horizontais usam LazyRow. Perfil e saves são carregados fora da UI.
O início rápido só corrige preferências após a lista terminar de carregar.
A rotação do indicador lê o estado na camada gráfica, sem recompor a
cada quadro. Metadados antigos e arquivos visíveis têm dois slots de rede
em cada caminho; instalar pacotes mantém seu fluxo anterior.

O orçamento da cache não limita todo o PSS do app: referências visíveis,
buffers temporários, texturas da GPU, fontes e runtime também consomem
memória. Uma página Markdown muito longa ainda pode custar composição e
layout; retirar parsing/I/O não elimina todo custo da UI.

## Testes JVM e build

```powershell
gradle :app:testDebugUnitTest :app:assembleDebug --console=plain
```

106 testes: 92 existentes e 14 novos, sendo sete de cache, quatro de GIF e
três de observação por uid. Os testes de cache usam threads/latches reais;
os de GIF comparam a saída reduzida à composição já validada contra Pillow.
Eles não simulam velocidade do Android nem a saída gráfica da GPU.

## Medição Android

`device.py` requer ADB/su, launcher fechado e cópias de preparação em
`build/launcherperf`. Não inicia GameActivity. Esta execução foi preparada
com backups tar de `shared_prefs` e `cache/store`, o índice em
`index.original.json` e uma hierarquia da tela inicial em `initial.xml`.
Os APKs completos ficam em `baseline.apk` e `candidate.apk` nessa pasta.
Não substitua os backups enquanto uma preparação estiver em uso.

Os backups podem ser obtidos com `adb exec-out run-as com.bunnyloader tar
-cf - shared_prefs` e o equivalente para `cache/store`, gravando stdout
binário diretamente, sem conversão de texto pelo PowerShell. O XML vem de
`uiautomator dump`; ele fornece as coordenadas dos controles, em vez de
supor posições. Em múltiplos displays, o runner move a task do launcher
para o display 0 antes dos gestos. A resolução desta execução foi
1.600 × 900, densidade 240 dpi.

```powershell
# Instalar baseline antes destas chamadas:
python tools/tests/launcherperf/device.py run --label before-1
python tools/tests/launcherperf/device.py run --label before-2
python tools/tests/launcherperf/device.py run --label before-3
python tools/tests/launcherperf/device.py run --label before-stress --stress
# Instalar candidato e repetir com labels after-1/2/3/stress.
python tools/tests/launcherperf/analyze.py
python tools/tests/launcherperf/device.py restore
```

Nesta execução houve uma contraprova adicional na ordem candidato, anterior,
candidato. Instale o APK correspondente antes de cada comando:

```powershell
python tools/tests/launcherperf/device.py run --label after-check1
python tools/tests/launcherperf/device.py run --label before-check
python tools/tests/launcherperf/device.py run --label after-check2
```

`analyze.py` inclui essas rodadas separadamente quando presentes. Registros
adicionais de build, JUnit, verificação online e restauração em
`build/launcherperf/*.json` são opcionais; eles documentam as verificações
desta execução e não são fabricados pelo analisador. `RESULTADOS.md` contém
a interpretação manual dos dados, não é regenerado pelo analisador.

As rodadas temporariamente rejeitam HTTPS no emulador, restaurando a regra
em `finally`; não alteram Wi-Fi/dados móveis. Esse kernel não oferece o
match iptables `owner`, portanto o bloqueio temporário de porta 443 cobre
o emulador inteiro. `network.blocked.json` permite desfazer as regras após
uma interrupção. `restore` também remove essa regra antes de restaurar os
arquivos. Use apenas um runner por emulador e execute restore ao terminar.

A comparação principal usa a mesma cache de 19 mods, sem variação de rede;
são três rodadas consecutivas por APK, não alternadas. Cada abertura
restaura as preferências anteriores à instalação, incluindo a marca do
APK, para exercitar a preparação após atualização. Foram medidos entrada
em Explorar, 16 gestos de rolagem, Config, ficha do Example Mod e repouso
na ficha. Builds não rodaram junto com medições válidas.

O diagnóstico separado gera 40 mods locais de teste com GIFs de
512 × 512 e dez quadros; os pacotes não são instalados. Não representa o
catálogo público nem é combinado com a mediana principal.

A verificação online foi feita depois das medições, com HTTPS liberado:
restaurar os backups, esvaziar somente `cache/store`, restaurar índice/ETag
do tar, abrir Explorar e inventariar os arquivos baixados. Depois abrir uma
ficha remota, conferir texto/capa/avatar, visitar Config, voltar e arrastar
para atualizar. Hierarquias, screenshots e inventários estão em
`build/launcherperf/online-*`; os resultados conferidos estão em
`online-validation.json`. Finalizar sempre com `device.py restore`.

Gfxinfo registra histograma e ring buffer de até 120 quadros; o ring buffer
do fim pode perder a travada inicial. `metrics.py` aceita os cabeçalhos CSV
de diferentes versões Android e mantém ambos os resultados. Buckets do
histograma são aproximações, não máximos exatos. PSS é amostrado após
cada fase, não é uma medição de pico. Não use GPU timestamps inválidos
do emulador (`GpuCompleted=-1`) para prometer FPS/latência física.

Resultados e limitações: [RESULTADOS.md](RESULTADOS.md) e
[measurements.json](measurements.json). Arquivos gfxinfo, meminfo, logcat,
screenshots e backups completos ficam em `build/launcherperf`.

As decisões seguem a documentação primária de
[otimização de bitmaps](https://developer.android.com/develop/ui/compose/graphics/images/optimization)
e [leituras de estado no Compose](https://developer.android.com/develop/ui/compose/performance/bestpractices).
