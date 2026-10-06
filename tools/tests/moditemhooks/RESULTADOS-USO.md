# ModItem: resultado da etapa 2

Implementados os 11 métodos de animação, frames e munição. Os contratos do tModLoader, o dump móvel, o disassembly de `Player` e a alternativa de animação do ExMod do TL Pro foram conferidos. A implementação mantém os pontos nativos de cálculo e consumo, com as adaptações documentadas em [Uso e munição](../../../docs/referencia/moditem-uso-municao.md).

## Testes automatizados

| Suite | Verificações aprovadas |
| --- | --- |
| ModItem | 98 |
| ModPlayer | 44 |
| ModNPC | 46 |
| GlobalProjectile | 59 |
| Ciclo de morte de projéteis | 16 |
| Localização | 95 |
| Total | 358 |

A suite de ModItem confere 21 assinaturas e nomes de parâmetros contra `refs/dump.cs`. Os casos novos incluem categoria de munição, vetos em ambos os itens, seleção e alternância de slots, ausência de munição, `NeedsAmmo`, estatísticas finais, conservação nativa, `dontConsume`, última unidade, erros e restauração de contexto.

Três ordens de registro de arma, munição e ModPlayer produziram um único hook de animação e um único hook de munição. A alteração de duração da arma compôs com o multiplicador de ModPlayer; as notificações ocorreram na mesma ordem. Os testes de reentrada verificam a restauração do estado JavaScript e não removem a proteção nativa do dispatcher.

## Execução no APK

MuMu Android arm64, uma instância, singleplayer. O fixture completo repetiu os 30 casos de combate e acrescentou 26 verificações de uso e munição: **56 verificações, zero falhas**. Log final: [native-use-results.log](native-use-results.log).

- UseAnimation participou do início da animação. UseItemFrame e HoldItemFrame alteraram o bodyFrame após o cálculo nativo.
- A seleção rejeitou candidatos, avançou ao slot seguinte, aceitou outra categoria e respeitou o veto da munição, inclusive com arma vanilla.
- A alternância nativa considerou apenas os candidatos aceitos.
- NeedsAmmo false forneceu munição temporária sem alterar o inventário; a recusa de uso permaneceu válida.
- PickAmmo alterou projétil, velocidade, dano total e repulsão finais.
- Os vetos da arma, munição e ModPlayer conservaram a última unidade sem alterar permanentemente consumable.
- As três notificações viram os tipos originais na última unidade; TurnToAir nativo limpou o item depois.
- dontConsume e munição não consumível conservaram a pilha e suprimiram notificações.
- ModItem e ModPlayer compartilharam um único hook de Player.PickAmmo.

O primeiro build de teste rejeitou o filtro de frames porque `selectedItem` é uma propriedade. O índice foi corrigido para o campo da struct `selectedItemState.selected`; seus offsets são resolvidos por metadados e pelo ajuste de cabeçalho de structs da ponte. O disassembly do getter confirma a mesma leitura.

A primeira medição de frames somava o hook de armaduras existente com o novo hook de ModItem. O fixture passou a registrar a posição do hook instalado pela classe e medir cada contador. Em 100 chamadas por grupo, os deltas foram `100,0` para vanilla e `100,0` para mod sem sobrescritas: o hook de armaduras continuou entrando no JavaScript, e o novo hook de ModItem teve zero entradas. Isso comprova a filtragem deste hook, sem atribuir a ele a otimização dos outros caminhos de PlayerFrame. Não é uma medição de FPS.

Os campos temporários e o inventário foram restaurados em finally. A limpeza usou backups próprios desta sessão, restaurou apenas suas preferências, confirmou o caminho e removeu seu UID. O emulador iniciado para o teste foi desligado.

## Build, catálogo e revisão

Build Android arm64 debug concluído em 33 s, com um worker e heap máximo de 1536 MiB. A assinatura APK v2 foi verificada. O APK instalado, testado e copiado para `out/bunny-loader.apk` tem SHA-256:

```text
F8CB3D71E4184B544C0386E1A1BDA15836DFBC3748905A93A99DABC657F866AE
```

O catálogo contém 1.066 registros, sendo 1.056 declarados e 10 indisponíveis, em 64 classes e 8 objetos/enums. JSON, CSV e Excel foram confrontados célula a célula; filtros e painéis fixos foram conferidos. As assinaturas novas da planilha foram renderizadas e revisadas mantendo o formato anterior.

A revisão concentrou-se nos filtros antes do JavaScript, nos offsets e limites do índice, na preservação da ordem de slots, na instalação única, nos vetos e na restauração da última unidade. Os planos e rótulos ficam em cache por classe. A varredura dos candidatos é O(s); o filtro de frames é O(1), sem varrer o inventário por quadro. O código novo não acrescenta comentários inline.

As notificações de consumo são uma adaptação móvel: recebem a pilha já decrementada. PickAmmo modifica o dano total calculado pelo jogo, sem separar a parcela da munição como o tModLoader. Esses limites estão documentados e fazem parte dos testes.

Esta etapa foi validada em singleplayer. Não foi executada uma sessão com dois processos; essa validação e a origem PvP por rede seguem pendentes. Cura, desenho, coleta, pilhas, persistência e reforja permanecem nas etapas seguintes.
