namespace Terraria.ModLoader.Core;

internal static unsafe class NPCDrawOffsetHooks
{
    static readonly object Lock = new();
    static byte* types;

    internal static void Want(int type)
    {
        if ((uint)type >= NPCGates.TypeBits || NPCGates.Marked(types, NPCGates.TypeBits, type)) return;
        lock (Lock)
        {
            if (types != null)
            {
                NPCGates.Mark(types, NPCGates.TypeBits, type);
                return;
            }
            byte* map = NPCGates.Bitmap<On_Main.hook_NPCAddHeight>(0, "type", NPCGates.TypeBits);
            NPCGates.Mark(map, NPCGates.TypeBits, type);
            types = map;
            On_Main.NPCAddHeight += AddHeight;
        }
    }

    static float AddHeight(On_Main.orig_NPCAddHeight orig, NPC npc)
    {
        float height = orig(npc);
        ModNPC? own = NPCLoader.InstanceOf(npc);
        return own == null ? height : height + own.DrawOffsetY;
    }
}
