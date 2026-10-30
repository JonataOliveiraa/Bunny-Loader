using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.ResourceSets;
using Terraria.ModLoader;

namespace CsResource;

public class TestOverlay : ModResourceOverlay
{
    public static int Matched;
    public static int Pre, Post, LifeDisplays, ManaDisplays, PostDisplays, MaxNumber = -1, NamedTextures, LifeMax = -1, Hearts = -1;
    public static readonly HashSet<string> Textures = new();

    public override bool PreDrawResource(ResourceOverlayDrawContext context)
    {
        Pre++;
        if (context.resourceNumber > MaxNumber) MaxNumber = context.resourceNumber;
        if (context.texture.Name != "?") NamedTextures++;
        if (Textures.Count < 20) Textures.Add(context.texture.Name);
        if (context.texture == Main.Assets.Request<Texture2D>("Images/UI/PlayerResourceSets/FancyClassic/Heart_Fill")) Matched++;
        return true;
    }

    public override void PostDrawResource(ResourceOverlayDrawContext context)
    {
        Post++;
        if (context.resourceNumber != 0) return;
        context.color = new Color(255, 0, 0, 160);
        context.Draw();
    }

    public override bool PreDrawResourceDisplay(PlayerStatsSnapshot snapshot, IPlayerResourcesDisplaySet displaySet, bool drawingLife, ref Color textColor, out bool drawText)
    {
        if (drawingLife) LifeDisplays++;
        else ManaDisplays++;
        LifeMax = snapshot.LifeMax;
        Hearts = snapshot.AmountOfLifeHearts;
        drawText = !drawingLife;
        return true;
    }

    public override void PostDrawResourceDisplay(PlayerStatsSnapshot snapshot, IPlayerResourcesDisplaySet displaySet, bool drawingLife, Color textColor, bool drawText) => PostDisplays++;
}
