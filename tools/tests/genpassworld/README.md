# GenPass e PassLegacy: geração completa

Este fixture cria um mundo pequeno pelo menu normal do Terraria mobile 301720. O jogo inicia `WorldGen.worldGenCallback` por `ThreadManager.RunForUserCritical`. Nenhuma checagem de thread é substituída e nenhuma etapa nativa de terreno é removida ou desativada.

`BeforePass` e `StructurePass` estendem diretamente `GenPass` e sobrescrevem `ApplyPass(progress, configuration)`. A primeira etapa recebe os objetos nativos antes do terreno. A segunda roda depois de Final Cleanup e coloca uma estrutura de 7 × 5 blocos de cobre e estanho. Um PassLegacy posterior confirma os 35 blocos. Uma terceira subclasse é desativada e deve permanecer sem chamadas.

O teste verifica a thread gerenciada de geração, a ordem, os argumentos nativos, peso alterado, Enable/Disable, contagem exata de chamadas, etapas nativas concluídas, PostWorldGen, validação do `.wld` e persistência dos blocos após reiniciar o processo e carregar o arquivo salvo. A prova dos callbacks fica no arquivo auxiliar `.wld.bl.json` do sistema.

## Executar

```powershell
python tools/tests/moditemhooks/device.py prepare --fixture genpassworld --run-id meu-teste --world '-' --isolate
```

Inicie o jogo, escolha um personagem de teste e crie um mundo pelo menu. O fixture fixa um nome exclusivo `BL_GenPass_meu-teste`, tamanho pequeno e dificuldade clássica. O helper prepara `config.js` com o mesmo nome nos retries. Registre `GENPASS_GENERATED` e `GENPASS_SAVED` do log.

Feche o processo e reabra o mundo gerado com o mesmo fixture habilitado. A prova carregada e os blocos devem produzir `GENPASS_RELOADED` com `generationCallbacksThisProcess: 0`, demonstrando que a estrutura veio do arquivo salvo. Valide o log reunindo as duas execuções:

```powershell
python tools/tests/genpassworld/verify.py caminho-do-log.txt
```

Limpe as preferências e o pacote temporário:

```powershell
python tools/tests/moditemhooks/device.py cleanup --fixture genpassworld --run-id meu-teste
```

Esse comando preserva o mundo gerado. Se precisar removê-lo, confirme o caminho exato do arquivo novo e remova somente seus arquivos auxiliares. Não reutilize um nome de mundo existente.

Verifique também que a prova carregada permanece intacta em um salvamento posterior:

```powershell
node tools/tests/genpassworld/check.mjs
```

## Resultado nativo

Execução em **2026-10-06**, no MuMu/Android, com o APK de `out/bunny-loader.apk` e Terraria mobile **301720**. O [log dos resultados](native-results.log) passou por `verify.py`:

- Mundo **BL_GenPass_full-20261006-a**, **4200 × 1200**.
- Thread criadora **1**; geração na thread **5**, identificada como `worldGenCallback`.
- **106 etapas nativas executadas**, com todas as etapas de terreno preservadas.
- Ordem: PreWorldGen, subclasse BeforePass, subclasse StructurePass, PassLegacy e PostWorldGen. Cada etapa personalizada ativa executou uma vez; a subclasse desativada executou zero vezes.
- Estrutura de **35 blocos** presente ao terminar a geração, após salvar e depois de reiniciar o processo. Na recarga, os callbacks de geração do novo processo permaneceram em zero.
- Arquivo `.wld` validado pelo jogo, **2.989.962 bytes**, formato nativo **326**. Cópia de evidência em `build/genpassworld/full-20261006-a/world/`.

Nenhuma correção no código de produção foi necessária. A suíte ModSystem passou em **32 grupos de verificações**; o teste adicional de salvar a prova carregada também passou. Os mundos temporários e o pacote do fixture foram removidos do emulador, com as preferências restauradas.

## Cobertura

O teste é singleplayer. Não executa conversão completa de Hardmode nem sincronização entre aparelhos. O [fixture ModSystem](../modsystemhooks/README.md) cobre os contratos adicionais e os testes automatizados incluem subclasses com estado próprio e isolamento de uma exceção em ApplyPass.
