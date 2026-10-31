using System;
using CsNpcDraw.Content;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsNpcDraw;

public static class Checks
{
    const int ServerNumber = 4242;
    const string ServerText = "from server";

    static int pass, fail, frames, draw = -1, head = -1, net = -1;
    static bool done, reported;

    static ILog Log => ModContent.GetInstance<CsNpcDrawMod>().Logger;

    public static void Reset()
    {
        pass = fail = frames = 0;
        draw = head = net = -1;
        done = reported = false;
        DrawNpc.Recording = DrawNpc.Skip = false;
    }

    public static void Frame(Player p)
    {
        if (done) return;
        frames++;
        try
        {
            if (Main.netMode == 0) Local(p);
            else Network(p, Main.netMode == 1);
        }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done)
        {
            DrawNpc.Recording = DrawNpc.Skip = false;
            foreach (int index in new[] { draw, head, net })
                if ((uint)index < 200u) Main.npc[index].active = false;
            Log.Info($"csnpcdraw END pass={pass} fail={fail}");
        }
    }

    static void Local(Player p)
    {
        if (frames == 60)
        {
            draw = NPC.NewNPC(p.GetSource_FromThis(), (int)p.Center.X, (int)p.Center.Y - 60, ModContent.NPCType<DrawNpc>());
            head = NPC.NewNPC(p.GetSource_FromThis(), (int)p.Center.X + 60, (int)p.Center.Y - 60, ModContent.NPCType<HeadNpc>());
        }
        if (frames == 90) Record();
        if (frames == 94) CheckOrder();
        if (frames == 100)
        {
            DrawNpc.Skip = true;
            Record();
        }
        if (frames == 104) CheckSkip();
        if (frames == 110) CheckBehind();
        if (frames == 112) CheckHeads();
        if (frames == 114)
        {
            CheckExtraAI(p);
            done = true;
        }
    }

    static void Record()
    {
        DrawNpc.Order.Clear();
        DrawNpc.Alphas.Clear();
        DrawNpc.PostCalls = 0;
        DrawNpc.Recording = true;
    }

    static void CheckOrder()
    {
        DrawNpc.Recording = false;
        string order = string.Join(",", DrawNpc.Order.GetRange(0, Math.Min(8, DrawNpc.Order.Count)));
        Report("drawing runs DrawEffects, PreDraw, vanilla and PostDraw in tModLoader's order",
            order == "EffectsN,EffectsG,PreG,PreN,AlphaG,AlphaN,PostN,PostG", $"order={string.Join(",", DrawNpc.Order)}");
        Report("the DrawEffects color reaches the vanilla draw", DrawNpc.Alphas.Contains(DrawNpc.Effect),
            $"alphas={string.Join(";", DrawNpc.Alphas)}");
    }

    static void CheckSkip()
    {
        DrawNpc.Recording = false;
        DrawNpc.Skip = false;
        Report("PreDraw false skips the vanilla draw but keeps PostDraw",
            DrawNpc.Order.Contains("PreN") && !DrawNpc.Order.Contains("AlphaN") && DrawNpc.PostCalls > 0,
            $"order={string.Join(",", DrawNpc.Order)} post={DrawNpc.PostCalls}");
    }

    static void CheckBehind()
    {
        Report("DrawBehind runs with the NPC index", DrawNpc.BehindCalls > 0 && DrawNpc.Behind == draw,
            $"calls={DrawNpc.BehindCalls} index={DrawNpc.Behind} expected={draw}");
    }

    static void CheckHeads()
    {
        int slot = ModContent.GetModBossHeadSlot("CsNpcDraw/Content/HeadNpc_Head_Boss");
        int headIndex = Main.npc[head].GetBossHeadTextureIndex();
        Report("BossHeadTexture registers the _Head_Boss icon for its NPC", slot >= 0 && headIndex == slot, $"slot={slot} index={headIndex}");
        NPC n = Main.npc[draw];
        int drawIndex = n.GetBossHeadTextureIndex();
        float rotation = n.GetBossHeadRotation();
        SpriteEffects effects = n.GetBossHeadSpriteEffects();
        Report("BossHeadSlot, BossHeadRotation and BossHeadSpriteEffects change the icon",
            CsNpcDrawMod.ExtraHead >= 0 && CsNpcDrawMod.ExtraHead != slot && drawIndex == CsNpcDrawMod.ExtraHead && rotation == 1.5f && effects == SpriteEffects.FlipHorizontally,
            $"extra={CsNpcDrawMod.ExtraHead} index={drawIndex} rotation={rotation} effects={effects}");
    }

    static NetNpc Spawn(Player p, Vector2 offset, int number, string text, bool first, bool second, float value)
    {
        int index = NPC.NewNPC(p.GetSource_FromThis(), (int)(p.Center.X + offset.X), (int)(p.Center.Y + offset.Y), ModContent.NPCType<NetNpc>());
        NPC npc = Main.npc[index];
        var data = (NetNpc)npc.ModNPC;
        data.Number = number;
        data.Text = text;
        NetBits bits = npc.GetGlobalNPC<NetBits>();
        bits.First = first;
        bits.Second = second;
        bits.Value = value;
        data.Dirty = true;
        return data;
    }

    static bool Matches(NPC npc, int number, string text, bool first, bool second, float value)
    {
        var data = (NetNpc)npc.ModNPC;
        NetBits bits = npc.GetGlobalNPC<NetBits>();
        return data.Number == number && data.Text == text && bits.First == first && bits.Second == second && bits.Value == value;
    }

    static string Describe(NPC npc)
    {
        var data = (NetNpc)npc.ModNPC;
        NetBits bits = npc.GetGlobalNPC<NetBits>();
        return $"number={data.Number} text='{data.Text}' bits={bits.First},{bits.Second} value={bits.Value}";
    }

    static void CheckExtraAI(Player p)
    {
        NPC source = Spawn(p, new Vector2(-60f, -120f), 42, "hello", true, false, 3.5f).NPC;
        byte[]? bytes = NPCLoader.WriteExtraAI(source);
        Report("WriteExtraAI lays out the global bits first", bytes != null && bytes[0] == 2 && bytes[1] == 1 && bytes.Length == 2 + 4 + 6 + 4,
            bytes == null ? "null" : BitConverter.ToString(bytes));
        NPC target = Spawn(p, new Vector2(60f, -120f), 0, "", false, true, 0f).NPC;
        NPCLoader.ReceiveExtraAI(target, bytes!);
        Report("ReceiveExtraAI restores the ModNPC and its global", Matches(target, 42, "hello", true, false, 3.5f), Describe(target));
        int bunny = NPC.NewNPC(p.GetSource_FromThis(), (int)p.Center.X, (int)p.Center.Y - 160, NPCID.Bunny);
        Report("an NPC without extra AI writes nothing", NPCLoader.WriteExtraAI(Main.npc[bunny]) == null, $"index={bunny}");
        Main.npc[bunny].active = false;
        source.active = false;
        target.active = false;
    }

    static void Network(Player p, bool client)
    {
        if (!client)
        {
            if (frames == 120)
            {
                net = Spawn(p, new Vector2(-80f, -100f), ServerNumber, ServerText, true, true, 8.5f).NPC.whoAmI;
                Log.Info($"csnpcdraw spawned server NPC {Describe(Main.npc[net])}");
            }
            if ((uint)net < 200u && frames % 120 == 0 && Main.npc[net].ModNPC is NetNpc dirty) dirty.Dirty = true;
            if (frames == 3600)
            {
                Report("the server keeps its NPC", (uint)net < 200u && Main.npc[net].active, $"index={net}");
                done = true;
            }
            return;
        }
        if (!reported && frames >= 300 && frames % 60 == 0)
        {
            NPC? seen = null;
            for (int i = 0; i < 200; i++)
                if (Main.npc[i].active && Main.npc[i].ModNPC is NetNpc) seen = Main.npc[i];
            bool ok = seen is NPC found && Matches(found, ServerNumber, ServerText, true, true, 8.5f);
            if (ok || frames >= 3000)
            {
                Report("the client receives the server's NPC extra AI", ok, seen is NPC any ? Describe(any) : "no NPC from the server");
                reported = true;
            }
        }
        if (reported && frames >= 3600) done = true;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csnpcdraw {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
