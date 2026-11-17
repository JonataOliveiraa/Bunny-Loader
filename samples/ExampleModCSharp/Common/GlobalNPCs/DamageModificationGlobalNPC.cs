using ExampleMod.Content.Buffs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Common.GlobalNPCs;

internal class DamageModificationGlobalNPC : GlobalNPC
{
    public override bool InstancePerEntity => true;
    public bool exampleDefenseDebuff;

    public override void ResetEffects(NPC npc) => exampleDefenseDebuff = false;

    public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
    {
        // For best results, defense debuffs should be multiplicative
        if (exampleDefenseDebuff) modifiers.Defense *= ExampleDefenseDebuff.DefenseMultiplier;
    }

    public override void DrawEffects(NPC npc, ref Color drawColor)
    {
        // This simple color effect indicates that the buff is active
        if (exampleDefenseDebuff) drawColor.G = 0;
    }
}
