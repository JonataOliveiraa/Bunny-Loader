# Achievements

A `ModAchievement` is written as in tModLoader. It joins the game's achievement
list: it has its own icon, name and description, it fills its conditions, and
it is saved with the game's achievements on the device.

```csharp
using Terraria.ModLoader;

public class PartyZombieKilled : ModAchievement
{
    public override void SetStaticDefaults()
    {
        AddNPCKilledCondition(ModContent.NPCType<PartyZombie>());
    }
}
```

The name and description are in the mod's localization, under
`Achievements.<Class>`:

```hjson
Achievements: {
	PartyZombieKilled: {
		FriendlyName: Party Crasher
		Description: Defeat a Party Zombie.
	}
}
```

The icon is `<namespace>/<Class>.png`, or another file named by
`TextureName`. It uses the tModLoader layout: each icon is a 64×64 row with
a 2 px gap, the unlocked frame on the left and the locked frame on the right.
`Index` picks the row, so one sheet can hold several achievements.

## Conditions

| Method | Completes when |
|---|---|
| `AddCondition(key)` | `Complete()` is called on it |
| `AddIntCondition(max)` / `AddFloatCondition(max)` | its `Value` reaches `max` (the menu shows a bar) |
| `AddItemCraftCondition(id or ids)` | the player crafts one of the items |
| `AddItemPickupCondition(id or ids)` | the player picks one up |
| `AddNPCKilledCondition(id or ids)` | the player kills one (the game counts one kill) |
| `AddTileDestroyedCondition(ids)` | the player breaks one of the tiles |
| `AddMany…Condition(ids)` | one condition per id, so all of them are needed |

Each method takes an optional `key` first, which names the condition
(`Achievement.GetCondition(key)`). With more than one condition the menu
shows how many are done. With one number condition it shows that number.
`AutoStaticDefaults` sets this up, and `Achievement.SetCategory(...)`
changes the category (Slayer by default).

`OnCompleted(achievement)` runs once, when the last condition is done.
`Hidden` shows the name and description as `???` until the achievement is
completed.

The conditions are game objects, so they are `struct`s here (see
[values and objects](04-valores-e-objetos.md)). To change a number condition
with `Condition.Value++`, keep it in a field, not in a property:

```csharp
public class ManyGiantWormsKilled : ModAchievement
{
    public CustomIntCondition Condition;

    public override bool Hidden => Condition.Value == 0;

    public override void SetStaticDefaults()
    {
        Achievement.SetCategory(AchievementCategory.Collector);
        Condition = AddIntCondition(5);
        AchievementsHelper.OnNPCKilled += NPCKilledListener;
    }

    public override void Unload() => AchievementsHelper.OnNPCKilled -= NPCKilledListener;

    private void NPCKilledListener(Player player, short npcId)
    {
        if (player.whoAmI == Main.myPlayer && npcId == NPCID.GiantWormHead)
            Condition.Value++;
    }
}
```

`AchievementsHelper.OnNPCKilled`, `OnItemPickup`, `OnItemCraft`,
`OnTileDestroyed` and `OnProgressionEvent` are tModLoader's events. Here they
take `Action` delegates with the same parameters.

## Order

`GetDefaultPosition`, `GetAdvisorPosition` and `GetModdedConstraints` compile,
but the phone's achievement menu has no order to follow. It sorts on its own:
completed achievements first, then the rest. The phone has no achievement
advisor either.

Example: `samples/ExampleModCSharp/Content/Achievements` (`PartyZombieKilled`
and `ManyGiantWormsKilled`, tModLoader's `MinionBossKilled` and
`ManyExampleWormsKilled` aimed at what the Example Mod has). Test pack:
`tools/tests/csachievement`.
