using BunnyLoader.Interop;
using Terraria.ModLoader.Core;

namespace Terraria.Map;

public readonly partial struct IMapLayer
{
    public static IMapLayer Digtoise => Find(DigtoiseMapLayer.Class);
    public static IMapLayer BossBag => Find(BossBagMapLayer.Class);
    public static IMapLayer Spawn => Find(SpawnMapLayer.Class);
    public static IMapLayer TeamBasedSpawn => Find(TeamBasedSpawnMapLayer.Class);
    public static IMapLayer Pylons => Find(TeleportPylonsMapLayer.Class);
    public static IMapLayer Pings => Find(PingMapLayer.Class);

    static IMapLayer Find(GameClass type)
    {
        var list = new GameList(Main.MapIcons._layers);
        for (int i = 0; i < list.Count; i++)
            if (GameInterop.IsInstance(list[i], type)) return new IMapLayer(list[i]);
        return default;
    }

    public bool Visible
    {
        get => !SystemDrawHooks.IsHidden(Pointer);
        set => SystemDrawHooks.SetHidden(Pointer, !value);
    }
}
