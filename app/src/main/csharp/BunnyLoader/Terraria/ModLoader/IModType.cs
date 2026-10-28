namespace Terraria.ModLoader;

public interface IModType
{
    Mod Mod { get; }

    string Name { get; }

    string FullName { get; }
}
