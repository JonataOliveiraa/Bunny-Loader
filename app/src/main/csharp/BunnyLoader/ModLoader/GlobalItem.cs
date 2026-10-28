using Microsoft.Xna.Framework;

namespace Terraria.ModLoader;

public abstract class GlobalItem : GlobalType<Item, GlobalItem>
{
    protected sealed override void Register() => ItemLoader.RegisterGlobal(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual void SetDefaults(Item entity) { }

    public virtual bool CanUseItem(Item item, Player player) => true;

    public virtual bool? UseItem(Item item, Player player) => null;

    public virtual void UseAnimation(Item item, Player player) { }

    public virtual void HoldItem(Item item, Player player) { }

    public virtual void UseStyle(Item item, Player player, Rectangle heldItemFrame) { }

    public virtual void HoldStyle(Item item, Player player, Rectangle heldItemFrame) { }

    public virtual Vector2? HoldoutOffset(int type) => null;

    public virtual void UpdateInventory(Item item, Player player) { }
}
