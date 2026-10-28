namespace Terraria.ModLoader;

public abstract class ModSystem : ModType
{
    protected sealed override void Register() => SystemLoader.Add(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual void PostSetupContent() { }

    public virtual void AddRecipeGroups() { }

    public virtual void AddRecipes() { }

    public virtual void PostAddRecipes() { }
}
