# ModItem: etapa de combate

O [plano](PLANO.md) cobre todos os métodos solicitados. Esta suite valida os 12 métodos da primeira etapa. Os métodos de uso, munição, cura, desenho, coleta, pilhas, estado e reforja ainda não foram adicionados por esta etapa.

## Testes automatizados

```powershell
node tools/tests/moditemhooks/check.mjs
node tools/tests/modplayerhooks/check.mjs
node tools/tests/modnpchooks/check.mjs
node tools/tests/globalprojectilehooks/check.mjs
node tools/tests/projectilekill/check.mjs
node tools/tests/localization/check.mjs
```

O teste de ModItem carrega os loaders reais em um ambiente que simula chamadas nativas. Verifica assinaturas e nomes dos parâmetros contra `refs/dump.cs`, referências, modificadores, ordem entre classes, vetos, registros herdados e tardios, marcas antes da entrada no JavaScript, exceções e restauração de contextos.

Os testes de contexto aninhado verificam a restauração do estado JavaScript. A ponte nativa mantém sua proteção contra reentrada no mesmo hook. A suite não remove essa proteção e a simulação não é uma medição de FPS.

## Execução nativa

O fixture usa itens, um NPC e um jogador temporários em singleplayer. Restaura os campos alterados e desativa o NPC em `finally`. O teste roda 60 atualizações após entrar no mundo e escreve `moditemhooks FIM checks=... falhas=...` no log.

Com o APK instalado e uma instância Android de teste conectada, prepare o fixture:

```powershell
python tools/tests/moditemhooks/device.py prepare --device 127.0.0.1:16384
```

O script preserva as preferências existentes, habilita somente seu próprio UID e instala o conteúdo numa pasta exclusiva. Use `--world NOME.wld` apenas para escolher um mundo de teste já existente; sem essa opção, ele conserva o mundo configurado no launcher. Inicie o jogo pelo launcher e leia o log do processo `com.bunnyloader:game`.

Depois da execução:

```powershell
python tools/tests/moditemhooks/device.py cleanup --device 127.0.0.1:16384
```

A limpeza restaura somente as preferências do fixture e confirma o caminho absoluto antes de remover sua pasta. Os backups locais ficam em `build/moditemhooks`.

O teste nativo de PvP usa dois objetos Player no mesmo processo. Não comprova comportamento multiplayer entre aparelhos. Os callbacks de PvP desta etapa usam o contexto de `ItemCheck_MeleeHitPVP`; recuperar a arma em uma mensagem `Hurt` isolada e validar entre processos permanecem pendentes.

Resultados e medição: [RESULTADOS.md](RESULTADOS.md). Contratos: [Combate de ModItem](../../../docs/referencia/moditem-combate.md).
