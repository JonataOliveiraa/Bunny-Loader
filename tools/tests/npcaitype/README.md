# AIType de ModNPC

## Análise

Entrada: instância de `ModNPC`, `AIType`, `npc.aiStyle`, hooks locais e
globais. Saída: execução da IA vanilla com um tipo temporário, conservando
a identidade do NPC para os callbacks posteriores.

Fontes conferidas:

- [ModNPC.cs do tModLoader stable](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/ModNPC.cs):
  `AIType` é um inteiro por instância, com padrão zero; `AnimationType` é
  outra propriedade.
- [NPCLoader.cs do tModLoader stable](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/NPCLoader.cs):
  `NPCAI` consulta `PreAI`, troca `npc.type` quando `AIType > 0`, executa
  `VanillaAI`, restaura o tipo, executa `AI` e depois `PostAI`.
  Todos os globais `PreAI` são consultados antes do local; um veto global
  impede o `PreAI` local. `AI` e `PostAI` executam o local antes dos globais.
- [NPC.cs.patch do tModLoader](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/NPC.cs.patch):
  separa `AI` e `VanillaAI`; a seleção de família continua em `aiStyle`.
- Referências locais em `exmod_tl/ExMod_v1.7.1 (3)/Modified/1.mod/TL/` e
  `exmod_tl/ExMod_v1.4.2 (2)/Modified/1.mod/TL/`: `ModNPC.js`,
  `Hooks/NPC.js` e `Loaders/NPCLoader.js`. Ambas declaram `AIType = 0` e
  fazem a troca ao redor do corpo original de `NPC.AI`, restaurando antes
  dos callbacks. O ExMod busca o valor pelo molde do tipo, mantém um mapa
  temporário `realTypes` e não protege a restauração com `finally` nesse
  trecho. Seu `PreAI` local precede os globais, cuja busca pode terminar
  no primeiro veto; essa ordenação difere do tModLoader.
- `ModProjectile.js` e `Loaders/ProjectileLoader.js` do Bunny Loader:
  propriedade por instância e restauração com `try/finally` já existentes.
- Dump móvel `refs/dump.cs` e desmontagem de `Terraria.NPC$$AI`:
  a família 2 chama `AI_002_FloatingEye`; o Android não tem o
  `NPC.VanillaAI` acrescentado pelo tModLoader. Aqui o corpo original
  de `NPC.AI` corresponde à etapa vanilla.

Problemas identificados antes da mudança:

1. A base `ModNPC` não declarava `AIType`.
2. O hook de IA era instalado e marcado apenas para classes que
   sobrescreviam `PreAI`, `AI` ou `PostAI`. Só configurar a propriedade em
   `SetDefaults` não instalaria nem habilitaria o hook.
3. Hooks locais e globais eram wrappers independentes. Com a troca de
   tipo, um wrapper global interno poderia receber o tipo emprestado,
   reavaliar `AppliesToEntity` e perder a instância global original.
   A ordem dos vetos e callbacks também dependia do encadeamento.

Premissa de uso: `AIType` recebe um ID positivo de NPC vanilla compatível
com `aiStyle`. O loader não escolhe a família nem clona atributos.
Como em `ModProjectile`, a leitura usa um inteiro JS de 32 bits; valores
não positivos não ativam a substituição. Isso não valida IDs positivos
arbitrários nem torna toda IA vanilla adequada a qualquer NPC.

Casos de borda: propriedade sem hooks, herança, registro posterior,
duas instâncias, troca em `PreAI`, zero, negativos, vetos, exceções,
mudança de tipo dentro da IA, globais condicionais e por entidade,
ordens distintas de instalação e repetição por centenas de ticks.

## Projeto

| Abordagem | Tempo por chamada | Espaço | Manutenção |
|---|---|---|---|
| Troca dentro do wrapper local existente | O(1) local, O(G) nos globais | O(1) temporário | Simples, mas os globais internos podem ver o tipo emprestado e a ordem depende da instalação |
| Despacho compartilhado de IA, com filtro nativo quando não há globais | O(1) local, O(G) nos globais | Plano por classe e O(1) temporário | Um contrato de ordem e restauração, independente da instalação |

