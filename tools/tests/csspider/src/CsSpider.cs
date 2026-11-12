using System;
using System.Linq;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsSpider;

public sealed class CsSpiderMod : Mod
{
    static readonly (int Type, int Wall, int Ground, bool OnWall)[] Cases =
    {
        (NPCID.WallCreeper, NPCID.WallCreeperWall, NPCID.WallCreeper, true),
        (NPCID.BlackRecluse, NPCID.BlackRecluseWall, NPCID.BlackRecluse, true),
        (NPCID.WallCreeperWall, NPCID.WallCreeperWall, NPCID.WallCreeper, false),
        (NPCID.BlackRecluseWall, NPCID.BlackRecluseWall, NPCID.BlackRecluse, false),
    };
    static readonly int[] Spawned = new int[Cases.Length];
    static int frame, shots, pass, fail;

    static ILog Log => ModContent.GetInstance<CsSpiderMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csspider {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

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
            if (frame == 40)
            {
                Main.dayTime = true;
                Main.time = 27000;
                int cx = (int)(self.Center.X / 16), cy = (int)(self.Center.Y / 16);
                for (int x = cx - 40; x <= cx - 4; x++)
                    for (int y = cy - 20; y <= cy + 2; y++)
                        WorldGen.PlaceWall(x, y, WallID.Stone, true);
                for (int k = 0; k < Cases.Length; k++)
                {
                    var c = Cases[k];
                    Vector2 at = self.Center + new Vector2(c.OnWall ? -400 + k * 120 : 250 + (k - 2) * 160, c.Type == c.Wall ? -120 : -10);
                    Spawned[k] = NPC.NewNPC(self.GetSource_FromThis(), (int)at.X, (int)at.Y, c.Type);
                }
            }
            if (frame == 41)
            {
                var sample = typeof(NPC).Assembly.GetType("Terraria.ModLoader.Core.NPCLootHooks")!.GetMethod("TryGetSample", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
                foreach (int t in new[] { NPCID.BlackRecluse, NPCID.BlackRecluseWall, NPCID.WallCreeper, NPCID.WallCreeperWall })
                {
                    object?[] args = { t, null };
                    bool found = (bool)sample.Invoke(null, args)!;
                    NPC n = found ? (NPC)args[1]! : default;
                    Log.Info($"csspider sample {t}: found={found} type={(found ? n.type : 0)} size={(found ? n.width : 0)}x{(found ? n.height : 0)} scale={(found ? n.scale : 0)} active={(found && n.active)}");
                }
                for (int k = 0; k < Cases.Length; k++)
                {
                    NPC npc = Main.npc[Spawned[k]];
                    var c = Cases[k];
                    int target = npc.type == c.Wall ? c.Ground : c.Wall;
                    Vector2 kept = npc.position;
                    bool moved = npc.TryMoveToTransformableSpaceInto(target);
                    npc.position = kept;
                    Log.Info($"csspider live {npc.type}: size={npc.width}x{npc.height} into {target} -> {moved}");
                }
            }
            if (frame > 40 && frame % 60 == 0 && frame <= 400)
                Log.Info("csspider types " + string.Join(" ", Spawned.Select(n => $"{Main.npc[n].type}{(Main.npc[n].active ? "" : "x")}")));
            if (frame == 300) Log.Info($"csspider SHOT {++shots} spiders on and off the wall");
            if (frame == 60)
            {
                for (int k = 0; k < Cases.Length; k++)
                {
                    var c = Cases[k];
                    NPC npc = Main.npc[Spawned[k]];
                    int expected = c.OnWall ? c.Wall : c.Ground;
                    Report($"{c.Type} {(c.OnWall ? "in front of a wall" : "in the open")} becomes {expected}", npc.active && npc.type == expected, $"type={npc.type} size={npc.width}x{npc.height} active={npc.active}");
                }
            }
            if (frame == 420)
            {
                frame = -1;
                Log.Info($"csspider END pass={pass} fail={fail}");
            }
        }
        catch (Exception e)
        {
            frame = -1;
            Log.Info($"csspider END pass=0 fail=1 {e}");
        }
    }
}
