using System;
using System.Collections.Generic;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsTownAttack;

public abstract class AttackNpc : ModNPC
{
    public override string Texture => "CsTownAttack/Content/TownNpc";

    public override string HeadTexture => "CsTownAttack/Content/TownNpc_Head";

    protected abstract int AttackKind { get; }

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 25;
        NPCID.Sets.ExtraFramesCount[Type] = 9;
        NPCID.Sets.AttackFrameCount[Type] = 4;
        NPCID.Sets.AttackTime[Type] = 90;
        NPCID.Sets.AttackType[Type] = AttackKind;
        NPCID.Sets.DangerDetectRange[Type] = 700;
    }

    public override void SetDefaults()
    {
        NPC.townNPC = NPC.friendly = true;
        NPC.width = 18;
        NPC.height = 40;
        NPC.lifeMax = 250;
        NPC.defense = 15;
        NPC.aiStyle = 7;
    }
}

[AutoloadHead]
public sealed class GunNpc : AttackNpc
{
    public static int Calls;

    protected override int AttackKind => 1;

    public override void DrawTownAttackGun(ref Texture2D item, ref Rectangle itemFrame, ref float scale, ref int horizontalHoldoutOffset)
    {
        Calls++;
        Main.GetItemDrawFrame(ItemID.SniperRifle, out item, out itemFrame);
        scale = 2f;
        horizontalHoldoutOffset = 8;
    }
}

[AutoloadHead]
public sealed class SwingNpc : AttackNpc
{
    public static int Calls;

    protected override int AttackKind => 3;

    public override void DrawTownAttackSwing(ref Texture2D item, ref Rectangle itemFrame, ref int itemSize, ref float scale, ref Vector2 offset)
    {
        Calls++;
        Main.GetItemDrawFrame(ItemID.TerraBlade, out item, out itemFrame);
        itemSize = 54;
        scale = 2f;
    }
}

public sealed class VanillaDraw : GlobalNPC
{
    public static int Guns, Swings;

    public override void DrawTownAttackGun(NPC npc, ref Texture2D item, ref Rectangle itemFrame, ref float scale, ref int horizontalHoldoutOffset)
    {
        if (npc.type != NPCID.ArmsDealer)
            return;
        Guns++;
        Main.GetItemDrawFrame(ItemID.Megashark, out item, out itemFrame);
        scale = 2f;
    }

    public override void DrawTownAttackSwing(NPC npc, ref Texture2D item, ref Rectangle itemFrame, ref int itemSize, ref float scale, ref Vector2 offset)
    {
        if (npc.type != NPCID.DyeTrader)
            return;
        Swings++;
        Main.GetItemDrawFrame(ItemID.Meowmere, out item, out itemFrame);
        itemSize = 42;
        scale = 2f;
    }
}

public sealed class CsTownAttackMod : Mod
{
    static int frame, pass, fail;
    static readonly List<int> npcs = new();

    static ILog Log => ModContent.GetInstance<CsTownAttackMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cstownattack {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void Add(Player player, int type, int offset)
    {
        int index = NPC.NewNPC(player.GetSource_FromThis(), (int)player.Center.X + offset, (int)player.Bottom.Y, type);
        NPC npc = Main.npc[index];
        npcs.Add(index);
    }

    static void Hold()
    {
        foreach (int index in npcs)
        {
            NPC npc = Main.npc[index];
            int kind = NPCID.Sets.AttackType[npc.type];
            npc.ai[0] = kind == 1 ? 12 : 15;
            npc.ai[1] = 40;
            npc.localAI[3] = 20;
            npc.direction = 1;
            npc.spriteDirection = 1;
        }
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer)
            return;
        frame++;
        try
        {
            if (frame == 60)
            {
                Add(self, ModContent.NPCType<GunNpc>(), -220);
                Add(self, ModContent.NPCType<SwingNpc>(), -90);
                Add(self, NPCID.ArmsDealer, 70);
                Add(self, NPCID.DyeTrader, 300);
                if (ModContent.TryFind("ExampleMod", "ExampleBoneMerchant", out ModNPC bone))
                    Add(self, bone.Type, -340);
                if (ModContent.TryFind("ExampleMod", "ExampleTravelingMerchant", out ModNPC traveling))
                    Add(self, traveling.Type, 385);
            }
            if (frame > 60)
                Hold();
            if (frame == 120)
                Log.Info("cstownattack SHOT attack");
            if (frame == 540)
            {
                Report("the mod NPC's gun hook runs and replaces the gun", GunNpc.Calls > 0, $"calls={GunNpc.Calls}");
                Report("the mod NPC's swing hook runs and replaces the item", SwingNpc.Calls > 0, $"calls={SwingNpc.Calls}");
                Report("GlobalNPC gun hook runs on a vanilla NPC", VanillaDraw.Guns > 0, $"calls={VanillaDraw.Guns}");
                Report("GlobalNPC swing hook runs on a vanilla NPC", VanillaDraw.Swings > 0, $"calls={VanillaDraw.Swings}");
                foreach (int index in npcs)
                    Main.npc[index].active = false;
                Log.Info($"cstownattack END pass={pass} fail={fail}");
            }
        }
        catch (Exception e)
        {
            Report("frame " + frame, false, e.ToString());
        }
    }
}
