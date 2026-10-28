namespace Terraria.ModLoader;

public interface ILocalizedModType : IModType
{
    string LocalizationCategory { get; }
}
