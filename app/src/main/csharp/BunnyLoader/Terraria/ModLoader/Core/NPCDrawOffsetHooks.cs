namespace Terraria.ModLoader.Core;

internal static class NPCDrawOffsetHooks
{
    internal static void Install() => ItemHooks.Once("NPCDrawOffset", () => On_Main.NPCAddHeight += AddHeight);

    static float AddHeight(On_Main.orig_NPCAddHeight orig, NPC npc)
    {
        float height = orig(npc);
        ModNPC? own = NPCLoader.InstanceOf(npc);
        return own == null ? height : height + own.DrawOffsetY;
    }
}
