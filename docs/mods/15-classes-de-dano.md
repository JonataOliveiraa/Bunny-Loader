# 15. Classes de dano (`DamageClass`)

A classe de dano diz três coisas sobre um ataque:

1. **de quais bônus ele se beneficia**: o acessório "+10% de dano corpo a
   corpo" vale para a espada, não para o cajado;
2. **que efeitos ele ativa**: a Pedra de Magma põe fogo nos golpes corpo a
   corpo, a armadura Spectre solta raios nos ataques mágicos;
3. **que prefixos aceita**: "Lendário" é de arma corpo a corpo, "Místico" de
   arma mágica.

No jogo isso são quatro flags fixas no item (`melee`, `ranged`, `magic`,
`summon`) e um campo por classe no jogador (`meleeDamage`, `meleeCrit`...). Não
cabe uma quinta. O `DamageClass` do tModLoader troca isso por objetos: as
classes do jogo viram objetos prontos, e um mod cria classes novas (um
"ladino", um "bardo") com o mesmo poder das do jogo, que os outros mods também
enxergam.

## Usar uma classe do jogo

```js
SetDefaults() {
    this.Item.DamageType = DamageClass.Magic;
}
```

| Classe | O que é |
|---|---|
| `DamageClass.Default` | Sem classe: não ganha bônus nenhum (dano de armadilha, de água jogada). |
| `DamageClass.Generic` | A base de todas. Um "+10% de todo dano" vai aqui, e toda classe que herda dela ganha. |
| `DamageClass.Melee` | Corpo a corpo. |
| `DamageClass.MeleeNoSpeed` | Corpo a corpo que não fica mais rápido com velocidade de ataque (lanças). |
| `DamageClass.Ranged` | À distância. |
| `DamageClass.Magic` | Mágica. |
| `DamageClass.Summon` | Invocação (lacaios, sentinelas): sem crítico. |
| `DamageClass.SummonMeleeSpeed` | Chicotes: invocação que fica mais rápida com a velocidade do corpo a corpo. |
| `DamageClass.MagicSummonHybrid` | Mágica e invocação ao mesmo tempo. |
| `DamageClass.Throwing` | Arremesso: nenhum item do jogo usa; existe para os mods combinarem. |

`item.melee = true`, do jeito antigo, continua valendo: é o mesmo que
`DamageClass.Melee`. O projétil tem `proj.DamageType` igual.

## Dar bônus por classe

Num acessório, armadura, buff ou `ModPlayer`, nos ganchos de equipamento
(`UpdateAccessory`, `UpdateEquip`, `PostUpdateEquips`, o `Update` do buff):

```js
UpdateAccessory(item, player, vanity, hideVisual) {
    player.GetDamage(DamageClass.Generic).Additive += 0.10;  // +10% em tudo
    player.GetCritChance(DamageClass.Melee).value += 5;      // +5% de crítico corpo a corpo
    player.GetAttackSpeed(DamageClass.Ranged).value += 0.15; // atira 15% mais rápido
    player.GetArmorPenetration(DamageClass.Magic).value += 5;
    player.GetKnockback(DamageClass.Summon).Additive += 0.5;
}
```

Os bônus valem no quadro em que são postos, como os do jogo: no quadro seguinte
eles começam do zero e o acessório soma de novo.

### O dano é um `StatModifier`

```
dano final = (dano da arma + Base) × Additive × Multiplicative + Flat
```

| Campo | Para quê | Exemplo |
|---|---|---|
| `Additive` | Os "+X%": somam entre si (dois +10% dão +20%, não +21%). | `.Additive += 0.25` |
| `Multiplicative` | Os "×X": multiplicam por cima de tudo. | `.Multiplicative *= 1.12` |
| `Base` | Somado ao dano da arma, antes das porcentagens. | `.Base += 4` |
| `Flat` | Somado no fim, depois de tudo. | `.Flat += 5` |

A repulsão (`GetKnockback`) é um `StatModifier` igual. O crítico, a
velocidade e a penetração são números: `.value`.

No C# do tModLoader é `player.GetDamage(x) += 0.1f`. JavaScript não tem esse
operador para objetos, então aqui se soma no campo: `.Additive += 0.1`.

### Os totais

Para saber quanto o jogador tem numa classe, já com a herança:

| Método | Devolve |
|---|---|
| `player.GetTotalDamage(classe)` | `StatModifier` (use `.ApplyTo(dano)`) |
| `player.GetTotalCritChance(classe)` | número |
| `player.GetTotalAttackSpeed(classe)` | número (1 = normal) |
| `player.GetTotalArmorPenetration(classe)` | número |
| `player.GetTotalKnockback(classe)` | `StatModifier` |
| `player.GetWeaponAttackSpeed(item)`, `player.GetWeaponArmorPenetration(item)` | o da arma |

`player.GetArmorPenetration(true)`, com um booleano, continua sendo o do jogo.

## Criar uma classe nova

```js
export class RogueDamageClass extends DamageClass {
    // De quem ela herda os bônus: tudo do Generic e metade do Throwing.
    GetModifierInheritance(damageClass) {
        if (damageClass === DamageClass.Generic) return StatInheritanceData.Full;
        if (damageClass === DamageClass.Throwing) return new StatInheritanceData({ damageInheritance: 0.5 });
        return StatInheritanceData.None;
    }

    // Como quem ela "conta" para os efeitos do jogo: a Pedra de Magma funciona.
    GetEffectInheritance(damageClass) {
        return damageClass === DamageClass.Melee;
    }

    // Bônus que ela tem sempre, todo quadro.
    SetDefaultStats(player) {
        player.GetCritChance(this).value += 4;
    }
}
```

