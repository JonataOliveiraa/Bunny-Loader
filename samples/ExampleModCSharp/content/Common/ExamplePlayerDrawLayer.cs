using ExampleMod.Content.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace ExampleMod.Common;

public class ExamplePlayerDrawLayer : PlayerDrawLayer
{
    public override bool IsHeadLayer => true;

    public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
    {
        return drawInfo.drawPlayer.HeldItem.type == ModContent.ItemType<ExampleItem>();
    }

    public override Position GetDefaultPosition() => new BeforeParent(PlayerDrawLayers.Head);

    protected override void Draw(ref PlayerDrawSet drawInfo)
    {
        var exampleItemTexture = TextureAssets.Item[ModContent.ItemType<ExampleItem>()];

        var position = drawInfo.Center + new Vector2(0f, -20f) - Main.screenPosition;
        position = new Vector2((int)position.X, (int)position.Y);

        drawInfo.DrawDataCache.Add(new DrawData(
            exampleItemTexture.Value,
            position,
            null,
            Color.White,
            0f,
            exampleItemTexture.Size() * 0.5f,
            1f,
            SpriteEffects.None,
            0
        ));
    }
}
