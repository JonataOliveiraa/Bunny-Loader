using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsEmote.Content;

public sealed class TestEmote : ModEmoteBubble
{
    public static int Spawns, Updates, Pres, Posts, MenuFrames;
    public static readonly HashSet<int> Frames = new();
    public static readonly HashSet<IntPtr> Bound = new();

    public IntPtr SpawnedOn;

    public override void SetStaticDefaults() => AddToCategory(EmoteID.Category.General);

    public override void OnSpawn()
    {
        Spawns++;
        SpawnedOn = EmoteBubble.Pointer;
        Bound.Add(EmoteBubble.Pointer);
    }

    public override bool UpdateFrame()
    {
        Updates++;
        return true;
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Texture2D texture, Vector2 position, Rectangle frame, Vector2 origin, SpriteEffects spriteEffects)
    {
        Pres++;
        Frames.Add(frame.X);
        return true;
    }

    public override void PostDraw(SpriteBatch spriteBatch, Texture2D texture, Vector2 position, Rectangle frame, Vector2 origin, SpriteEffects spriteEffects) => Posts++;

    public override Rectangle? GetFrameInEmoteMenu(int frame, int frameCounter)
    {
        MenuFrames++;
        return null;
    }
}

public sealed class LockedEmote : ModEmoteBubble
{
    public override void SetStaticDefaults() => AddToCategory(EmoteID.Category.General);

    public override bool IsUnlocked() => false;
}

public sealed class TestGlobalEmote : GlobalEmoteBubble
{
    public static int VanillaSpawns, VanillaDraws;

    public override void OnSpawn(EmoteBubble emoteBubble)
    {
        if (emoteBubble.emote == EmoteID.EmotionLove) VanillaSpawns++;
    }

    public override bool PreDraw(EmoteBubble emoteBubble, SpriteBatch spriteBatch, Texture2D texture, Vector2 position, Rectangle frame, Vector2 origin,
                                 SpriteEffects spriteEffects)
    {
        if (emoteBubble.emote == EmoteID.EmotionLove) VanillaDraws++;
        return true;
    }
}

public sealed class TalkerNpc : ModNPC
{
    public static string FaceEmoteError = "";

    public override void SetStaticDefaults()
    {
        try { NPCID.Sets.FaceEmote[Type] = ModContent.EmoteBubbleType<TestEmote>(); }
        catch (Exception e) { FaceEmoteError = e.GetType().Name; }
    }

    public override void SetDefaults()
    {
        NPC.width = NPC.height = 24;
        NPC.lifeMax = 100;
        NPC.aiStyle = 0;
        NPC.friendly = true;
        NPC.townNPC = true;
        NPC.noGravity = NPC.noTileCollide = true;
    }

    public override int? PickEmote(Player closestPlayer, List<int> emoteList, WorldUIAnchor otherAnchor) => ModContent.EmoteBubbleType<TestEmote>();
}