Ela é registrada sozinha, como qualquer classe exportada. A arma:

```js
import { RogueDamageClass } from '../DamageClasses/RogueDamageClass.js';

SetDefaults() {
    this.Item.DamageType = ModContent.GetInstance(RogueDamageClass);
}
```

E um acessório (deste mod ou de outro) dá bônus a ela:
`player.GetDamage(RogueDamageClass).Additive += 0.2`.

### O que a classe pode decidir

| Método | O que decide | Padrão |
|---|---|---|
| `GetModifierInheritance(outra)` | Quanto herda dos bônus da outra: um `StatInheritanceData` com dano, crítico, velocidade, penetração e repulsão (1 = tudo, 0 = nada). | Tudo do Generic, nada das outras. |
| `GetEffectInheritance(outra)` | `true`: conta como a outra para os efeitos do jogo. | Nenhuma. |
| `GetPrefixInheritance(outra)` | `true`: aceita os prefixos da outra. | As mesmas do efeito. |
| `SetDefaultStats(player)` | Os bônus que ela tem sempre. | Nenhum. |
| `get UseStandardCritCalcs()` | `false`: sem crítico (e sem a linha de crítico no tooltip). | `true` |
| `ShowStatTooltipLine(player, linha)` | `false` esconde a linha `'Damage'`, `'CritChance'`, `'Speed'` ou `'Knockback'`. | Todas aparecem. |

`StatInheritanceData.Full` herda tudo e `StatInheritanceData.None` nada. Para
herdar só parte, use o construtor com os nomes:
`new StatInheritanceData({ damageInheritance: 1, attackSpeedInheritance: 0.5 })`.

`classe.CountsAsClass(DamageClass.Melee)` e `item.CountsAsClass(DamageClass.Melee)`
perguntam se ela conta como corpo a corpo.

### O nome no tooltip

![O tooltip da arma de exemplo: "70 dano de exemplo", o crítico da classe e, escondida pela classe, nenhuma linha de velocidade](../imagens/classe-de-dano-tooltip.jpg)

O tooltip mostra "70 dano de ladino". O texto vem do `Localization/<cultura>.json`
do mod e inclui a palavra "dano":

```json
{
  "DamageClasses": {
    "RogueDamageClass": { "DisplayName": "dano de ladino" }
  }
}
```

Sem ele, sai o nome da classe separado ("Rogue Damage Class"). Um
`DisplayName = 'dano de ladino'` escrito na classe também vale.

## Trocar a classe de um item do jogo

Num `GlobalItem`:

```js
SetDefaults(item) {
    if (item.type === ItemID.WoodenSword) item.DamageType = ModContent.GetInstance(RogueDamageClass);
}
```

A classe escrita no `SetDefaults` vale para o tipo inteiro: o item continua com
ela quando é copiado, cai no chão ou vai pela rede. Mudar o `DamageType` de um
item fora do `SetDefaults`, com o jogo rodando, vale só para aquele item, e se
perde quando o jogo o copia.

## `ModifyWeaponDamage`

No `ModPlayer` e no `GlobalItem`, o dano chega de um jeito que serve aos dois
estilos:

```js
// O de antes: devolver o dano novo.
ModifyWeaponDamage(player, item, damage) { return damage * 2; }

// O do tModLoader: mexer no StatModifier.
ModifyWeaponDamage(player, item, damage) { damage.Additive += 1; }
```

## Como funciona por dentro

O jogo do celular é compilado: não dá para trocar os campos dele como o
tModLoader faz. Então:

- **as classes do jogo** continuam sendo as flags do item e os campos do
  jogador. O que um mod soma em `GetDamage(DamageClass.Melee)` vai para o
  `meleeDamage` a cada quadro, antes de o jogo usar o valor, e todo o código do
  jogo já vê o bônus;
- **as classes de mod** (e `Generic`, `Throwing` e `MagicSummonHybrid` numa
  arma) o jogo não conhece: o dano, o crítico (sorteado de novo), a repulsão,
  a velocidade, a penetração e o dano de lacaio e sentinela delas passam por
  ganchos do Bunny Loader;
- a arma de uma classe de mod liga as flags das classes que ela "conta como"
  ou cujos prefixos aceita: assim os efeitos e os prefixos do jogo funcionam
  sozinhos.

Só paga quem usa: um mod que só escreve `DamageType = DamageClass.Melee` não
instala gancho nenhum.

## Limites

- Código do jogo que lê `meleeDamage` direto (efeitos especiais de algumas
  armaduras) vê os bônus das classes do jogo, mas não os de uma classe de mod.
- O "Generic" que vem do equipamento do próprio jogo é a parte comum aos quatro
  campos de dano: o jogo não guarda um "todo dano" separado.
- A penetração de armadura de uma classe entra no projétil quando ele nasce.

## Exemplo completo

O Example Mod tem a `ExampleDamageClass` (`Content/DamageClasses`), a
`ExampleCustomDamageWeapon` (uma espada com ela) e o
`ExampleStatBonusAccessory` (um acessório que mexe em várias classes).
