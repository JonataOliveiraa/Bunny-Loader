# Regressão de desenho, CloneDefaults e banners

Validado em 08/10/2026 contra o tModLoader 1.4.5,
commit `39e7995fa519a94468ebaf4699508d25903e143e`, e Terraria Mobile
1.4.5.8/301720 ARM64 no MuMu. A análise dos contratos e das alternativas
está em [compatibilidade-tmodloader-desenho-banners.md](../../../docs/nucleo/compatibilidade-tmodloader-desenho-banners.md).

## Resultados

| Verificação | Resultado |
|---|---|
| `check.mjs` | 27 casos comportamentais; JS de produção, chamadas nativas simuladas e assinaturas conferidas no dump |
| `verify_native.py` | Quatro blocos de instruções dos pontos Pre/Post conferidos na libil2cpp |
| Fixture no jogo, com ExampleMod carregado antes | **26 verificações, zero falhas** |
| Rodada posterior no jogo, com balanço do banner | **27 verificações, zero falhas**; 120 chamadas e 360 sprites, rotação variável |
| Regressão `npcaitype/check.mjs` | 39 casos |
| Regressão `globalprojectilehooks/check.mjs` | 59 casos e 22 assinaturas nativas |
| Regressão `moditemdata/check.mjs` | 67 casos, incluindo arraste/equipamento e persistência de instância |
| Suítes migradas para CombatLoader | ModItem: 138, ModNPC: 46, ModPlayer: 55; zero falhas |
| `pendingnetwork/check.mjs` | 8 casos: PvP recebido, repasse, veto, exceção, origem inválida e ordem de combate |
| [PvP e banner entre dois processos](../pendingnetwork/README.md) | Host: 12, cliente: 12, reconexão: 2; zero falhas |
| `:app:assembleDebug` | BUILD SUCCESSFUL |

Total das suítes de JS acima: **439 casos**. As chamadas nativas dessas
suítes são simuladas. As fixtures no jogo são contadas separadamente.

A fixture nativa confirmou ordem, argumentos e cores do inventário,
veto do sprite, Post após veto, retorno da escala, recursão no callback,
refs de rotação/escala chegando ao desenho do mundo, defaults vanilla e
de outro mod, dificuldades Clássica/Expert/Mestre, preservação de instância,
globais e participação dos jogadores, mapeamento compartilhado de banner,
50º abate, lista de resgate mobile, serialização e entrega do item pelo
pedido nativo com loopback, ativação do bônus pelo tile e recarga do save.

Evidência da troca de ordem: sem ExampleMod, o representante era **697**,
com item **6197**. Carregando o ExampleMod completo antes, passou a **700**,
com item **6280**. O sidecar recuperou 50 abates, um banner resgatável e o
aviso de novidade. O item e o tile do `ExampleSlimeNPC` também foram conferidos.
Nenhum erro de carregamento foi registrado; o aviso sobre emotes desligados
é uma limitação anterior do ExampleMod.

O save vanilla serializou **293** entradas. O pacote de rede da fixture
com ExampleMod serializou **702** entradas. Foi lido o inteiro do payload,
em vez de usar a capacidade inicial `NetPacket.Length = 65535` como prova.

## Executar

```powershell
node tools/tests/apiexpansion/check.mjs
python tools/tests/apiexpansion/verify_native.py
```

Para a execução nativa, instale o APK completo com o código atualizado.
`device.py` usa o MuMu `127.0.0.1:16384` e os saves locais `sda.plr` e
`Pesadelo_Odioso.wld` como fontes para cópias `BL_ApiDiag`. Esses nomes
devem existir nesse ambiente; o script não gera um mundo do zero.

```powershell
python tools/tests/apiexpansion/device.py prepare
python tools/tests/apiexpansion/device.py run
python tools/tests/apiexpansion/device.py collect
# Reiniciar testa a recarga do sidecar salvo pela primeira execução.
python tools/tests/apiexpansion/device.py run
python tools/tests/apiexpansion/device.py collect
# Inclui uma cópia isolada do ExampleMod e muda os IDs da fixture.
python tools/tests/apiexpansion/device.py example
python tools/tests/apiexpansion/device.py run
python tools/tests/apiexpansion/device.py collect
python tools/tests/apiexpansion/device.py restore
```

Espere a linha `apiexpansion WIND FIM` antes de coletar ou reiniciar;
ela vem após os 120 quadros de medição do banner.
`restore` compara byte a byte as preferências originais de settings, mods e
powers, remove as cópias dos saves/pacotes e relança a sessão original.
O backup e os logs ficam em `build/apiexpansion/` (ignorados pelo Git).
O pacote original instalado do ExampleMod é preservado.

APK conferido: `app/build/outputs/apk/debug/app-debug.apk`, 204975273 bytes.
SHA-256: `247e0085978390efcb42f3d51ce397f09b18c336294737bfe5bec023505fcf48`.

O ExampleMod foi empacotado como **1.9.0** para identificar a atualização
com o novo banner. Após essa alteração de manifesto, o APK foi recompilado
e o pacote `.bl` teve versão e presença dos novos arquivos conferidas.

## Limites

- O teste de balanço cobre um banner 1x3 com âncora sólida, vento ativo e a
  opção correspondente habilitada. Não cobre todas as texturas/alternates
  de terceiros nem constitui benchmark de desempenho de muitos banners.
- O PvP de item e os banners passaram entre dois processos no MuMu; os
  cenários e limites estão no relatório específico de rede.
- Não houve medição de FPS no aparelho físico do relato do EternalLaser.
  Os dados anteriores no MuMu e a comparação do ExMod estão em
  `out/diagnostics/eternal-laser/analise.md`; a sonda está disponível para
  capturar o cenário afetado sem mudar AIType ou extraUpdates.

Texturas de banner: ExampleMod do tModLoader, no commit citado, preservadas
sem edição. A textura de slime e a de item de teste vêm dos exemplos e das
fixtures existentes deste repositório.
