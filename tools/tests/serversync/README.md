# Versões na sincronização de mods

## Análise

Requisito: com sincronização ligada, host com um mod 1.1.0 e cliente com o
mesmo uid em 1.0.0 não podem entrar juntos antes da sincronização.

Fatos verificados no código e reproduzidos pelos testes anteriores à correção:

- `ServerSyncLoader` já comparava uid, ordem e versão; acrescentar apenas uma
  comparação de versão não resolveria a origem do problema.
- O launcher aceitava `manifest.json` e `mod.json`, mas o núcleo só lia o
  primeiro. Um mod com `mod.json` chegava ao handshake com versão vazia.
- Duas versões vazias eram aceitas como iguais.
- O planejamento Kotlin comparava versões, mas a admissão do ZIP baixado e
  a gravação final da sessão não conferiam a versão exigida pelo host.

Premissa: a versão do manifesto é o identificador de compatibilidade
publicado pelo autor. O protocolo não autentica conteúdo por hash.

Casos de borda: versão ausente ou em branco, cliente mais novo, prerelease,
loja offline, versão exigida fora da loja, índice e ZIP divergentes, uid
inválido/duplicado, tipo/formato incompatível, mod alterado entre planejamento
e sessão, ordem diferente, lista vazia e sincronização desativada pelo host.

## Design

| Alternativa | Tempo, memória e manutenção |
|---|---|
| Comparar a versão apenas no JS | O(n), O(1) adicional na comparação; já existia e não resolve metadados ausentes nem ZIP divergente |
| Alinhar metadados e validar cada fronteira | Escolhida: comparação O(n), planejamento O(n + m + k) com mapas O(n + m + k), mantendo protocolo e formatos existentes |
| Trocar o protocolo por hashes de todos os arquivos | Exige I/O proporcional ao conteúdo, cache/invalidação e compatibilidade de protocolo; escopo maior que a correção de versões |

`n` é a lista do host, `m` a de instalados e `k` a da loja. O núcleo usa os
mesmos nomes/prioridade de manifesto do launcher. O handshake recusa versão
em branco. `ServerModSync` concentra igualdade exata, planejamento, admissão
e conferência final da sessão. O callback do importador roda antes de
substituir a instalação. As verificações acontecem na entrada/sincronização,
sem acrescentar um hook de atualização por quadro no QuickJS.

## Testes sem o jogo

```powershell
node tools/tests/serversync/check.mjs
gradle :app:testDebugUnitTest :app:assembleDebug --console=plain
```

```bash
# Linux ou WSL, na raiz do repositório; requer g++ com C++20.
python3 tools/tests/serversync/native.py
```

- `check.mjs`: carrega o ServerSyncLoader de produção em VM com transporte
  simulado. Exercita apresentação UUID, leitura pelo host, decisão, oferta
  e restauração do UUID original; não copia a função privada de comparação.
- `native.py` / `native.cpp`: compilam o ModLoader.cpp de produção com
  substitutos mínimos do motor/texturas/log, ASan e UBSan. Usam arquivos
  reais para testar os dois nomes, prioridade, campo aninhado, diretório em
  vez de manifesto e versão ausente.
- `ServerModSyncTest`: testa a política Kotlin de produção para atualização,
  retorno à versão do host, falhas/offline, pacote incompatível e sessão.
  A admissão é testada como política; esta suíte não executa a transferência
  HTTP e instalação de um ZIP no Android.

## Teste Android isolado

`device.py` requer Python, ADB, APK debug instalado e emuladores com `su 0`.
O jogo precisa estar fechado antes de `prepare`. A preparação guarda os
saves originais numa pasta irmã, cria cópias de um personagem/mundo, salva
preferências/cache da loja e habilita somente o fixture deste diretório.
Não substitua `build/serversync/<serial>/prepared.json` nem seus backups
enquanto uma preparação estiver em uso. Execute `restore` também após uma
rodada interrompida: ele verifica hashes dos saves e bytes dos arquivos
originais de preferências/cache.

Exemplo com duas instâncias já conectadas ao ADB:

```powershell
python tools/tests/serversync/device.py prepare --device 127.0.0.1:16384 --version 1.1.0
python tools/tests/serversync/device.py prepare --device 127.0.0.1:16416 --version 1.0.0 --legacy
python tools/tests/serversync/device.py launch --device 127.0.0.1:16384
python tools/tests/serversync/device.py launch --device 127.0.0.1:16416
```

No host, hospede o mundo copiado com sincronização ligada. No cliente,
conecte ao host usando o personagem copiado. Configure o encaminhamento de
rede conforme o emulador: as duas instâncias podem estar em NATs separados.
Colete logs/capturas com `collect --device SERIAL --label different`.
Espere a tela de falta da versão 1.1.0, sem entrada no mundo do cliente.

```powershell
python tools/tests/serversync/device.py version --device 127.0.0.1:16416 --version 1.1.0 --legacy
python tools/tests/serversync/device.py launch --device 127.0.0.1:16416
```

Conecte de novo, mantendo o host aberto. Ambos devem entrar na mesma sessão;
colete com `--label equal`. Os logs `serversync-probe WORLD` e `CONNECT`
permitem conferir versão e lado. O fixture não registra conteúdo de jogo.

```powershell
python tools/tests/serversync/device.py restore --device 127.0.0.1:16416
python tools/tests/serversync/device.py restore --device 127.0.0.1:16384
```

Resultados desta execução: [RESULTADOS.md](RESULTADOS.md).
