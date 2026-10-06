# ModItem: cura e atraso de poções

Os quatro métodos recebem o `item` da instância como primeiro argumento. Os modificadores usam `Ref<number>`: altere `.value`. O retorno dos modificadores é ignorado.

| Método | Contrato |
| --- | --- |
| `GetHealLife(item, player, quickHeal, healValue)` | Modifica a vida restaurada quando `item.healLife > 0`. Também participa da seleção de candidatos de `QuickHeal`. |
| `GetHealMana(item, player, quickHeal, healValue)` | Modifica a mana restaurada quando `item.healMana > 0`. |
| `ModifyPotionDelay(item, player, baseDelay)` | Modifica o atraso em ticks calculado pelo jogo móvel, antes da permissão e da aplicação do contador e do buff. |
| `ApplyPotionDelay(item, player, potionDelay)` | Padrão `true`. `false` impede o contador e o buff de doença de poção desta aplicação. |

## Cura normal e rápida

`quickHeal` é `true` na seleção de cura rápida e na aplicação feita por `QuickHeal` ou `QuickMana`; na aplicação normal é `false`. O contexto identifica o jogador que iniciou a operação. Uma cura de outro jogador durante esse contexto continua recebendo `false`.

Para cada canal com valor base positivo, a ordem é `ModItem`, `ModPlayer` e aplicação nativa. Os campos `healLife` e `healMana` são substituídos temporariamente para o cálculo e restaurados em `finally`, incluindo falhas do helper nativo. Os máximos de vida e mana, efeitos visuais, doença de mana e consumo continuam no jogo. O callback não deve consumir o item.

A seleção de `QuickHeal` usa o valor modificado de vida para comparar candidatos e restaura o campo após cada comparação. Pode consultar o mesmo item antes de aplicá-lo; evite efeitos persistentes nesse modificador. Pilhas vazias, itens que não são poções e candidatos sem cura base positiva não executam o callback de seleção. `QuickMana` conserva a escolha nativa do primeiro candidato válido com mana positiva.

Valores negativos são limitados a zero e frações são truncadas. Valores não finitos ou de outro tipo mantêm o valor nativo anterior. Valores muito grandes são limitados para impedir overflow na soma de inteiros do jogo. Um canal com valor base zero não é convertido em canal de cura por estes hooks, incluindo ModPlayer.

`StrangeBrew` conserva o sorteio nativo de vida, que usa `healLife` como limite inferior. Uma alteração de ModPlayer nesse item muda esse limite, e não substitui o resultado sorteado. Preserve o intervalo aceito pelo sorteio nativo ao modificá-lo. Itens novos de ModItem não possuem o tipo vanilla de StrangeBrew.

## Atraso de poções

1. O jogo calcula seu atraso, preservando os campos de duração e os casos especiais de RestorationPotion, Eggnog, Mushroom e StrangeBrew.
2. `ModItem.ModifyPotionDelay` recebe esse resultado em uma `Ref`.
3. `ModItem.ApplyPotionDelay` e `ModPlayer.ApplyPotionDelay` recebem o inteiro final. Ambos são consultados; qualquer `false` prevalece.
4. Se o resultado for positivo e permitido, `player.potionDelay` e o buff `PotionSickness` recebem a mesma duração.

Um veto ou uma duração zero conserva o contador anterior e impede o buff. Também impede a normalização de fome que ocorre dentro de `ApplyPotionDelay`. O uso nativo de Mushroom possui uma chamada adicional de fome fora desse método; ela mantém seu comportamento. O veto do atraso permite que a cura e o consumo continuem.

Os callbacks de permissão veem o contador anterior, antes de aplicar o novo valor. `AddBuff` de outro jogador, outro tipo de buff ou fora do contexto de `ApplyPotionDelay` conserva o comportamento original. Exceções de callback são registradas com classe e método; os demais callbacks continuam. Uma falha do helper nativo restaura o contador anterior e libera o contexto, sem desfazer efeitos nativos que já ocorreram.

No tModLoader, `ModifyPotionDelay` atua antes de `Player.PotionDelayModifier`. Esse modificador não existe no dump móvel usado pelo Bunny Loader. Aqui, `baseDelay` representa o atraso já calculado pelo jogo móvel, incluindo as reduções nativas. Não aplique novamente essas reduções. Essa diferença foi conferida nas [assinaturas de ModItem](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/ModItem.cs) e no [fluxo de Player do tModLoader](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/Player.cs.patch).

## Filtros e validação

Os planos de sobrescritas ficam em cache por classe. Marcas por tipo filtram a cura e o atraso antes de entrar no JavaScript. A comparação de cura rápida lê o quarto argumento do método estático nativo e usa `whileIn` para restringir a seleção. Os hooks auxiliares de buff e fome só entram durante o método de atraso marcado. As consultas custam O(1) por candidato e a seleção mantém a varredura nativa O(s), com s igual ao número de slots consultados.

Um observador de ModPlayer amplia as marcas uma vez no registro, incluindo itens registrados depois. ModItem e ModPlayer compartilham um único hook de aplicação de cura e um único hook de atraso, independentemente da ordem de registro.

A execução no APK validou cura normal, `QuickHeal`, `QuickMana`, consumo, refs, contador e buff, seleção de vida e composição com ModPlayer em singleplayer. A validação entre dois processos permanece pendente. Evidências: [resultado da etapa 3](../../tools/tests/moditemhooks/RESULTADOS-CURA.md).

## Exemplo

```js
GetHealLife(item, player, quickHeal, healValue) {
    healValue.value += 25;
}

GetHealMana(item, player, quickHeal, healValue) {
    healValue.value *= 1.5;
}

ModifyPotionDelay(item, player, baseDelay) {
    baseDelay.value = Math.trunc(baseDelay.value * 0.8);
}

ApplyPotionDelay(item, player, potionDelay) {
    return true;
}
```
