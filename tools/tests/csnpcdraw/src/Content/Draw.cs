using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CsNpcDraw.Content;

public sealed class DrawNpc : ModNPC
{
    public static readonly Color Effect = new(255, 10, 20);
    public static readonly List<string> Order = new();
    public static readonly List<Color> Alphas = new();
    public static bool Recording, Skip;
    public static int Behind = -1, BehindCalls, PostCalls;

    public override void SetDefaults()
    {
        NPC.width = NPC.height = 24;
        NPC.lifeMax = 100000;
        NPC.damage = 0;
        NPC.aiStyle = 0;
        NPC.friendly = true;
        NPC.townNPC = true;
        NPC.noGravity = NPC.noTileCollide = true;
    }

    public override void DrawEffects(ref Color drawColor)
    {
        if (Recording) Order.Add("EffectsN");
        drawColor = Effect;
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (Recording) Order.Add(drawColor.Equals(Effect) ? "PreN" : "PreN?");
        return !Skip;
    }

    public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        PostCalls++;
        if (Recording) Order.Add("PostN");
    }

    public override Color? GetAlpha(Color drawColor)
    {
        if (Recording)
        {
            Alphas.Add(drawColor);
            if (!Order.Contains("AlphaN")) Order.Add("AlphaN");
        }
        return null;
    }

    public override void DrawBehind(int index)
    {
        BehindCalls++;
        Behind = index;
    }

    public override void BossHeadSlot(ref int index) => index = CsNpcDrawMod.ExtraHead;

    public override void BossHeadRotation(ref float rotation) => rotation = 1.5f;

    public override void BossHeadSpriteEffects(ref SpriteEffects spriteEffects) => spriteEffects = SpriteEffects.FlipHorizontally;

    public override bool CheckDead() => false;
}

public sealed class HeadNpc : ModNPC
{
    public override void SetDefaults()
    {
        NPC.width = NPC.height = 24;
        NPC.lifeMax = 100;
        NPC.aiStyle = 0;
        NPC.friendly = true;
        NPC.noGravity = NPC.noTileCollide = true;
    }
}

public sealed class DrawGlobal : GlobalNPC
{
    public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == ModContent.NPCType<DrawNpc>();

    public override void DrawEffects(NPC npc, ref Color drawColor)
    {
        if (DrawNpc.Recording) DrawNpc.Order.Add(drawColor.Equals(DrawNpc.Effect) ? "EffectsG" : "EffectsG?");
    }

    public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (DrawNpc.Recording) DrawNpc.Order.Add("PreG");
        return true;
    }

    public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (DrawNpc.Recording) DrawNpc.Order.Add("PostG");
    }

    public override Color? GetAlpha(NPC npc, Color drawColor)
    {
        if (DrawNpc.Recording && !DrawNpc.Order.Contains("AlphaG")) DrawNpc.Order.Add("AlphaG");
        return null;
    }
}

public sealed class NetNpc : ModNPC
{
    public int Number;
    public string Text = "";
    public bool Dirty;

    public override void SetDefaults()
    {
        NPC.width = NPC.height = 24;
        NPC.lifeMax = 100000;
        NPC.aiStyle = 0;
        NPC.friendly = true;
        NPC.noGravity = NPC.noTileCollide = true;
    }

    public override void AI()
    {
        NPC.velocity = Vector2.Zero;
        if (!Dirty) return;
        Dirty = false;
        NPC.netUpdate = true;
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(Number);
        writer.Write(Text);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        Number = reader.ReadInt32();
        Text = reader.ReadString();
    }

    public override bool CheckDead() => false;

    public override bool CheckActive() => false;
}

public sealed class NetBits : GlobalNPC
{
    public bool First, Second;
    public float Value;

    public override bool InstancePerEntity => true;

    public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == ModContent.NPCType<NetNpc>();

    public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
    {
        bitWriter.WriteBit(First);
        bitWriter.WriteBit(Second);
        binaryWriter.Write(Value);
    }

    public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
    {
        First = bitReader.ReadBit();
        Second = bitReader.ReadBit();
        Value = binaryReader.ReadSingle();
    }
}
