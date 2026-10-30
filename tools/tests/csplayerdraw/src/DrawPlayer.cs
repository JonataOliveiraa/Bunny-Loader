using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics;
using Terraria.ModLoader;

namespace CsPlayerDraw;

public static class Counts
{
    public static readonly Dictionary<string, int> Calls = new();

    public static void Add(string name) => Calls[name] = Calls.GetValueOrDefault(name) + 1;

    public static int Of(string name) => Calls.GetValueOrDefault(name);
}

public class BetweenLayer : PlayerDrawLayer
{
    public static int CacheBefore = -1, CacheAfter = -1, TextureWidth = -1;

    public override Position GetDefaultPosition() => new Between(PlayerDrawLayers.Torso, PlayerDrawLayers.OffhandAcc);

    protected override void Draw(ref PlayerDrawSet drawInfo)
    {
        Counts.Add(nameof(BetweenLayer));
        CacheBefore = drawInfo.DrawDataCache.Count;
        Texture2D texture = Terraria.GameContent.TextureAssets.Item[23].Value;
        TextureWidth = texture.Width;
        drawInfo.DrawDataCache.Add(new DrawData(texture, drawInfo.Position - Main.screenPosition, null, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None));
        CacheAfter = drawInfo.DrawDataCache.Count;
    }
}

public class ChildLayer : PlayerDrawLayer
{
    public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.Head);

    protected override void Draw(ref PlayerDrawSet drawInfo) => Counts.Add(nameof(ChildLayer));
}

public class HiddenLayer : PlayerDrawLayer
{
    public override Position GetDefaultPosition() => new BeforeParent(PlayerDrawLayers.Skin);

    protected override void Draw(ref PlayerDrawSet drawInfo) => Counts.Add(nameof(HiddenLayer));
}

public class MovedLayer : PlayerDrawLayer
{
    public override Position GetDefaultPosition() => new Between(PlayerDrawLayers.Torso, PlayerDrawLayers.OffhandAcc);

    protected override void Draw(ref PlayerDrawSet drawInfo) => Counts.Add(nameof(MovedLayer));
}

public class DrawingPlayer : ModPlayer
{
    public static int ShirtRed = -1, PantsRed = -1, TransformCache = -1;
    public static bool CameraSeen;
    public static float ZoomFactor = 1f;

    public override void ModifyDrawLayerOrdering(IDictionary<PlayerDrawLayer, PlayerDrawLayer.Position> positions)
    {
        Counts.Add(nameof(ModifyDrawLayerOrdering));
        positions[ModContent.GetInstance<MovedLayer>()] = new PlayerDrawLayer.Between(PlayerDrawLayers.Wings, PlayerDrawLayers.HairBack);
    }

    public override void HideDrawLayers(PlayerDrawSet drawInfo)
    {
        Counts.Add(nameof(HideDrawLayers));
        ModContent.GetInstance<HiddenLayer>().Hide();
    }

    public override void DrawEffects(PlayerDrawSet drawInfo, ref float r, ref float g, ref float b, ref float a, ref bool fullBright)
    {
        Counts.Add(nameof(DrawEffects));
        r = 0f;
    }

    public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
    {
        Counts.Add(nameof(ModifyDrawInfo));
        ShirtRed = drawInfo.colorShirt.R;
        PantsRed = drawInfo.colorPants.R;
    }

    public override void TransformDrawData(ref PlayerDrawSet drawInfo)
    {
        Counts.Add(nameof(TransformDrawData));
        TransformCache = drawInfo.DrawDataCache.Count;
    }

    public override void DrawPlayer(Camera camera)
    {
        Counts.Add(nameof(DrawPlayer));
        CameraSeen |= !camera.IsNull;
    }

    public override void ModifyScreenPosition() => Counts.Add(nameof(ModifyScreenPosition));

    public override void ModifyZoom(ref float zoom)
    {
        Counts.Add(nameof(ModifyZoom));
        zoom *= ZoomFactor;
    }
}
