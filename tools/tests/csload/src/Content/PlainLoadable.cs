using Terraria.ModLoader;

namespace CsLoad.Content;

public sealed class PlainLoadable : ILoadable
{
    public Mod? Loaded { get; private set; }

    public void Load(Mod mod)
    {
        Loaded = mod;
        Checks.Note("PlainLoadable.Load");
    }

    public void Unload() { }
}
