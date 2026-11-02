using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Accessories;

public class ExampleExtraJumpAccessory : ModItem
{
    public override void SetDefaults()
    {
        Item.DefaultToAccessory(20, 26);
        Item.SetShopValues(ItemRarityColor.Green2, Item.buyPrice(silver: 50));
    }

    public override void UpdateAccessory(Player player, bool hideVisual)
    {
        player.GetJumpState<SimpleExtraJump>().Enable();
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>(20)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}

public class SimpleExtraJump : ExtraJump
{
    public override Position GetDefaultPosition() => new After(BlizzardInABottle);

    public override float GetDurationMultiplier(Player player)
    {
        return 2.25f;
    }

    public override void UpdateHorizontalSpeeds(Player player)
    {
        player.runAcceleration *= 1.75f;
        player.maxRunSpeed *= 2f;
    }

    public override void OnStarted(Player player, ref bool playSound)
    {
        int offsetY = player.height;
        if (player.gravDir == -1f)
            offsetY = 0;

        offsetY -= 16;

        for (int i = 0; i < 10; i++)
        {
            Dust dust = Dust.NewDustDirect(player.position + new Vector2(-34f, offsetY), 102, 32, DustID.Cloud, -player.velocity.X * 0.5f, player.velocity.Y * 0.5f, 100, Color.Gray, 1.5f);
            dust.velocity = dust.velocity * 0.5f - player.velocity * new Vector2(0.1f, 0.3f);
        }

        SpawnCloudPoof(player, player.Top + new Vector2(-16f, offsetY));
        SpawnCloudPoof(player, player.position + new Vector2(-36f, offsetY));
        SpawnCloudPoof(player, player.TopRight + new Vector2(4f, offsetY));
    }

    private static void SpawnCloudPoof(Player player, Vector2 position)
    {
        Gore gore = Gore.NewGoreDirect(player.GetSource_FromThis(), position, -player.velocity, Main.rand.Next(11, 14));
        gore.velocity.X = gore.velocity.X * 0.1f - player.velocity.X * 0.1f;
        gore.velocity.Y = gore.velocity.Y * 0.1f - player.velocity.Y * 0.05f;
    }

    public override void ShowVisuals(Player player)
    {
        int offsetY = player.height - 6;
        if (player.gravDir == -1f)
            offsetY = 6;

        Vector2 spawnPos = new Vector2(player.position.X, player.position.Y + offsetY);

        for (int i = 0; i < 2; i++)
        {
            SpawnBlizzardDust(player, spawnPos, 0.1f, i == 0 ? -0.07f : -0.13f);
        }

        for (int i = 0; i < 3; i++)
        {
            SpawnBlizzardDust(player, spawnPos, 0.6f, 0.8f);
        }

        for (int i = 0; i < 3; i++)
        {
            SpawnBlizzardDust(player, spawnPos, 0.6f, -0.8f);
        }
    }

    private static void SpawnBlizzardDust(Player player, Vector2 spawnPos, float dustVelocityMultiplier, float playerVelocityMultiplier)
    {
        Dust dust = Dust.NewDustDirect(spawnPos, player.width, 12, DustID.Snow, player.velocity.X * 0.3f, player.velocity.Y * 0.3f, newColor: Color.Gray);
        dust.fadeIn = 1.5f;
        dust.velocity *= dustVelocityMultiplier;
        dust.velocity += player.velocity * playerVelocityMultiplier;
        dust.noGravity = true;
        dust.noLight = true;
    }
}
