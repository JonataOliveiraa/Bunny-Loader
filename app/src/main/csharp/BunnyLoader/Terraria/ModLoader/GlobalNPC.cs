namespace Terraria.ModLoader;

public abstract class GlobalNPC : GlobalType<NPC, GlobalNPC>
{
    protected sealed override void Register() => NPCLoader.RegisterGlobal(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual void SetDefaults(NPC entity) { }
}
