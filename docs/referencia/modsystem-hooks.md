# ModSystem: geração, atualização e integração móvel

Implementação conferida com `exmod_tmod/tModLoader/patches/tModLoader/Terraria/ModLoader/ModSystem.cs` e `refs/dump.cs`. A assinatura atual local de `ModifyWorldGenTasks` recebe somente `tasks`.

## Etapas de geração

```javascript
export class OreSystem extends ModSystem {
    ModifyWorldGenTasks(tasks) {
        const index = tasks.findIndex(pass => pass.Name === 'Shinies');
        if (index < 0) return;
        tasks.splice(index + 1, 0, new PassLegacy('Meu minério', (progress, configuration) => {
            progress.Message = 'Gerando minério';
            progress['void Set(double value)'](1);
        }, 1));
    }
}
```

O exemplo ilustra a inserção. Acrescente a geração dentro do callback, usando o tipo de bloco, a API nativa de geração e os limites do mundo.

`tasks` é um array JavaScript de `GenPass`. As etapas nativas também são representadas por essa classe. É possível inserir, excluir, reordenar, alterar `Weight` e chamar `Disable()`/`Enable()`. O loader valida os tipos e os pesos antes de substituir a lista nativa. Uma lista inválida é reportada e a lista nativa permanece disponível.

`PreWorldGen()` executa antes da execução das etapas, depois de o jogo montar a lista. `PostWorldGen()` executa quando `WorldGenerator.GenerateWorld()` retorna `true`, incluindo a execução das etapas adicionais. Uma geração cancelada não chama o pós-hook.

`GenPass` aceita nome e peso; subclasses implementam `ApplyPass(progress, configuration)`. `PassLegacy` aceita nome, função e peso opcional, com padrão 1. Os argumentos do callback são os objetos nativos `GenerationProgress` e `Terraria.IO.GameConfiguration`. Uma etapa desativada não executa. Exceções JavaScript de uma etapa são reportadas sem impedir as demais etapas.

## Hardmode

No celular, a conversão está concentrada em `WorldGen.initializeHardMode()`. A lista inicial de `ModifyHardmodeTasks(tasks)` contém **Hardmode Conversion**, que executa essa rotina nativa. Adicione etapas antes ou depois dela ou desative-a. Os nomes separados `Hardmode Good`, `Hardmode Evil` e `Hardmode Walls` do tModLoader não existem nessa adaptação; procure defensivamente pela etapa móvel. Seus callbacks recebem `null` para progresso e configuração, como as etapas de Hardmode locais.

## Atualização e desenho

| Método | Contrato / ponto móvel |
| --- | --- |
| `UpdateUI(gameTime)` | Depois de `Main.UpdateUIStates`, dentro do mundo; inclui atualização de UI durante pausa. |
| `PostUpdateInput()` | Depois de `Main.DoUpdate_HandleInput`. |
| `PreUpdateEntities()` | Entrada de `Main.DoUpdateInWorld`, antes dos grupos. |
| `PreUpdatePlayers()`, `PostUpdatePlayers()` | Bordas do laço de jogadores. |
| `PreUpdateNPCs()`, `PostUpdateNPCs()` | Bordas do grupo de NPCs. |
| `PreUpdateGores()`, `PostUpdateGores()` | Bordas do laço de gores. |
| `PreUpdateProjectiles()`, `PostUpdateProjectiles()` | Antes e depois do grupo de projéteis. |
| `PreUpdateItems()`, `PostUpdateItems()` | Bordas do laço de `WorldItem`. |
| `PreUpdateDusts()`, `PostUpdateDusts()` | Antes e depois de `Dust.UpdateDust`. |
| `PreUpdateInvasions()`, `PostUpdateInvasions()` | Antes e depois de `Main.UpdateInvasion`, quando a rotina nativa executa. |
| `ModifyTimeRate(timeRate, tileUpdateRate, eventUpdateRate)` | Três `Ref` independentes. O relógio usa `double`; tiles e eventos acumulam frações entre ticks para alimentar os campos inteiros móveis. Valores não finitos, negativos ou acima de `Int32.MaxValue` mantêm o valor do jogo. |
| `ModifySunLightColor(tileColor, backgroundColor)` | Dois `Ref<Color>` depois do cálculo nativo das cores do céu e dos tiles. |
| `ModifyLightingBrightness(scale)` | `Ref<float>` depois de `Lighting.UpdateGlobalBrightness`; valores não finitos e negativos são ignorados. |
| `ModifyScreenPosition()` | Depois do cálculo nativo da câmera e de ModPlayer, dentro do mundo. Altere `Main.screenPosition`. O hook é compartilhado e a ordem independe do registro. |
| `ModifyTransformMatrix(transform)` | `Ref<SpriteViewMatrix>` depois do zoom nativo, antes de consumir a matriz no desenho. |

