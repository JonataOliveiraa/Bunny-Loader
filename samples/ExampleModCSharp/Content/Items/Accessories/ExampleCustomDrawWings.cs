using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Accessories;

[AutoloadEquip(EquipType.Wings)]
public class ExampleCustomDrawWings : ModItem
{
    private static Asset<Texture2D> glowTexture;

    public override void SetStaticDefaults()
    {
        glowTexture = ModContent.Request<Texture2D>(Texture + "_Wings_Glow");
        ArmorIDs.Wing.Sets.Stats[Item.wingSlot] = new WingStats(180, 9f, 2.5f);
    }

    public override void SetDefaults()
    {
        Item.width = 22;
        Item.height = 20;
        Item.value = 10000;
        Item.rare = ItemRarityID.Green;
        Item.accessory = true;
    }

    public override void VerticalWingSpeeds(Player player, ref float ascentWhenFalling, ref float ascentWhenRising,
        ref float maxCanAscendMultiplier, ref float maxAscentMultiplier, ref float constantAscend)
    {
        ascentWhenFalling = 0.85f;
        ascentWhenRising = 0.15f;
        maxCanAscendMultiplier = 1f;
        maxAscentMultiplier = 3f;
        constantAscend = 0.135f;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile<Tiles.Furniture.ExampleWorkbench>()
            .Register();
    }

    public override bool ModifyEquipTextureDraw(ref PlayerDrawSet drawInfo, ref DrawData drawData, EquipTexture equipTexture, string methodName)
    {
        Vector2 playerBackPosition = drawInfo.Position - Main.screenPosition + new Vector2(drawInfo.drawPlayer.width / 2, drawInfo.drawPlayer.height - drawInfo.drawPlayer.bodyFrame.Height / 2) + new Vector2(0f, 7f);

        int wingFrameCount = 7;
        var texture = drawData.texture;
        Vector2 drawPosition = playerBackPosition + new Vector2(-14, -5) * drawInfo.drawPlayer.Directions;
        drawData = new DrawData(texture, drawPosition.Floor(), new Rectangle(0, texture.Height / wingFrameCount * drawInfo.drawPlayer.wingFrame, texture.Width, texture.Height / wingFrameCount), drawData.color, drawInfo.drawPlayer.bodyRotation, new Vector2(texture.Width / 2, texture.Height / wingFrameCount / 2), 1f, drawInfo.playerEffect);
        drawData.shader = drawInfo.cWings;

        drawInfo.DrawDataCache.Add(drawData);

        for (int i = 0; i < 2; i++)
        {
            DrawData glow = drawData;
            glow.color = Color.White;
            glow.texture = glowTexture.Value;
            glow.position = drawPosition + Main.rand.NextVector2Circular(1.25f, 1.25f);
            drawInfo.DrawDataCache.Add(glow);
        }

        return false;
    }

    public override bool WingUpdate(Player player, bool inUse)
    {
        if (inUse || player.jump > 0)
        {
            player.wingFrameCounter++;
            if (player.wingFrameCounter > 3)
            {
                player.wingFrame++;
                player.wingFrameCounter = 0;
                if (player.wingFrame >= 7)
                {
                    player.wingFrame = 1;
                }
            }
        }
        else if (player.velocity.Y != 0f)
        {
            player.wingFrame = 2;
            if (player.ShouldFloatInWater && player.wet)
            {
                player.wingFrame = 0;
            }
        }
        else
        {
            player.wingFrame = 0;
        }

        if (!inUse && player.wingsLogic > 0 && player.controlJump && player.velocity.Y > 0f)
        {
            player.wingFrame = 1;
        }

        if (inUse)
        {
            if (Main.rand.NextBool(2))
            {
                SpawnWingDust(player);
            }
        }
        else if (player.controlJump && player.velocity.Y > 0f)
        {
            if (Main.rand.NextBool(4))
            {
                SpawnWingDust(player);
            }
        }

        if (player.wingFrame == 4)
        {
            if (!player.flapSound)
            {
                SoundEngine.PlaySound(SoundID.Item32, player.position);
            }

            player.flapSound = true;
        }
        else
        {
            player.flapSound = false;
        }

        return true;
    }

    private void SpawnWingDust(Player player)
    {
        bool noLightEmittance = player.wingsLogic != player.wings;
        int spawnXOffset = 4;
        if (player.direction == 1)
        {
            spawnXOffset = -40;
        }

        Dust dust = Dust.NewDustDirect(new Vector2(player.position.X + (player.width / 2) + spawnXOffset, player.position.Y + (player.height / 2) - 15f), 30, 30, DustID.AncientLight, 0f, 0f, 50, default, 0.6f);
        dust.fadeIn = 1.1f;
        dust.noGravity = true;
        dust.noLight = true;
        dust.noLightEmittance = noLightEmittance;
        dust.velocity *= 0.3f;
        dust.shader = GameShaders.Armor.GetSecondaryShader(player.cWings, player);
    }
}
