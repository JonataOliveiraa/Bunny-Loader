using Terraria.ModLoader.Core;

namespace Terraria.Map;

public readonly partial struct IMapLayer
{
    public bool Visible
    {
        get => !SystemDrawHooks.IsHidden(Pointer);
        set => SystemDrawHooks.SetHidden(Pointer, !value);
    }
}
