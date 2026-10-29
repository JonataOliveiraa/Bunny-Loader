using Terraria;
using Terraria.ModLoader;

namespace CsUse.Content.Items;

public class TimedItem : ModItem
{
    public static bool? AutoReuse;

    public static int LastConsumed, LastMissing;

    public override void SetDefaults()
    {
        Item.width = 40;
        Item.height = 40;
        Item.useStyle = 5;
        Item.useTime = 10;
        Item.useAnimation = 10;
        Item.mana = 10;
    }

    public override float UseTimeMultiplier(Player player) => 2f;

    public override float UseAnimationMultiplier(Player player) => 3f;

    public override float UseSpeedMultiplier(Player player) => 0.5f;

    public override bool? CanAutoReuseItem(Player player)
    {
        Counts.Add("Timed.CanAutoReuseItem");
        return AutoReuse;
    }

    public override void ModifyManaCost(Player player, ref float reduce, ref float mult) => mult *= 0.5f;

    public override void OnMissingMana(Player player, int neededMana)
    {
        Counts.Add("Timed.OnMissingMana");
        LastMissing = neededMana;
    }

    public override void OnConsumeMana(Player player, int manaConsumed)
    {
        Counts.Add("Timed.OnConsumeMana");
        LastConsumed = manaConsumed;
    }

    public override void UseItemFrame(Player player) => Counts.Add("Timed.UseItemFrame");

    public override void HoldItemFrame(Player player) => Counts.Add("Timed.HoldItemFrame");

    public override bool AltFunctionUse(Player player)
    {
        Counts.Add("Timed.AltFunctionUse");
        return true;
    }
}
