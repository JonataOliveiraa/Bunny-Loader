# ModSystem: implementação e validação

## Análise

A referência é a cópia local de `exmod_tmod/tModLoader/patches/tModLoader/Terraria/ModLoader/ModSystem.cs`, junto das chamadas de `SystemLoader`, `Main`, `WorldGen`, `MessageBuffer` e `NetMessage`. Assinaturas móveis foram conferidas em `refs/dump.cs`; pontos internos foram conferidos em `refs/script.json` e `refs/libil2cpp.so` da versão 301720.

Entradas: sistemas registrados, lista nativa de etapas, estado do mundo e argumentos de desenho/rede. Saídas: callbacks nas bordas dos grupos, etapas editadas, valores por Ref e vetos. Casos cobertos: nenhuma sobrescrita, registros posteriores, geração cancelada, etapas desativadas, pesos inválidos, taxas fracionárias e não finitas, menus, exceções, cabeçalho ausente, leitores substituídos e descarregamento repetido.

## Projeto

| Abordagem | Tempo por atualização | Espaço | Manutenção |
| --- | --- | --- | --- |
| Interceptar cada entidade e contar as bordas | Proporcional às entidades, além dos sistemas interessados | Contadores e estados intermediários | Depende da quantidade e de caminhos de atualização de cada entidade |
| Interceptar diretamente as bordas dos grupos | Proporcional aos sistemas interessados por callback | Listas de sobrescritas em cache | Nove pontos internos com verificação de instruções, além dos métodos públicos |

A implementação usa a segunda abordagem. Nenhum ponto de grupo é instalado sem uma sobrescrita correspondente. As listas são invalidadas no registro de um sistema, sem procurar sobrescritas ou criar uma função por ouvinte a cada tick. A edição das etapas custa O(n), onde n é a quantidade de etapas, e ocorre na geração.

`GenPass` e `PassLegacy` JS possuem um PassLegacy nativo; o hook de ApplyPass encaminha as etapas personalizadas para seus callbacks. As etapas nativas existentes mantêm sua implementação. A lista JS fica referenciada durante a chamada do gerador.

## Testes automatizados

```powershell
node tools/tests/modsystemhooks/check.mjs
python tools/tests/modsystemhooks/verify_native.py
```

Resultado: **32 grupos de verificações**, **27 hooks de métodos nativos** exercitados pela suíte e **9 pontos internos** com instruções conferidas no binário. O teste também confere assinaturas e nomes dos parâmetros contra o dump.

As verificações incluem ordem de execução, valores Ref, três taxas independentes, listas de geração, sucesso/cancelamento, Hardmode, receitas, cache de ouvintes, cabeçalhos, rejeição de mundos, envio/recebimento e isolamento de exceções. Subclasses diretas de GenPass preservam o estado da instância, recebem os argumentos de ApplyPass e respeitam Enable/Disable; uma exceção na subclasse permite executar o PassLegacy seguinte. A câmera usa um único hook e preserva ModPlayer antes de ModSystem nas duas ordens de registro. Um veto permanece válido mesmo se a mensagem de rejeição lançar uma exceção ou se um callback que cancelou um pacote fornecer Ref inválida.

Regressões: ModPlayer **55**, ModItem **138**, localização **95**, PlayerDrawLayer **28** e cabelo **11** verificações passaram. Esses testes usam um ambiente JS controlado; não substituem execução no jogo.

## Fixture nativo em singleplayer

```powershell
python tools/tests/moditemhooks/device.py prepare --fixture modsystemhooks --run-id minha-execucao --isolate
```

Inicie o jogo com um personagem e mundo de teste. O fixture registra as chamadas e emite `MODSYSTEM_RESULT` depois de 240 ticks. A limpeza usa o mesmo identificador:

```powershell
python tools/tests/moditemhooks/device.py cleanup --fixture modsystemhooks --run-id minha-execucao
```

O helper preserva e restaura as preferências alteradas, instala o UID exclusivo do fixture e verifica os caminhos antes de removê-los. Os backups ficam em `build/modsystemhooks/<run-id>`.

O fixture valida no gerador nativo a inserção de duas etapas, a desativação da etapa original, a ordem antes/depois e PostWorldGen. Como a chamada sintética ocorre na thread de atualização, somente durante esse teste o fixture pula `ThreadManager.CheckThreadTerminiate`: o gerador normal exige uma thread gerenciada de geração. O código de produção conserva essa verificação. As etapas sintéticas não geram terreno; não é um teste de criação completa de mundo nem de geração em uma thread de trabalho.

A etapa padrão de Hardmode é desativada apenas pelo fixture antes de executar uma etapa temporária. Isso comprova a edição da lista, sem converter o mundo de teste. O recebimento usa MessageBuffer real, cancela um pacote temporário e verifica a saída por Ref. O teste das taxas usa quatro ticks reais com clock 0,25, tiles 0,5 e eventos 0,75, verificando o avanço total do relógio e a acumulação independente dos campos inteiros. Um ModPlayer temporário verifica a ordem da câmera compartilhada no jogo.

O resultado da execução está em [native-singleplayer.log](native-singleplayer.log). Os testes de rede desta rodada não comprovam uma sessão multiplayer entre dispositivos. Persistência de cabeçalho, bloqueio do menu e descarregamento foram validados na suíte automatizada; não são afirmados como testes completos de UI/salvamento no aparelho.

## Geração completa com GenPass e PassLegacy

O [fixture genpassworld](../genpassworld/README.md) foi executado separadamente pelo menu normal, na thread gerenciada `worldGenCallback`, sem substituir checagens de thread. Gerou um mundo de **4200 × 1200**, completou as **106 etapas nativas**, executou duas subclasses diretas de GenPass e um PassLegacy na ordem esperada e manteve uma etapa desativada sem chamadas. A estrutura de **35 blocos** persistiu no `.wld` válido e foi conferida após reiniciar o processo e carregar o mundo. Os [resultados nativos](../genpassworld/native-results.log) passaram pelo verificador independente do fixture. Essa execução complementa o gerador sintético descrito acima; não executa a conversão completa de Hardmode.

## Método descartado

`RequiresScreenTarget`: o renderer móvel usa Unity, não chama `FilterManager.CanCapture` e não tem `Main.finalScreenTarget`. Main.screenTarget não constitui a captura final solicitada pelo método. Retornar true não teria o efeito esperado do tModLoader.

## Adaptações

`ModifyWorldGenTasks(tasks)` tem um argumento, seguindo a referência local atual. PreWorldGen roda depois de montar a lista móvel e antes de executá-la. Hardmode expõe a etapa única **Hardmode Conversion**, em vez dos passos Good/Evil/Walls. Cabeçalhos usam o arquivo auxiliar local `.wld.bl.header.json`; não há persistência em nuvem. Contratos e exemplos: [modsystem-hooks.md](../../../docs/referencia/modsystem-hooks.md).
