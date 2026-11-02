using ExampleMod.Common.GlobalNPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Buffs;

public class ExampleDefenseDebuff : ModBuff
{
    public const int DefenseReductionPercent = 25;
    public static float DefenseMultiplier = 1 - DefenseReductionPercent / 100f;

    public override void SetStaticDefaults()
    {
        Main.debuff[Type] = true;
        Main.pvpBuff[Type] = true;
        BuffID.Sets.GrantImmunityWith[Type].Add(BuffID.Ichor);
    }

    public override void Update(NPC npc, ref int buffIndex) => npc.GetGlobalNPC<DamageModificationGlobalNPC>().exampleDefenseDebuff = true;

    public override void Update(Player player, ref int buffIndex) => player.statDefense = (int)(player.statDefense * DefenseMultiplier);
}
