using ExampleMod.Content.Dusts;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Placeable;

public class ExampleTorch : ModItem
{
    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 100;

        ItemID.Sets.ShimmerTransformToItem[Type] = ItemID.ShimmerTorch;
        ItemID.Sets.SingleUseInGamepad[Type] = true;
        ItemID.Sets.Torches[Type] = true;
    }

    public override void SetDefaults()
    {
        Item.DefaultToTorch(ModContent.TileType<Tiles.ExampleTorch>(), 0, false);
        Item.value = 50;
    }

    public override void HoldItem(Player player)
    {
        if (player.wet)
        {
            return;
        }

        if (Main.rand.NextBool(player.itemAnimation > 0 ? 7 : 30))
        {
            Dust dust = Dust.NewDustDirect(new Vector2(player.itemLocation.X + (player.direction == -1 ? -16f : 6f), player.itemLocation.Y - 14f * player.gravDir), 4, 4, ModContent.DustType<Sparkle>(), 0f, 0f, 100);
            if (!Main.rand.NextBool(3))
            {
                dust.noGravity = true;
            }

            dust.velocity *= 0.3f;
            dust.velocity.Y -= 1.5f;
            dust.position = player.RotatedRelativePoint(dust.position);
        }

        Vector2 position = player.RotatedRelativePoint(new Vector2(player.itemLocation.X + 12f * player.direction + player.velocity.X, player.itemLocation.Y - 14f + player.velocity.Y), true);

        Lighting.AddLight(position, 1f, 1f, 1f);
    }

    public override void PostUpdate(WorldItem item)
    {
        if (!item.wet)
        {
            Lighting.AddLight(item.Center, 1f, 1f, 1f);
        }
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile<Tiles.Furniture.ExampleWorkbench>()

            .Register();
    }
}
