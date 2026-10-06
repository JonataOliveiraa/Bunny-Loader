# ModItem: animação, frames e munição

Os callbacks recebem o `item` da instância como primeiro argumento. Sobrescreva apenas os métodos necessários. Métodos de arma recebem a munição como segundo argumento; métodos de munição recebem a arma nessa posição.

## Animação e frames

| Método | Contrato |
| --- | --- |
| `UseAnimation(item, player)` | Antes de `Player.ApplyItemAnimation` calcular a duração. Alterações em `item.useAnimation` participam do cálculo nativo. Modificadores de velocidade de ModPlayer são aplicados em seguida. |
| `UseItemFrame(item, player)` | Depois de `Player.PlayerFrame`, com `itemAnimation > 0`. Pode alterar `player.bodyFrame`, `legFrame` e os demais campos de desenho do jogador. |
| `HoldItemFrame(item, player)` | Depois de `Player.PlayerFrame`, com `itemAnimation <= 0`, desde que `CanVisuallyHoldItem` permita desenhar o item. |

Os frames são uma adaptação do ponto de inserção do tModLoader: executam depois do método nativo completo do jogo móvel. Seus retornos são ignorados. O filtro nativo lê `inventory[selectedItemState.selected].type` e verifica a marca por tipo antes de adquirir a trava do JavaScript. Não percorre o inventário por quadro. Itens sem sobrescritas não entram nesse callback do dispatcher. O hook de armaduras já existente possui seu próprio fluxo e seus próprios contadores.

## Seleção e consumo

| Método | Quem recebe | Padrão e comportamento |
| --- | --- | --- |
| `NeedsAmmo(item, player)` | Arma | `true`. `false` permite usar uma instância temporária da munição padrão quando nenhum candidato é encontrado. Ela não entra no inventário e não é consumida. A categoria precisa corresponder ao campo `ammo` do item padrão. |
| `CanChooseAmmo(item, ammo, player)` | Arma | `null`. `false` rejeita o candidato; `true` permite outra categoria. |
| `CanBeChosenAsAmmo(item, weapon, player)` | Munição | `null`. Compõe com a decisão da arma. Qualquer `false` prevalece. Sem decisão explícita, vale `ammo.ammo === weapon.useAmmo`. |
| `CanConsumeAmmo(item, ammo, player)` | Arma | `true`. `false` conserva a pilha sem impedir o tiro. |
| `CanBeConsumedAsAmmo(item, weapon, player)` | Munição | `true`. `false` conserva a pilha. |
| `OnConsumeAmmo(item, ammo, player)` | Arma | Notificação de consumo efetivo. |
| `OnConsumedAsAmmo(item, weapon, player)` | Munição | Notificação de consumo efetivo. |
| `PickAmmo(item, weapon, player, type, speed, damage, knockback)` | Munição | `type`, `speed` e `knockback` são `Ref<number>`. `damage` é um `StatModifier` aplicado ao dano final calculado pelo jogo móvel. |

`undefined` também preserva a decisão padrão das permissões. Pilhas vazias e itens de ar não são candidatos. Retornar `true` indiscriminadamente permite qualquer item com pilha positiva, inclusive a própria arma; restrinja essa permissão ao candidato desejado.

A seleção mantém as listas de slots fornecidas pelo jogo e os modos de alternância de munição da versão móvel. O índice de alternância é aplicado apenas aos candidatos aceitos; armas com as exceções nativas de seleção conservam a primeira pilha. A varredura custa O(s), com s igual à quantidade de slots daquele intervalo, e acontece somente quando a seleção é consultada. Uma sobrescrita na munição precisa observar armas vanilla; suas marcas são ampliadas no registro, incluindo tipos registrados posteriormente.

## Ordem e adaptação móvel

1. Selecionar um candidato e consultar os vetos de consumo da arma, da munição e de ModPlayer.
2. Executar `PickAmmo` nativo, preservando bônus, combinações de projéteis e chances de economia do jogo.
3. Executar `ModItem.PickAmmo` da munição, ajustando os resultados antes de retorná-los ao chamador.
4. Havendo decremento efetivo, chamar `OnConsumeAmmo` da arma, `OnConsumedAsAmmo` da munição e `ModPlayer.OnConsumeAmmo`, nessa ordem.
5. Limpar a última unidade com `TurnToAir` nativo.

No tModLoader, as notificações antecedem o decremento. No Bunny Loader, o decremento continua no método nativo; os callbacks recebem a pilha já reduzida. Na última unidade, `stack` é zero, mas o tipo e a instância permanecem disponíveis até as notificações terminarem. O `StatModifier` de `PickAmmo` opera sobre o dano total móvel, e não sobre a contribuição separada da munição usada pelo tModLoader.

`dontConsume`, munição não consumível, veto ou economia nativa não geram notificações. `PickAmmo` ainda pode ajustar os resultados de uma consulta sem consumo. Não force uma nova redução de `stack` em uma notificação.

Os valores finais de projétil e dano são inteiros não negativos. Velocidade e repulsão são finitas não negativas. Valores não finitos devolvem o resultado nativo anterior. Exceções de callback são atribuídas à classe e ao método; as outras notificações continuam, e a limpeza e os campos temporários são restaurados em `finally`.

Existe um único hook compartilhado de `Player.PickAmmo`, independentemente da ordem de registro de ModItem e ModPlayer. Os hooks auxiliares de seleção do item e de limpeza só entram no JavaScript durante esse contexto. Esses callbacks não enviam pacotes adicionais; a seleção participa do fluxo local existente. A execução em dois processos ainda precisa de validação específica.

## Exemplos

```js
NeedsAmmo(item, player) {
    return false;
}

CanConsumeAmmo(item, ammo, player) {
    return player.itemAnimation <= item.useTime;
}
```

Na classe da munição:

```js
CanBeChosenAsAmmo(item, weapon, player) {
    return weapon.useAmmo === Terraria.ID.AmmoID.Arrow ? true : null;
}

PickAmmo(item, weapon, player, type, speed, damage, knockback) {
    speed.value += 2;
    damage.Flat += 5;
}
```

Contratos consultados: [ModItem](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/ModItem.cs), [ItemLoader](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/ItemLoader.cs) e [pontos de inserção em Player](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/Player.cs.patch). A alternativa de `UseAnimation` no ExMod do TL Pro também foi conferida. Evidências locais: `refs/dump.cs` e disassembly das rotinas móveis de animação, seleção, uso e consumo.
