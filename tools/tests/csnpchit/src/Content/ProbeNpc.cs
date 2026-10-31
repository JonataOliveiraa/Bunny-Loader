using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CsNpcHit.Content;

public sealed class ProbeNpc : ModNPC
{
    public bool? Decision, Collision;
    public bool Crit, DisableCrit = true, Hide, Instant;
    public int Limit = int.MaxValue, ArmorPenetration, CanCalls, CollisionCalls, IncomingCalls, ModifyCalls, HitCalls, Done, EffectCalls;
    public float DefenseScale = 1;
    public NPC.HitInfo Hit, Effect;
    public Player Attacker;
    public Item Weapon;

    public override string Texture => "CsNpcHit/Content/ProbeNpc";
    public override void SetDefaults()
    {
        NPC.width = NPC.height = 24;
        NPC.lifeMax = 100000;
        NPC.damage = 0;
        NPC.aiStyle = 0;
        NPC.townNPC = true;
        NPC.noGravity = NPC.noTileCollide = true;
    }
    public override bool? CanBeHitByItem(Player player, Item item) { CanCalls++; Checks.Mark("NCan"); return Decision; }
    public override bool? CanCollideWithPlayerMeleeAttack(Player player, Item item, Rectangle meleeAttackHitbox)
    {
        CollisionCalls++;
        Checks.Mark("NCollide");
        return Collision;
    }
    public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
    {
        IncomingCalls++;
        Checks.Mark("IncomingN");
        modifiers.Defense *= DefenseScale;
        if (DisableCrit) modifiers.DisableCrit();
        else if (Crit) modifiers.SetCrit();
    }
    public override void ModifyHitByItem(Player player, Item item, ref NPC.HitModifiers modifiers)
    {
        ModifyCalls++;
        Checks.Mark("ModifyN");
        modifiers.ArmorPenetration += ArmorPenetration;
        modifiers.SetMaxDamage(Limit);
        if (Hide) modifiers.HideCombatText();
        if (Instant) modifiers.SetInstantKill();
    }
    public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone)
    {
        HitCalls++;
        Checks.Mark("OnN");
        Hit = hit;
        Done = damageDone;
        Attacker = player;
        Weapon = item;
        if (Checks.Nested) NPC.StrikeNPC(7, 0, 1, false, false, player.whoAmI);
    }
    public override void HitEffect(NPC.HitInfo hit) { EffectCalls++; Effect = hit; Checks.Mark("EffectN"); }
    public override bool CheckDead() => false;
}
