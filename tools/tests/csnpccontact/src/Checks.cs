using System;
using System.Collections.Generic;
using CsNpcContact.Content;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsNpcContact;

public static class Checks
{
    static readonly List<string> Order = new(), NpcOrder = new();
    static int pass, fail, frames, contact = -1, victim = -1, zombie = -1, hitsBefore, npcHitsBefore, victimLife;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsNpcContactMod>().Logger;

    public static void Mark(string step)
    {
        if (Order.Count < 12) Order.Add(step);
    }

    public static void MarkNpc(string step)
    {
        if (NpcOrder.Count < 12) NpcOrder.Add(step);
    }

    public static void Reset()
    {
        pass = fail = frames = 0;
        contact = victim = zombie = -1;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done) return;
        frames++;
        try
        {
            if (frames == 60) Start(p);
            if (frames > 60 && frames < 400) Hold(p);
            if (frames == 120) CheckPlayer(p);
            if (frames == 240) CheckBlocked(p);
            if (frames == 260) StartNpc(p);
            if (frames == 320) CheckNpc();
            if (frames == 390) CheckNpcBlocked();
        }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done)
        {
            foreach (int index in new[] { contact, victim, zombie })
                if ((uint)index < 200u) Main.npc[index].active = false;
            Log.Info($"csnpccontact END pass={pass} fail={fail}");
        }
    }

    static void Start(Player p)
    {
        Order.Clear();
        contact = NPC.NewNPC(p.GetSource_FromThis(), (int)p.Center.X, (int)p.Center.Y + 12, ModContent.NPCType<ContactNpc>());
    }

    static void Hold(Player p)
    {
        p.statLife = p.statLifeMax2;
        if ((uint)contact < 200u && frames < 240) Main.npc[contact].Center = p.Center;
        if ((uint)zombie < 200u && (uint)victim < 200u)
        {
            NPC z = Main.npc[zombie];
            z.Center = Main.npc[victim].Center;
            z.velocity = Vector2.Zero;
        }
    }

    static void CheckPlayer(Player p)
    {
        string order = string.Join(",", Order.GetRange(0, Math.Min(Order.Count, 7)));
        Report("NPC contact runs NPC, global and player hooks in tModLoader's order", order == "CanG,CanN,CanP,ModifyN,ModifyG,ModifyP,OnN" && ContactNpc.Hits > 0,
            $"order={string.Join(",", Order)} hits={ContactNpc.Hits}");
        ContactNpc.Allow = false;
        hitsBefore = ContactNpc.Hits;
        p.immuneTime = 0;
        p.immune = false;
    }

    static void CheckBlocked(Player p)
    {
        Report("CanHitPlayer false stops the contact", ContactNpc.Hits == hitsBefore, $"hits={ContactNpc.Hits - hitsBefore}");
        ContactNpc.Allow = true;
        if ((uint)contact < 200u) Main.npc[contact].active = false;
        contact = -1;
    }

    static void StartNpc(Player p)
    {
        NpcOrder.Clear();
        victim = NPC.NewNPC(p.GetSource_FromThis(), (int)p.Center.X + 300, (int)p.Center.Y - 60, ModContent.NPCType<VictimNpc>());
        zombie = NPC.NewNPC(p.GetSource_FromThis(), (int)p.Center.X + 300, (int)p.Center.Y - 60, NPCID.Zombie);
        NPC z = Main.npc[zombie];
        z.aiStyle = 0;
        z.noGravity = true;
        z.noTileCollide = true;
        victimLife = Main.npc[victim].life;
    }

    static void CheckNpc()
    {
        NPC v = Main.npc[victim];
        string order = string.Join(",", NpcOrder.GetRange(0, Math.Min(NpcOrder.Count, 5)));
        Report("a hostile NPC hitting a friendly one runs CanHitNPC, CanBeHitByNPC, ModifyIncomingHit, ModifyHitNPC and OnHitNPC",
            order == "CanHitG,CanBeHitV,IncomingV,ModifyHitG,OnHitG" && v.life < victimLife,
            $"order={string.Join(",", NpcOrder)} life={v.life}/{victimLife}");
        VictimNpc.Allow = false;
        npcHitsBefore = ContactGlobal.Hits;
        victimLife = v.life;
        v.immune[255] = 0;
    }

    static void CheckNpcBlocked()
    {
        NPC v = Main.npc[victim];
        Report("CanBeHitByNPC false protects the victim", ContactGlobal.Hits == npcHitsBefore && v.life == victimLife,
            $"hits={ContactGlobal.Hits - npcHitsBefore} life={v.life}/{victimLife}");
        VictimNpc.Allow = true;
        done = true;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csnpccontact {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
