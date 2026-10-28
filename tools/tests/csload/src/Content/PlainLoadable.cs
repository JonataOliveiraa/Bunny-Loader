using Terraria.ModLoader;

namespace CsLoad.Content;

// Um ILoadable que nao e ModType: o autoload pega do mesmo jeito.
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
