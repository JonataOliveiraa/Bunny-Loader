# Filtro nativo de áudio

Este documento e RESULTADOS.md descrevem a etapa inicial, com reprodução
síncrona. A evolução para despacho assíncrono está em
[soundasync](../soundasync/README.md). O check.mjs e o fixture compartilhado
também verificam o contrato atual; os dados históricos desta etapa foram
preservados.

## Análise

Entrada: chamadas ao `LegacySoundPlayer.PlaySound`, com tipo vanilla ou o
marcador 1000 de SoundStyle. Saída: os sons vanilla seguem para o original;
somente o marcador customizado entra no callback JS do SoundLoader.

O filtro da ponte C++ é executado antes do `JsLock` e da conversão dos
argumentos. `arg: 0` identifica o parâmetro declarado `type`, sem contar o
receiver. A marca `sound.mod` evita encaminhar ao JS tipos altos que não
sejam o marcador. A guarda JavaScript existente continua presente.

Casos cobertos: instalação lazy/única, IDs vanilla e inválidos, diferentes
estilos, argumentos do original, hooks brutos encadeados, API direta,
volume, pitch, pan, distância, silêncio, limites de instâncias, stop,
expiração, cache e falha do backend.

## Design e implementação

| Alternativa | Custo | Avaliação |
| --- | --- | --- |
| Guarda apenas no JS | Uma entrada JS por chamada vanilla; espaço constante | Comportamento atual antes da correção, mas espera/conversões desnecessárias |
| Filtro nativo + guarda existente | Filtro O(1) antes do lock; tabela de marcas de tamanho fixo | Adotado; preserva o caminho e o contrato dos sons customizados |
| Fila assíncrona de SFX | Estado de pedidos/streams e limite/cancelamento próprios | Fora desta mudança; a proposta enviada tem regressões documentadas na revisão |

A alteração de produção está restrita a `SoundLoader.Install`: registra a
marca 1000 e passa `{ minType: SoundLoader.ID, arg: 0, marks: 'sound.mod' }`
ao hook. Não altera a reprodução Kotlin nem a ordem dos fades de música.

## Testes sem dispositivo

```powershell
node tools/tests/soundfilter/check.mjs
```

Carrega os três arquivos de produção SoundLoader, SoundStyle e SoundEngine.
O Android e o encaminhamento nativo são simulados; os resultados não medem
QuickJS nem FPS. A integração seguinte verifica a ponte real.

## Medição no Android

O runner requer o ADB e um emulador com `su 0` e `run-as com.bunnyloader`.
Ele recusa preparar o teste se há uma sessão de jogo ativa. Usa cópias
`BL_SoundFilter_Test` de `sda.plr` e `Pesadelo_Odioso.wld`; ajuste esses nomes
no runner para outro aparelho. Não execute em uma sessão com progresso não
salvo. O restore verifica os saves originais por SHA-256 e as preferências
byte a byte, remove apenas o pacote e saves temporários e abre o launcher.

Antes de compilar a mudança, preserve o APK anterior em
`build/soundfilter/baseline.apk`. Compile o APK filtrado normalmente. O build
completo é necessário: as classes JS são incorporadas à biblioteca nativa.

```powershell
python tools/tests/soundfilter/device.py prepare
python tools/tests/soundfilter/device.py run --variant baseline --label baseline-1 --apk build/soundfilter/baseline.apk
python tools/tests/soundfilter/device.py run --variant filtered --label filtered-1 --apk app/build/outputs/apk/debug/app-debug.apk
# Repetir com labels baseline-2/3 e filtered-2/3, alternando os APKs.
python tools/tests/soundfilter/device.py restore
python tools/tests/soundfilter/analyze.py build/soundfilter/127.0.0.1-16384 --out build/soundfilter/summary.json
```

Sempre execute restore, inclusive depois de uma rodada com falha. Os
arquivos de backup e logs ficam em `build/soundfilter/<device>/`.

Os tons WAV e a textura são gerados pelo runner, sem depender de áudio de
outro mod. Cada rodada carrega o mesmo personagem/mundo inicial, habilita
somente o fixture e usa quatro efeitos de 200 ms, uma música em loop e 32
NPCs temporários sem movimento/dano. Os NPCs exercitam o registro, a ponte
de IA e a seleção de música; não simulam a IA complexa de um boss do Split.

Após aquecimento, cada fase descarta 60 atualizações e mede 180 lotes:

| Fase | Pedidos por atualização |
| --- | --- |
| vanilla | 48 pedidos vanilla pela API nativa |
| mixed | 48 vanilla + 12 marcadores customizados pela API nativa |
| custom-marker | 12 marcadores customizados pela API nativa |
| custom-direct | 12 pedidos pela API JS SoundEngine.PlaySound |

O som customizado passa pelo caminho normal, com MaxInstances 0. Esse é um
teste de saturação deliberada, chegando a 720 pedidos customizados/s em
60 atualizações/s. O limite de 32 streams do SoundPool continua valendo.
O fixture também verifica ReplaceOldest, IgnoreNew, distância, expiração,
uso real de um item com UseSound customizado, música e remoção dos NPCs.

`bl.hookStats()` mede chamadas, entradas no JS e tempo do callback sem o
original. **O tempo inclui chamadas JNI e espera no backend Android; não é
CPU pura do QuickJS.** Na API JS direta não há passagem pelo hook Legacy,
mas SoundLoader.Play ainda roda em JS. Zero no contador desse hook não
significa ausência de trabalho JS no caminho direto.

O lote mede a duração de disparar os pedidos. DoUpdate mede a atualização
completa e DoDraw mede intervalos entre desenhos. O jogo pode executar
várias atualizações entre desenhos; portanto, 180 atualizações não são
necessariamente 180 frames renderizados. O resumo usa a mediana de três
rodadas e também preserva o intervalo de resultados de cada contador.

As medições no emulador não garantem FPS em celulares nem reproduzem todas
as armas, NPCs ou arquivos do Split. Consulte `RESULTADOS.md` para os dados
e limites da execução registrada.

## Perfil opcional do backend

Acrescente `--profile` ao comando `run`, usando labels diferentes, como
`profile-baseline` e `profile-filtered`. Essa opção instrumenta apenas o
fixture: mede o tempo dentro de `bl.sounds.play/stop`, conta retornos zero
do backend e acrescenta a fase `custom-ignore`, com os mesmos 12 pedidos
por atualização e MaxInstances 1/IgnoreNew. Não altera o Kotlin nem os
defaults dos mods. Execute restore ao terminar.

O wrapper acrescenta leituras do relógio. Por isso essas rodadas são um
diagnóstico separado, não entram na mediana A/B principal. O intervalo
medido inclui JNI, despacho e espera do Android; não identifica sozinho
qual operação interna do SoundPool consome CPU ou bloqueia.
