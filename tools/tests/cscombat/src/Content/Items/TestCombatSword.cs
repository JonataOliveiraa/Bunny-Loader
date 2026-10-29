using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsCombat.Content.Items;

public class TestCombatSword : ModItem
{
    public override void SetDefaults()
    {
        Item.DamageType = DamageClass.Melee;
        Item.width = 40;
        Item.height = 40;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.useTime = 20;
        Item.useAnimation = 20;
        Item.damage = 40;
        Item.knockBack = 4;
        Item.crit = 4;
    }

    public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
    {
        if (!Checks.On) return;
        damage += 0.5f;
        damage.Flat += 3;
    }

    public override void ModifyWeaponCrit(Player player, ref float crit)
    {
        if (Checks.On) crit += 20;
    }

    public override void ModifyWeaponKnockback(Player player, ref StatModifier knockback)
    {
        if (Checks.On) knockback *= 2;
    }

    public override void ModifyItemScale(Player player, ref float scale)
    {
        if (Checks.On) scale *= 1.5f;
    }

    public override void UseItemHitbox(Player player, ref Rectangle hitbox, ref bool noHitbox) => Checks.HitboxCalls++;

    public override void MeleeEffects(Player player, Rectangle hitbox) => Checks.EffectsCalls++;

    public override bool? CanHitNPC(Player player, NPC target) => Checks.Veto ? false : null;

    public override bool? CanMeleeAttackCollideWithNPC(Rectangle meleeAttackHitbox, Player player, NPC target) => Checks.ForceCollide ? true : null;

    public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers)
    {
        Checks.ModifyCalls++;
        modifiers.FinalDamage.Flat += 100;
        modifiers.DisableCrit();
    }

    public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone)
    {
        Checks.OnHitCalls++;
        Checks.LastHit = hit;
        Checks.LastDone = damageDone;
    }
}
