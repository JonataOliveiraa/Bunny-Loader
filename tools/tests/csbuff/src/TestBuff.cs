using Terraria;
using Terraria.ModLoader;

namespace CsBuff;

public class TestBuff : ModBuff
{
    public static bool StaticRan, BlockReApply, Keep;
    public static int PlayerUpdates, NpcUpdates;

    public override void SetStaticDefaults()
    {
        StaticRan = true;
        Main.buffNoSave[Type] = true;
    }

    public override void Update(Player player, ref int buffIndex)
    {
        PlayerUpdates++;
        player.statDefense += 7;
    }

    public override void Update(NPC npc, ref int buffIndex) => NpcUpdates++;

    public override bool ReApply(Player player, int time, int buffIndex) => BlockReApply;

    public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
    {
        buffName += " (csbuff)";
        tip += " +7";
    }

    public override bool RightClick(int buffIndex) => !Keep;
}
