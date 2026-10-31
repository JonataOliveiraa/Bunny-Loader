using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsNpcContact.Content;

public sealed class ContactNpc : ModNPC
{
    public static bool Allow = true;
    public static int Hits;

    public override void SetDefaults()
    {
        NPC.width = NPC.height = 24;
        NPC.lifeMax = 100000;
        NPC.damage = 10;
        NPC.defense = 0;
        NPC.aiStyle = 0;
        NPC.townNPC = true;
        NPC.friendly = false;
        NPC.noGravity = NPC.noTileCollide = true;
    }

    public override bool CanHitPlayer(Player target, ref int cooldownSlot)
    {
        Checks.Mark("CanN");
        return Allow;
    }

    public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers) => Checks.Mark("ModifyN");

    public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo)
    {
        Hits++;
        Checks.Mark("OnN");
    }

    public override bool CheckDead() => false;
}

public sealed class ContactGlobal : GlobalNPC
{
    public static int Hits;
    public static bool AllowZombie = true;

    public override bool AppliesToEntity(NPC entity, bool lateInstantiation) =>
        entity.type == ModContent.NPCType<ContactNpc>() || entity.type == NPCID.Zombie;

    public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot)
    {
        Checks.Mark("CanG");
        return true;
    }

    public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers) => Checks.Mark("ModifyG");

    public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo) => Checks.Mark("OnG");

    public override bool CanHitNPC(NPC npc, NPC target)
    {
        if (npc.type == NPCID.Zombie) Checks.MarkNpc("CanHitG");
        return AllowZombie;
    }

    public override void ModifyHitNPC(NPC npc, NPC target, ref NPC.HitModifiers modifiers) => Checks.MarkNpc("ModifyHitG");

    public override void OnHitNPC(NPC npc, NPC target, NPC.HitInfo hit)
    {
        Hits++;
        Checks.MarkNpc("OnHitG");
    }
}

public sealed class VictimNpc : ModNPC
{
    public static bool Allow = true;

    public override void SetDefaults()
    {
        NPC.width = NPC.height = 24;
        NPC.lifeMax = 100000;
        NPC.damage = 0;
        NPC.aiStyle = 0;
        NPC.townNPC = true;
        NPC.friendly = true;
        NPC.noGravity = NPC.noTileCollide = true;
    }

    public override bool CanBeHitByNPC(NPC attacker)
    {
        Checks.MarkNpc("CanBeHitV");
        return Allow;
    }

    public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) => Checks.MarkNpc("IncomingV");

    public override bool CheckDead() => false;
}

public sealed class ContactPlayer : ModPlayer
{
    public override bool CanBeHitByNPC(NPC npc, ref int cooldownSlot)
    {
        if (npc.type == ModContent.NPCType<ContactNpc>()) Checks.Mark("CanP");
        return true;
    }

    public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers)
    {
        if (npc.type == ModContent.NPCType<ContactNpc>()) Checks.Mark("ModifyP");
    }

    public override void OnHitByNPC(NPC npc, Player.HurtInfo hurtInfo)
    {
        if (npc.type == ModContent.NPCType<ContactNpc>()) Checks.Mark("OnP");
    }
}