A segunda abordagem foi adotada. `NPCLoader.InstallAI` instala o caminho
filtrado para qualquer ModNPC, permitindo configurar a propriedade sem
sobrescrever AI. Quando existe um global com métodos de IA, instala o
despacho global compartilhado e desativa o caminho local por flag nativa.
Um registro local posterior não reativa o caminho desativado.

Sem global de IA, os NPCs vanilla passam pelo filtro `FIRST_NPC` sem entrar
em JS. Com global de IA, há um despacho JS por chamada. Os planos e rótulos
locais são reutilizados; os callbacks globais usam o registro existente.
`GlobalNPC` que só implementa outros eventos não habilita o despacho global.

## Implementação

O contrato segue o tModLoader para a ordem e os vetos, e a proteção já usada
no ModProjectile do Bunny Loader para restaurar o tipo em `finally`.
O valor é lido da instância depois de `PreAI`. Somente `npc.type` é
substituído: `netID`, `aiStyle`, `AnimationType`, estado de IA e identidade
da instância não são trocados. As alterações feitas pela IA em velocidade,
estado e atividade são conservadas.

Como no trecho de `NPCAI` do tModLoader, se a IA emprestada mudar `type`,
o tipo anterior ainda é restaurado. Sem `AIType`, mudanças nativas de tipo
não são desfeitas. Transformações intencionais do mod podem ser feitas
depois da etapa emprestada, em seu `AI`.

Uma exceção nos callbacks do mod é reportada pelo mecanismo `Safe` e os
demais estágios continuam conforme o contrato existente. Uma exceção
propagada pelo corpo original restaura o tipo antes de sair; não se promete
executar `PostAI` nesse caminho excepcional, como no tModLoader.

## Testes sem dispositivo

```powershell
node tools/tests/npcaitype/check.mjs
node tools/tests/modnpchooks/check.mjs
node tools/tests/globalprojectilehooks/check.mjs
node tools/tests/projectilekill/check.mjs
node tools/tests/modplayerhooks/check.mjs
```

A suíte carrega as classes de produção, `ModNPC.register`, `Entities`,
`GlobalRegistry`, `GlobalNPC.register` e os loaders reais. Somente a ponte
com o Terraria é simulada. Verifica filtros, flags nativas e ambos os
sentidos de encadeamento de wrappers. Os casos `netMode` 0, 1 e 2 verificam
que o despacho é independente do modo; não substituem teste multiplayer
com dois dispositivos. A exceção da chamada original é injetada na
simulação e verifica a restauração e a recuperação na chamada seguinte.

## Integração Android

Requer APK debug completo, ADB com root e saves locais. Encerre a sessão
atual antes de preparar. O runner guarda preferências, habilita somente
este fixture e usa `BL_NPCAIType_Test.plr/.wld`, cópias dos saves existentes.
Restaura as preferências e remove somente o fixture ao terminar.

```powershell
python tools/tests/npcaitype/device.py prepare --order mod-first
python tools/tests/npcaitype/device.py launch --order mod-first
# Aguarde a conclusão no log do jogo.
python tools/tests/npcaitype/device.py collect --order mod-first
python tools/tests/npcaitype/device.py restore

python tools/tests/npcaitype/device.py prepare --order global-first
python tools/tests/npcaitype/device.py launch --order global-first
python tools/tests/npcaitype/device.py collect --order global-first
python tools/tests/npcaitype/device.py restore

python tools/tests/npcaitype/device.py prepare --order local-only
python tools/tests/npcaitype/device.py launch --order local-only
python tools/tests/npcaitype/device.py collect --order local-only
python tools/tests/npcaitype/device.py restore
```

O fixture observa `AI_002_FloatingEye` e `AI_003_Fighters` dentro do corpo nativo, compara
velocidade e direção com um Wandering Eye vanilla em condições controladas,
verifica vetos, herança e instâncias independentes e executa 300 chamadas
reais para conferir a restauração repetida. Também mantém um NPC com somente
`AIType` durante 30 ticks do loop normal do mundo, sem chamar sua IA pelo
fixture nesse intervalo. Os NPCs são temporários e
desativados em `finally`; a alteração temporária de `Main.dayTime` é
restaurada. Não concede itens nem muda blocos. Os logs ficam em
`build/npcaitype/<dispositivo>/`.

Os resultados da execução estão em [RESULTADOS.md](RESULTADOS.md).
