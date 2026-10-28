namespace Terraria.ModLoader;

public abstract class GlobalItem : GlobalType<Item, GlobalItem>
{
    protected sealed override void Register() => ItemLoader.RegisterGlobal(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual void SetDefaults(Item entity) { }
}
