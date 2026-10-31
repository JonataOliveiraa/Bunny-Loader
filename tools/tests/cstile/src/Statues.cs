using System;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsTile;

public static partial class Checks
{
    public static int StatueNpc = -1;
    public static int StatuePre;
    public static int StatuePost;
    public static int StatueTeleports;
    public static bool WireVeto;

    static On_Wiring.hook_HitWireSingle WiringHook(string type, string name)
    {
        Type owner = typeof(ModContent).Assembly.GetType(type, true)!;
        MethodInfo method = owner.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;
        return method.CreateDelegate<On_Wiring.hook_HitWireSingle>();
    }

    static void Statues(Player player, int left, int top)
    {
        var tile = WiringHook("Terraria.ModLoader.Core.TileHooks", "Wire");
        var npc = WiringHook("Terraria.ModLoader.Core.NPCExtraHooks", "HitWireSingle");
        StatueNpc = NPC.NewNPC(player.GetSource_FromThis(), (int)player.Center.X, (int)player.Bottom.Y, NPCID.Merchant);
        if ((uint)StatueNpc >= 200u) throw new InvalidOperationException("No NPC slot for statue checks");
        Main.npc[StatueNpc].townNPC = true;
        try
        {
            for (int order = 0; order < 2; order++)
            {
                On_Wiring.HitWireSingle -= tile;
                On_Wiring.HitWireSingle -= npc;
                if (order == 0)
                {
                    On_Wiring.HitWireSingle += npc;
                    On_Wiring.HitWireSingle += tile;
                }
                else
                {
                    On_Wiring.HitWireSingle += tile;
                    On_Wiring.HitWireSingle += npc;
                }
                for (int style = 40; style <= 41; style++)
                {
                    int x = left + order * 8 + (style - 40) * 3;
                    PlaceStatue(x, top, style);
                    int pre = StatuePre;
                    int post = StatuePost;
                    int teleports = StatueTeleports;
                    Wiring.HitWireSingle(x, top);
                    Check($"statue {style} hook order {order}", StatuePre == pre + 1 && StatuePost == post + 1 && StatueTeleports == teleports + 1,
                        $"pre={StatuePre - pre}, post={StatuePost - post}, teleport={StatueTeleports - teleports}");
                }
                int vetoX = left + order * 8 + 6;
                PlaceStatue(vetoX, top, 40);
                WireVeto = true;
                int before = StatueTeleports;
                int after = StatuePost;
                Wiring.HitWireSingle(vetoX, top);
                Check($"statue veto hook order {order}", StatueTeleports == before && StatuePost == after);
                WireVeto = false;
            }
        }
        finally
        {
            WireVeto = false;
            Main.npc[StatueNpc].active = false;
            StatueNpc = -1;
        }
    }

    static void PlaceStatue(int left, int top, int style)
    {
        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                Tile tile = Main.tile[left + x, top + y];
                tile.active(true);
                tile.type = TileID.Statues;
                tile.frameX = (short)(style * 36 + x * 18);
                tile.frameY = (short)(y * 18);
            }
        }
    }
}
