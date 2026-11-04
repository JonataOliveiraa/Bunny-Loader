using Terraria.DataStructures;
using Terraria.ModLoader.Core;

namespace Terraria;

public readonly partial struct Player
{
    public bool ItemAnimationEndingOrEnded => itemAnimation <= 1;
    public int ItemUsesThisAnimation => PlayerItemUseState.Uses(this);
    public bool HasAmmo(Item item) => HasAmmo(item, true);
    public IEntitySource GetSource_ItemUse_WithPotentialAmmo(Item item, int ammoItemId, string? context = null) => GetProjectileSource_Item_WithPotentialAmmo(item, ammoItemId);

    public bool PickAmmo(Item item, out int projectile, out float speed, out int damage, out float knockback, out int usedAmmoItemId, bool dontConsume = false)
    {
        projectile = item.shoot;
        speed = item.shootSpeed;
        damage = GetWeaponDamage(item);
        knockback = GetWeaponKnockback(item, item.knockBack);
        bool canShoot = true;
        PickAmmo(item, ref projectile, ref speed, ref canShoot, ref damage, ref knockback, out usedAmmoItemId, dontConsume);
        return canShoot;
    }
}
