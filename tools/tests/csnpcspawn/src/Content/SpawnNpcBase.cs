using Terraria;
using Terraria.ModLoader;

namespace CsNpcSpawn.Content;

public abstract class SpawnNpcBase : ModNPC
{
    public static int Creates;
    public static bool ContextValid = true;
    public override string Texture => "Content/SpawnNpc";
    public override void SetDefaults()
    {
        NPC.width = NPC.height = 24;
        NPC.lifeMax = 100;
        NPC.damage = NPC.aiStyle = 0;
        NPC.noGravity = NPC.noTileCollide = true;
    }
    public override int SpawnNPC(int x, int y)
    {
        Creates++;
        int index = base.SpawnNPC(x, y);
        if (index < Main.maxNPCs) Main.npc[index].ai[0] = 7;
        return index;
    }
}
