namespace Terraria.ModLoader;

public interface ILoadable
{
    bool IsLoadingEnabled(Mod mod) => true;

    void Load(Mod mod);

    void Unload();
}
