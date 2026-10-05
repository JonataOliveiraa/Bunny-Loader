# GlobalProjectile: verificação dos 14 hooks

## Análise

Entrada: projéteis nativos ou de mods, seus callbacks locais e Globais,
retornos opcionais e parâmetros mutáveis. Saída: decisões de desenho,
dano, movimento, corte e gancho, com a ordem de combinação documentada.

Fatos verificados: as assinaturas constam em `refs/dump.cs`; o desenho de
extras está integrado ao `DrawProjDirect` no celular; pets têm uma saída
antecipada em `Damage`; a colisão complexa é selecionada por um Set;
`CutTiles` consulta `CanCutTiles` antes de cortar.

Premissa: conteúdo implementa os tipos de retorno documentados. A API
existente de `ModProjectile` permanece compatível. Os Globais novos usam
`Ref` para cor, hitbox e tipo de gancho, como no tModLoader.

Casos de borda: retorno indefinido, veto posterior a permissão, callbacks
que lançam erro, tipo sem classe `ModProjectile`, registro tardio,
reutilização de entidades, duas entidades do mesmo tipo, morte no chão,
desenho vetado com extras e restauração de estado após erro nativo.

## Projeto

| Abordagem | Tempo por chamada | Espaço | Manutenção |
|---|---|---|---|
| Wrappers globais separados dos locais | Até duas entradas JS e duas buscas por entidade | Contextos duplicados de movimento e desenho | Ordem depende da instalação; maior risco de callbacks repetidos |
| Fluxo compartilhado com lista de assinantes por método | O(S), com S callbacks aplicáveis; busca em cache O(1) | Listas por entidade e método, mantidas por `WeakMap` | Regras de retorno e restauração ficam num único fluxo |

A implementação usa a segunda abordagem. Sem Global, o filtro nativo
seleciona somente os tipos locais que implementam o método. Ao registrar
um Global, o filtro local é desligado e uma entrada global assume o mesmo
fluxo, atendendo também tipos registrados pela API nativa. O filtro
desligado é rejeitado no C++, sem entrar no JS. Helpers de movimento e
desenho continuam usando `whileIn` e flags nativas.

## Execução

Na raiz do repositório:

```powershell
node tools/tests/globalprojectilehooks/check.mjs
node tools/tests/projectilekill/check.mjs
node tools/tests/modplayerhooks/check.mjs
node tools/tests/globalprojectilehooks/bench.mjs 1bae565
node tools/tests/globalprojectilehooks/bench.mjs
```

O benchmark compara 8 assinantes entre 64 Globais, aquece o motor, mede
sete amostras de 100.000 chamadas e verifica a quantidade de callbacks.
Ele mede despacho JavaScript, sem atribuir o resultado ao FPS do jogo.

Para compilar com Gradle 8.14.3 e o JDK do Android Studio, sem disputar a
saída com builds do launcher:

```powershell
$env:JAVA_HOME = 'C:\Program Files\Android\Android Studio\jbr'
$env:CMAKE_BUILD_PARALLEL_LEVEL = '1'
gradle -I tools/tests/globalprojectilehooks/isolated.gradle :app:assembleDebug --no-daemon --max-workers=1 '-Dorg.gradle.jvmargs=-Xmx1536m' '-Pkotlin.compiler.execution.strategy=in-process'
```

O APK fica em `build/globalprojectilehooks/builds/app/outputs/apk/debug/`.
O fixture usa o UID `c54c31ec-a54d-4ed9-aa3e-265e0341fb48`. Copie somente
`manifest.json` e `content/` para a pasta desse UID em `bunny_packs` e abra
um mundo. O log termina com `globalprojectilehooks FIM`.

O fixture cria e remove seus projéteis, NPC e plataformas, recusa posições
com blocos existentes e desativa drops dos projéteis de teste. Remova a
pasta desse UID depois da execução. Não remova outros pacotes.

## Fontes conferidas

- [Contratos de GlobalProjectile](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/GlobalProjectile.cs).
- [Combinação dos callbacks no ProjectileLoader](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/ProjectileLoader.cs).
- `refs/dump.cs` e desmontagem de `Damage`, `HandleMovement`, `CutTiles`, `CanCutTiles` e `FireGrapple` da versão móvel.

Os resultados e limites das medições estão em [RESULTADOS.md](RESULTADOS.md).
