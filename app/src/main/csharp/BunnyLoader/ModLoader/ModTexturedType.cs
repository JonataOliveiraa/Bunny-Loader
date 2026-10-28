namespace Terraria.ModLoader;

public abstract class ModTexturedType : ModType
{
    public virtual string Texture => (GetType().Namespace + "." + Name).Replace('.', '/');
}
