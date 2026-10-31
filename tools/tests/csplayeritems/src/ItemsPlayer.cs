using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CsPlayerItems;

public class ItemsPlayer : ModPlayer
{
    public static bool On, NoShoot, KeepAmmo, Veto, AutoReuse;
    public static readonly Dictionary<string, int> Calls = new();
    public static float SpeedIn, SpeedOut;
    public static int Consumed;

    static void Count(string name) => Calls[name] = Calls.GetValueOrDefault(name) + 1;

    public override void PostItemCheck() => Count(nameof(PostItemCheck));

    public override void ModifyWeaponDamage(Item item, ref StatModifier damage)
    {
        if (On) damage += 1f;
    }

    public override void ModifyWeaponCrit(Item item, ref float crit)
    {
        if (On) crit += 10;
    }

    public override void ModifyWeaponKnockback(Item item, ref StatModifier knockback)
    {
        if (On) knockback += 1f;
    }

    public override void ModifyItemScale(Item item, ref float scale)
    {
        if (On) scale *= 1.5f;
    }

    public override float UseTimeMultiplier(Item item) => On ? 2f : 1f;

    public override bool? CanAutoReuseItem(Item item) => AutoReuse ? true : null;

    public override void ModifyManaCost(Item item, ref float reduce, ref float mult)
    {
        if (On) mult *= 0.5f;
    }

    public override void OnConsumeMana(Item item, int manaConsumed) => Consumed = manaConsumed;

    public override void GetHealLife(Item item, bool quickHeal, ref int healValue)
    {
        if (On) healValue += 20;
    }

    public override bool CanShoot(Item item) => !NoShoot;

    public override void ModifyShootStats(Item item, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
    {
        Count(nameof(ModifyShootStats));
        SpeedIn = velocity.Length();
        velocity *= 2f;
    }

    public override bool Shoot(Item item, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        Count(nameof(Shoot));
        SpeedOut = velocity.Length();
        return true;
    }

    public override bool CanConsumeAmmo(Item weapon, Item ammo) => !KeepAmmo;

    public override void OnConsumeAmmo(Item weapon, Item ammo) => Count(nameof(OnConsumeAmmo));

    public override bool CanHitNPC(NPC target) => !Veto;

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) => Count(nameof(ModifyHitNPC));

    public override void ModifyHitNPCWithItem(Item item, NPC target, ref NPC.HitModifiers modifiers) => Count(nameof(ModifyHitNPCWithItem));

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => Count(nameof(OnHitNPC));

    public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone) => Count(nameof(OnHitNPCWithItem));

    public override void OnHitAnything(float x, float y, Entity victim) => Count(nameof(OnHitAnything));

    public override void MeleeEffects(Item item, Rectangle hitbox) => Count(nameof(MeleeEffects));
}
