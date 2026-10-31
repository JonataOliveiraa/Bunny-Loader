using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsNpcHit.Content;

public sealed class AGlobalNpc : GlobalNPC
{
    public override bool InstancePerEntity => true;
    public bool? Decision, Collision;
    public int CanCalls, CollisionCalls, Hits;
    public override bool AppliesToEntity(NPC npc, bool late) => !late && (npc.type == NPCID.BlueSlime || NPCLoader.GetNPC(npc.type) is ProbeNpc);
    public override bool? CanBeHitByItem(NPC npc, Player player, Item item) { CanCalls++; Checks.Mark("NCanA"); return Decision; }
    public override bool? CanCollideWithPlayerMeleeAttack(NPC npc, Player player, Item item, Rectangle meleeAttackHitbox)
    {
        CollisionCalls++;
        Checks.Mark("NCollideA");
        return Collision;
    }
    public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
    {
        Checks.Mark("IncomingA");
        if (Checks.Arithmetic) modifiers.Defense.Flat -= 2;
        if (npc.ModNPC == null) modifiers.DisableCrit();
    }
    public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers)
    {
        Checks.Mark("ModifyA");
        if (Checks.Arithmetic) modifiers.FinalDamage.Flat += 1;
    }
    public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone)
    {
        Hits++;
        Checks.Mark("OnA");
        Checks.LastGlobalHit = hit;
        Checks.LastGlobalDone = damageDone;
    }
}

public sealed class BGlobalNpc : GlobalNPC
{
    public override bool InstancePerEntity => true;
    public bool? Decision, Collision;
    public int CanCalls, CollisionCalls, Hits;
    public override bool AppliesToEntity(NPC npc, bool late) => !late && (npc.type == NPCID.BlueSlime || NPCLoader.GetNPC(npc.type) is ProbeNpc);
    public override bool? CanBeHitByItem(NPC npc, Player player, Item item) { CanCalls++; Checks.Mark("NCanB"); return Decision; }
    public override bool? CanCollideWithPlayerMeleeAttack(NPC npc, Player player, Item item, Rectangle meleeAttackHitbox)
    {
        CollisionCalls++;
        Checks.Mark("NCollideB");
        return Collision;
    }
    public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
    {
        Checks.Mark("IncomingB");
        if (Checks.Arithmetic) modifiers.Defense.Base += 4;
    }
    public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers) => Checks.Mark("ModifyB");
    public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone) { Hits++; Checks.Mark("OnB"); }
}
