namespace Terraria.Audio;

public readonly partial struct LegacySoundStyle
{
    public bool HasValue => !IsNull;

    public LegacySoundStyle Value => this;
}
