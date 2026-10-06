# Resultados: primeira etapa de ModItem

Executado em 6 de outubro de 2026. Foram conferidos o ModItem stable do tModLoader, os helpers móveis do ExMod v1.5.0, o dump e a desassemblagem do Terraria móvel 301720.

## Testes automatizados

| Suite | Resultado |
| --- | --- |
| ModItem | 60 verificações de comportamento; 14 assinaturas e nomes de parâmetros nativos |
| ModPlayer | 44 verificações; 151 assinaturas; quatro estágios nativos |
| ModNPC | 46 verificações; 22 assinaturas |
| GlobalProjectile | 59 verificações; 22 assinaturas |
| Ciclo de morte de projétil | 16 verificações |
| Localização | 95 verificações |

As 320 verificações passaram. ModItem cobre os 12 métodos de combate da etapa: referências, retornos nulos, vetos, composição de modificadores, herança, registros tardios, filtragem, exceções e restauração de contextos. Três ordens de registro de ModItem, GlobalItem e ModPlayer produziram o mesmo dano, com um único hook de GetWeaponDamage. As simulações de ataques aninhados conferem o estado JavaScript; não removem a proteção nativa contra reentrada no mesmo hook.

## Execução no APK

MuMu Android arm64, uma instância, singleplayer com `Main.netMode === 0`. Resultado final: **30 verificações e zero falhas**, em [native-results.log](native-results.log).

- Modificadores de dano, crítico e repulsão alteraram o resultado nativo.
- A escala alterou a consulta e a hitbox, incluindo a luva, sem persistir a alteração temporária em Item.scale.
- As referências de Rectangle e dontAttack chegaram ao jogo.
- CanHitNPC e o veto de colisão impediram dano. Colisão true permitiu o golpe com uma hitbox distante. CanHitNPC true também permitiu um NPC amigável.
- ModifyHitNPC alterou o dano efetivo. O OnHitNPC anterior continuou sendo chamado uma vez com esse dano.
- CanHitPvp vetou o Hurt real. ModifyHitPvp alterou o golpe e OnHitPvp recebeu o dano efetivamente aplicado.
- MeleeEffects recebeu a hitbox após os efeitos nativos.
- Itens vanilla e de mod sem sobrescritas conservaram o caminho nativo.

Duas verificações iniciais exigiram correção. O fixture ativava kbGlove ao testar tamanho; esta versão usa meleeScaleGlove para esse efeito. A implementação calcula o multiplicador pelo helper nativo. Também existe uma verificação de friendly/tipo da arma depois de CanNPCBeHitByPlayerOrPlayerProjectile. O fluxo agora permite o ataque explicitamente autorizado, restaurando friendly antes dos callbacks de acerto e em finally. Os resultados finais incluem essas correções; as execuções com falha não foram consideradas aprovação.

O fixture restaurou os campos temporários e desativou o NPC. A limpeza restaurou somente as preferências usadas pelo teste e removeu somente seu UID. A instância do emulador iniciada para esta execução foi desligada.

## Filtragem e medição

Cinco amostras de 2.000 consultas GetWeaponDamage por grupo, totalizando 10.000 por grupo. Os contadores de `bl.hookStats()` confirmaram um hook compartilhado e:

| Item | Entradas no callback JavaScript | Mediana da consulta pela ponte |
| --- | --- | --- |
| Vanilla | 0 | 27,824 µs |
| Mod sem sobrescrita | 0 | 28,918 µs |
| Mod com sobrescrita | 10.000 | 60,882 µs |

A medição inclui a chamada do fixture pela ponte, o código nativo, os filtros e, quando aplicável, o dispatcher JavaScript. Não isola apenas o custo do callback e não é uma medição de FPS. Não representa uma comparação percentual com o APK anterior nem uma garantia para outro aparelho.

As sobrescritas e os rótulos de erro são preparados por classe. Marcas limitam os hooks por tipo. Flags limitam os contextos de ataque e Hurt; `whileIn` e uma flag limitam Intersects às colisões explicitamente substituídas. Observadores globais ampliam as marcas necessárias uma vez no registro, incluindo tipos de mod adicionados depois.

## Build, documentação e limites

Build Android arm64 debug concluído com Gradle 8.14.3, JDK do Android Studio, um worker e saída isolada. O build final terminou em 31 s. A assinatura APK v2 foi verificada. O APK instalado, testado e copiado para `out/bunny-loader.apk` tem SHA-256:

```text
76e0c7be414a72b8bc5988322ed8651edbdf206663dff504ed21c19d30d62c16
```

A referência, o catálogo e a planilha foram atualizados. A verificação confrontou os 1.055 registros entre JSON, CSV e Excel, incluindo filtros e painéis fixos. As vistas de assinaturas e contratos foram renderizadas para revisão.

PvP foi testado com dois objetos Player no mesmo processo. Não foi testada uma sessão multiplayer entre processos nesta etapa. O contexto atual de PvP vem de ItemCheck_MeleeHitPVP; mensagens Hurt recebidas isoladamente não reconstroem a arma de origem. Essa implementação e validação permanecem pendentes no [plano](PLANO.md), assim como as etapas 2 a 6.
