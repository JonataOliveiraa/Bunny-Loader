using System;
using System.Linq;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsPaladin;

public sealed class CsPaladinMod : Mod
{
    static int frame, pass, fail, attempts, created, direct;

    static ILog Log => ModContent.GetInstance<CsPaladinMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_NPC_Spawner.SpawnAnNPC += (orig, self, x, y, tileType, wallType, xRange, target) =>
        {
            attempts++;
            orig(self, x, y, tileType, wallType, xRange, target);
        };
    }

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cspaladin {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static int Paladins() => Main.npc.AsSpan().ToArray().Count(n => !n.IsNull && n.active && n.type == NPCID.Paladin);

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        if (!Main.gameMenu && i == Main.myPlayer)
        {
            self.creativeGodMode = true;
            self.statLife = self.statLifeMax2;
        }
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || frame < 0) return;
        frame++;
        try
        {
            if (frame == 30)
            {
                Main.dayTime = false;
                Main.time = 10000;
                direct = NPC.NewNPC(self.GetSource_FromThis(), (int)self.Center.X + 300, (int)self.Center.Y - 40, NPCID.Paladin);
                Log.Info($"cspaladin direct paladin {direct} active={Main.npc[direct].active} type={Main.npc[direct].type} life={Main.npc[direct].life}");
            }
            if (frame == 150)
            {
                NPC n = Main.npc[direct];
                Report("a paladin made with NewNPC stays on the surface", n.active && n.type == NPCID.Paladin, $"active={n.active} type={n.type} life={n.life} pos={n.position}");
                n.active = false;
                foreach (int town in new[] { NPCID.Guide, NPCID.Merchant, NPCID.Nurse, NPCID.Demolitionist })
                {
                    int at = NPC.NewNPC(self.GetSource_FromThis(), (int)self.Center.X - 60, (int)self.Center.Y, town);
                    Main.npc[at].homeless = true;
                }
                ModInvasion crusade = ModContent.Find<ModInvasion>("ExampleMod/PaladinCrusade");
                bool started = InvasionLoader.Start(crusade);
                Log.Info($"cspaladin start={started} active={InvasionLoader.Active?.FullName} wave={InvasionLoader.Wave} canSpawn={crusade.CanSpawn(self)} rate={crusade.SpawnRate} max={crusade.MaxSpawns}");
                attempts = 0;
            }
            if (frame > 150 && frame % 300 == 0)
                Log.Info($"cspaladin t={(frame - 150) / 60}s townNPCs={self.townNPCs} attempts={attempts} paladins={Paladins()} hostile={Main.npc.AsSpan().ToArray().Count(n => !n.IsNull && n.active && !n.friendly && !n.townNPC)} active={InvasionLoader.Active?.FullName}");
            if (frame == 150 + 40 * 60)
            {
                Report("the Paladin's Crusade spawns paladins next to a town", Paladins() > 0, $"attempts={attempts} paladins={Paladins()}");
                frame = -1;
                Log.Info($"cspaladin END pass={pass} fail={fail}");
            }
        }
        catch (Exception e)
        {
            frame = -1;
            Log.Info($"cspaladin END pass=0 fail=1 {e}");
        }
    }
}
