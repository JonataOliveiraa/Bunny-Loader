# Resultados de ModNPC

Executado em 5 de outubro de 2026, no ramo master, após revisar os contratos
do tModLoader stable e as alternativas do ExMod v1.5.0 local.

## Verificação automatizada

| Suite | Resultado |
|---|---|
| ModNPC | 46 casos de comportamento; 22 assinaturas de hook verificadas contra o dump móvel |
| ModPlayer | 44 casos; 150 assinaturas e quatro estágios nativos |
| GlobalProjectile | 59 casos; 22 assinaturas |
| Ciclo de morte de projétil | 16 casos |

Todos passaram. O teste de ModPlayer agora carrega o NPCLoader real, pois
o despacho de combate passou a usá-lo. Os testes de ModNPC verificam aridade,
referências, ordem, retorno nativo, isolamento, exceções, registros herdados
e posteriores, dano zero e negativo e preservação dos contextos JavaScript.
As simulações de contexto não removem a proteção nativa da ponte contra
reentrada no mesmo hook; os casos nativos abaixo verificam os fluxos reais.

## Singleplayer nativo

MuMu, uma instância Android arm64, Terraria móvel 301720, netMode 0.
Foram usados os mods já habilitados e somente o fixture desta suite.
Não foi iniciado servidor nem cliente multiplayer.

Resultado final: **31 verificações, zero falhas**, registradas em
[native-results.log](native-results.log). Foram conferidos:

- Nascimento com fonte correta, uma chamada e duas instâncias independentes.
- Perfil nativo, conversas fora de AI 7, ícone de chefe, rotação e flip.
- Escalonamento com contagem 2, balance nativo e ajuste Master 0,85.
- Veto de morte e de golpe de item, modificador de dano e crítico,
  HitInfo e notificações de item e projétil, inclusive owner 255.
- Veto de dano ao jogador, referência do cooldown e HurtInfo real.
- Loja preenchida, preço modificado e cálculo nativo de felicidade.
- Reset por tick após o zero dos flags, antes de AI; DrawBehind em frames
  reais; PostDraw após veto e cor Ref chegando ao renderer e GetAlpha.
- Tipo de mod sem sobrescritas mantendo a cor do jogo.

A primeira execução revelou dois pontos que exigiam corrigir o hook:
UpdateNPC incorpora o reset de flags inline e não chama a função homônima;
NPC.OnSpawn não usa source, e o chamador não preserva esse argumento.
A implementação final intercepta UpdateNPC_BuffSetFlags antes da aplicação
dos buffs e NewNPC após a criação, preservando a fonte da entrada.
Também foi removida uma cópia duplicada do fixture durante a preparação.
Nenhuma falha da execução inicial foi tratada como aprovação.

## Medição

Cinco amostras de 5.000 chamadas a UpdateNPC_BuffSetFlags(false), após a
prova de tick real: **25.000 callbacks, sem perda e com a mesma instância**.
Mediana final: **4,637 µs por chamada** no emulador usado.

A medição inclui chamada pela ponte, código nativo do método, despacho de
ResetEffects e rastreamento de DrawBehind. Não representa apenas o custo
do callback, nem uma medição de FPS ou um resultado garantido em outro
dispositivo. Não foi feita comparação percentual com a versão anterior,
pois ela não tinha esse callback de ModNPC.

## Build e revisão

Build Android arm64 debug concluído com Gradle 8.14.3, JDK do Android Studio,
saída isolada e um worker. O último build incremental terminou em 27 s.
O APK foi instalado; a biblioteca libbunny.so foi conferida para garantir
que continha o código de ModNPC, escalonamento e felicidade deste trabalho.

SHA-256 do APK testado:

```text
711f1d71b7a664229abbd4762c52cf1ebda8f50e7da9d406b10785595467b089
```

A revisão final conferiu filtros de argumentos nativos, cache dos planos
por classe, ausência de closures de despacho no reset, referências de cor,
restauração em finally, aridade, notificações somente após dano efetivo e
uso compartilhado do combate com ModPlayer. A comparação de cor usa
PackedValue para reduzir leituras pela ponte. Hurt usa o mesmo dano
normalizado na esquiva e na chamada nativa.

Os modificadores de combate são objetos do Bunny Loader com campos
documentados, não as estruturas completas do tModLoader. Os dez métodos
sem uma equivalência completa permaneceram descartados, com razões na
[README](README.md).

O fixture foi removido do emulador ao terminar. A entrada de ativação
temporária foi retirada e os valores anteriores dos outros pacotes foram
conferidos. A instância de emulador iniciada para este teste foi encerrada.
