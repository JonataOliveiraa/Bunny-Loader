namespace ReLogic.Content;

public enum AssetRequestMode
{
    DoNotLoad,
    AsyncLoad,
    ImmediateLoad,
}

public static class AssetRequestModes
{
    public static int ToGame(AssetRequestMode mode) => mode switch
    {
        AssetRequestMode.AsyncLoad => 2,
        AssetRequestMode.ImmediateLoad => 1,
        _ => 0,
    };

    public static AssetRequestMode FromGame(int mode) => mode switch
    {
        1 => AssetRequestMode.ImmediateLoad,
        2 => AssetRequestMode.AsyncLoad,
        _ => AssetRequestMode.DoNotLoad,
    };
}
