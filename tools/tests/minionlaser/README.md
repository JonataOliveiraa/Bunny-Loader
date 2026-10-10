# Lasers de mod e o custo do combate de projétil

Origem: o lacaio EyeOfEternity do Consolaria (porte do GST378) derrubava o
FPS ao atirar o EternalLaser, que clona o EyeLaser e usa
`AIType = ProjectileID.PurpleLaser`. A suspeita era o `AIType`; não era.

O teste dispara lasers num ritmo fixo (um a cada 2 quadros, 120 quadros de
vida: ~60 no ar) e mede o `DoUpdate`/`DoDraw` e os hooks (`bl.hookStats`) em
seis fases de 300 quadros: nada, PurpleLaser e EyeLaser do jogo, o
EternalLaser com e sem `AIType`, e 10 lacaios atirando em slimes. Precisa do
Consolaria ligado (`ModContent.ProjectileType('consolaria/EternalLaser')`).

## O que era

Com qualquer hook de golpe de projétil (um `OnHitNPC` de projétil, um
`OnHitNPCWithProj` de ModPlayer), o `CombatLoader` punha um hook JS no
`Projectile.Damage_PVE`, que o jogo chama para todo projétil amigo a cada
atualização (o laser tem 2 a 4 atualizações extras por quadro), e esse hook
ligava o `CanNPCBeHitByPlayerOrPlayerProjectile` no JS para cada NPC. No MuMu,
com 60 lasers e ~12 NPCs: 2.800 entradas no JS por quadro.

Agora o `Damage_PVE` tem só o escopo nativo (`scope()`), o `StrikeNPC` acha o
projétil pelo `scopeSelf()` e o `CanNPCBeHit...` de projétil só entra no JS
para quem tem `CanHitNPC` de projétil ou `CanBeHitByProjectile`.

| Fase (MuMu, 2026-10-09) | DoUpdate antes | depois |
|---|---:|---:|
| Nada | 4,4 ms | 4,3 ms |
| PurpleLaser do jogo | 68,3 ms | 6,5 ms |
| EyeLaser do jogo | 39,4 ms | 5,9 ms |
| EternalLaser com AIType | 48,4 ms | 9,0 ms |
| EternalLaser sem AIType | 31,4 ms | 7,8 ms |
| 10 lacaios atirando | — | 8,2 ms |

## Execução

Com o Consolaria e este pacote em `bunny_packs`, entrar no mundo; o teste
loga `minionlaser ...` e termina em `minionlaser FIM`.
