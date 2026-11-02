namespace Terraria.UI;

public delegate bool GameInterfaceDrawMethod();

public class GameInterfaceLayer
{
    public readonly string Name;
    public InterfaceScaleType ScaleType;
    public bool Active = true;

    internal System.IntPtr Native;

    public GameInterfaceLayer(string name, InterfaceScaleType scaleType)
    {
        Name = name;
        ScaleType = scaleType;
    }

    public bool Draw() => !Active || DrawSelf();

    protected virtual bool DrawSelf() => true;

    internal bool DrawFromGame() => DrawSelf();
}

public class LegacyGameInterfaceLayer : GameInterfaceLayer
{
    readonly GameInterfaceDrawMethod drawMethod;

    public LegacyGameInterfaceLayer(string name, GameInterfaceDrawMethod drawMethod, InterfaceScaleType scaleType = InterfaceScaleType.Game) : base(name, scaleType) =>
        this.drawMethod = drawMethod;

    protected override bool DrawSelf() => drawMethod();
}
