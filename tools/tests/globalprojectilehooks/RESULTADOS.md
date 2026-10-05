# Resultados de GlobalProjectile

Validação concluída em 5 de outubro de 2026, com a versão móvel 301720.
Os 14 métodos pedidos foram implementados para projéteis do jogo, classes
`ModProjectile` e tipos registrados diretamente por `bl.projectiles.register`.
A implementação e os fixtures estão no commit `42ecf6f`. A compilação
usou a árvore compartilhada, que também continha mudanças de outras
tarefas, com uma pasta de saída própria para esta validação.

## Testes e compilação

| Verificação | Resultado |
|---|---|
| Comportamento dos Globais, integração com métodos locais e cache | 59 casos aprovados; 22 assinaturas nativas conferidas em `refs/dump.cs` |
| Regressões de morte e colisão de projéteis | 16 casos aprovados |
| Regressões de `ModPlayer` | 44 casos aprovados; 150 assinaturas conferidas; 4 estágios nativos registrados |
| Fixture executado no jogo Android | 38 verificações, nenhuma falha |
| Compilação Android com CMake | `BUILD SUCCESSFUL in 2m 39s` |
| Conteúdo da biblioteca dentro do APK | Confirmados o fluxo global, o cache de clone e a validação do tipo nativo de gancho |
| Revisão de whitespace | `git diff --check` sem erros nos arquivos alterados por esta tarefa |

Comandos reproduzíveis estão em [README.md](README.md). O resultado nativo
completo, contendo somente mensagens deste fixture, está em
[native-results.log](native-results.log).

O APK validado fica em
`build/globalprojectilehooks/builds/app/outputs/apk/debug/app-debug.apk`.
SHA-256:

```text
2659D048B6AF81749C61CDE71F128D2404449CAFCE2D2F7A24A01D452F68D404
```

O jogo real confirmou substituição de cor, colisão fora da hitbox padrão,
veto de dano em NPC, contato de minion, restauração das tabelas de tipo,
substituição de hitbox por `Ref`, veto e ordem de corte, isolamento por
entidade e recriação de estado por `SetDefaults`. Confirmou também veto de
spawn de gancho, troca de tipo inclusive para conteúdo da API nativa,
restauração de `item.shoot`, fixação em blocos, colisão com plataformas,
preservação de projétil após `OnTileCollide(false)` e `OnKill` exatamente
uma vez quando o tempo termina no chão.

As regressões em JavaScript cobrem erros nativos e de callbacks, retomada
do estado em `finally`, registros tardios, clones de itens, número exato
de argumentos e 17 combinações do comportamento nativo de plataformas.
Também conferem a corrente de gancho quando o sprite é vetado.

Esta execução Android ocorreu em um mundo individual, no MuMu, com um
único dispositivo ativo. Os 44 testes de `ModPlayer` aqui são regressões
automatizadas; a validação anterior de host, cliente e reconexão está no
commit `44bd638`. Esta tarefa não repetiu aquela sessão multiplayer.

## Revisão de implementação

- Os contratos e as regras de combinação foram conferidos no
  [GlobalProjectile](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/GlobalProjectile.cs)
  e no
  [ProjectileLoader](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/ProjectileLoader.cs)
  do tModLoader. A ordem de desenho, dano, estilo de colisão, corte e gancho
  foi verificada por comportamento, com vetos e retornos indefinidos.
- Hooks locais e Globais usam o mesmo fluxo por operação. Ao registrar um
  Global, o filtro local é desligado no C++, deixando uma entrada efetiva
  em JS. Novos tipos não dependem de preencher marcas para todos os tipos
  nativos ou de substituir a função de registro.
- O registro armazena os assinantes por método. A primeira seleção custa
  O(G), para G Globais registrados; chamadas seguintes consultam o cache em
  O(1) e executam O(S), para S assinantes. Listas são mantidas por `WeakMap`.
  Registros posteriores acrescentam instâncias sem reiniciar o estado.
- Movimento sem observadores ou com `tileCollide` desligado não cria
  contexto, cópias de vetores ou `Ref`. Hitbox sem observadores não copia o
  retângulo. Dano sem callbacks de contato ou colisão dispensa a leitura
  das tabelas adicionais.
- Helpers de movimento e filtros de desenho usam `whileIn` e flags
  nativas. Contextos de movimento, cor, corte, desenho, tabelas de tipo e
  `item.shoot` são restaurados em `finally`.
- O cache do clone de `GlobalItem` preserva a versão da lista. Um teste com
  o loader real verifica que um Global registrado após o clone passa a
  existir no item copiado sem perder o estado já clonado.

`PreDraw` usa `Ref<Color>`, `ModifyDamageHitbox` usa `Ref<Rectangle>` e
`UseGrapple` usa `Ref<number>` nos Globais. A API anterior de
`ModProjectile` mantém seus parâmetros diretos e seu retorno numérico de
`UseGrapple`. A API móvel fornece `Tile` ao teste de fixação, por isso
`GrappleCanLatchOnTo` recebe o bloco em vez de coordenadas `x` e `y`.
Correntes e linhas permanecem no desenho nativo integrado ao
`DrawProjDirect`; o fixture não mede a ordem física de cada desenho de
extra em relação a desenhos personalizados feitos pelo callback.

## Medições de despacho

Node.js 22.14.0, Windows, comparação com o registro do commit `1bae565`.
O benchmark usa 64 Globais, com oito assinantes, aquecimento de 10.000
chamadas e sete amostras de 100.000 chamadas por caso. Valores são medianas
em microssegundos por chamada.

| Caso | Registro anterior | Registro atual | Redução medida |
|---|---:|---:|---:|
| Despacho para 8 de 64 Globais | 7,264 µs | 0,361 µs | 95,0% |
| Combinação de permissões para 8 de 64 Globais | 7,144 µs | 0,564 µs | 92,1% |
| Método sem assinantes entre 64 Globais | 3,800 µs | 0,059 µs | 98,4% |

As duas versões executaram exatamente 5.600.000 callbacks em cada caso
com assinantes. O caso vazio executou zero callbacks em ambas.

No APK final, sete amostras de 10.000 chamadas reais de `Colliding` com
dois Globais deram mediana de 12,482 µs. O teste verificou os 70.000
despachos e a preservação da mesma instância. Essa medida inclui a ponte
nativa, os dois callbacks, acesso a `GetGlobalProjectile` e registro de
eventos do fixture; não representa somente o custo do registro.

Estes números medem os casos descritos. Não são medidas de FPS, nem
estimativas de melhoria de desempenho do jogo inteiro. O emulador e as
compilações concorrentes variam o custo absoluto das chamadas; a medida
nativa não foi comparada a um APK anterior sob carga equivalente.

## Limpeza

O fixture desativa os projéteis e o NPC que criou, remove somente suas
plataformas e usa `noDropItem`/`noItem` para evitar itens residuais. Depois
da execução, seu pacote e os arquivos temporários foram removidos do
dispositivo. O emulador iniciado para esta validação foi encerrado.
