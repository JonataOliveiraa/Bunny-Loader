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
        if (exampleDefenseDebuff) modifiers.Defense *= ExampleDefenseDebuff.DefenseMultiplier;
    }

    public override void DrawEffects(NPC npc, ref Color drawColor)
    {
        if (exampleDefenseDebuff) drawColor.G = 0;
    }
}
