using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons;

public class ExampleSword : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 40;
        Item.height = 40;

        Item.useStyle = ItemUseStyleID.Swing;
        Item.useTime = 20;
        Item.useAnimation = 20;
        Item.autoReuse = true;

        Item.DamageType = DamageClass.Melee;
        Item.damage = 50;
        Item.knockBack = 6;
        Item.crit = 6;

        Item.value = Item.buyPrice(gold: 1);
        Item.rare = ItemRarityID.Orange;
        Item.UseSound = SoundID.Item1;
    }

    public override void MeleeEffects(Player player, Rectangle hitbox)
    {
        if (Main.rand.NextBool(3))
        {
            Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, DustID.GoldFlame);
        }
    }

    public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone)
    {
        target.AddBuff(BuffID.OnFire, 60);
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