Os callbacks percorrem listas de sobrescritas calculadas quando sistemas são registrados. Os pontos nativos dentro dos laços preservam os registradores e conferem quatro instruções do binário antes de instalar. Um sistema sem sobrescritas de atualização não instala esses pontos. Os callbacks de grupo executam uma vez por grupo, e não uma vez por entidade.

## Conteúdo, mundo e rede

| Método | Contrato |
| --- | --- |
| `ResizeArrays()` | Conteúdo nativo pronto, antes das fases de setup dos mods. |
| `OnLocalizationsLoaded()` | Depois de aplicar os JSONs dos mods, inicialmente e ao recarregar o idioma. |
| `PostSetupRecipes()` | Depois de todas as receitas, `PostAddRecipes` e reconstrução dos caches. |
| `OnModUnload()` | Uma vez na saída normal ou no shutdown do motor JS. Encerramento forçado pelo sistema não executa callbacks. |
| `SaveWorldHeader(tag)` | `TagCompound` separado dos dados completos, em `<mundo>.wld.bl.header.json`. Mantém entradas de mods ausentes. Sem persistência em nuvem. |
| `CanWorldBePlayed(playerData, worldData)` | Qualquer `false` impede seleção/carregamento. Recebe `PlayerFileData` e `WorldFileData`. |
| `WorldCanBePlayedRejectionMessage(playerData, worldData)` | Mensagem do primeiro sistema que bloqueou. Texto vazio recebe uma mensagem padrão. |
| `HijackGetData(messageType, reader, playerNumber)` | `Ref<byte>` e `Ref<BinaryReader>` nativo. A posição do leitor volta a 1 entre sistemas. Qualquer `true` cancela o pacote inteiro. `playerNumber` é `MessageBuffer.whoAmI`. Trocar o leitor copia seu payload para a rotina móvel. |
| `HijackSendData(whoAmI, msgType, remoteClient, ignoreClient, text, number, number2, number3, number4, number5, number6, number7)` | Qualquer `true` cancela `SendData` e o envelope de dados de entidade. `whoAmI` é o índice do buffer: `remoteClient` no envio direcionado do servidor e 256 nos outros casos. `TrySendData` também passa por `SendData`. |

Consulte o cabeçalho sem carregar os dados completos do mundo:

```javascript
const header = new Ref();
if (worldData.TryGetHeaderData(MySystem, header)) {
    const enabled = header.value.GetBool('enabled');
}
```

`TryGetHeaderData` também aceita a instância do sistema. Ao mover um mundo manualmente, leve os dois arquivos auxiliares `.bl.json` e `.bl.header.json`.

## Método descartado

`RequiresScreenTarget` não foi exposto. O renderer móvel usa Unity, não chama `FilterManager.CanCapture` e não possui `Main.finalScreenTarget`; retornar `true` nesse método não ativaria uma captura de tela compatível.

## Validação

Veja [suíte e limites dos testes](../../tools/tests/modsystemhooks/README.md). O teste do gerador usa etapas temporárias no gerador real; ele não substitui uma geração completa de terreno nem uma conversão completa de Hardmode. Os testes de rede desta rodada não equivalem a uma sessão multiplayer entre dispositivos.
