using System.IO;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace Terraria.ModLoader;

public abstract class ModTileEntity : ModType
{
    public int Type { get; internal set; } = -1;

    public int ID { get; internal set; } = -1;

    public Point16 Position { get; internal set; }

    public bool RequiresUpdates { get; set; }

    protected ModTileEntity()
    {
        RequiresUpdates = ItemHooks.Overrides(GetType(), typeof(ModTileEntity), nameof(Update));
    }

    protected sealed override void Register() => TileEntityHooks.Register(this);

    public static int CountInWorld() => TileEntityHooks.Count;

    public int Place(int i, int j) => TileEntityHooks.Place(this, i, j);

    public void Kill(int i, int j) => TileEntityHooks.Kill(this, i, j);

    public int Find(int i, int j) => TileEntityHooks.At(i, j) is ModTileEntity entity && entity.Type == Type ? entity.ID : -1;

    public abstract bool IsTileValidForEntity(int x, int y);

    public virtual int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate) => -1;

    public PlacementHook Generic_HookPostPlaceMyPlayer => new(Generic_Hook_AfterPlacement, -1, 0, true);

    public int Generic_Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
    {
        TileObjectData tileData = TileObjectData.GetTileData(type, style, alternate);
        Point16 topLeft = TileObjectData.TopLeft(i, j);
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetMessage.SendTileSquare(Main.myPlayer, topLeft.X, topLeft.Y, tileData.IsNull ? 1 : tileData.Width, tileData.IsNull ? 1 : tileData.Height);
            TileEntityHooks.RequestPlace(this, topLeft.X, topLeft.Y);
            return -1;
        }
        return Place(topLeft.X, topLeft.Y);
    }

    public virtual void NetPlaceEntityAttempt(int x, int y)
    {
        if (IsTileValidForEntity(x, y))
            TileEntityHooks.NetPlace(this, x, y);
    }

    public virtual void Update() { }

    public virtual void OnNetPlace() { }

    public virtual void PreGlobalUpdate() { }

    public virtual void PostGlobalUpdate() { }

    public virtual void OnKill() { }

    public virtual void SaveData(TagCompound tag) { }

    public virtual void LoadData(TagCompound tag) { }

    public virtual void NetSend(BinaryWriter writer) { }

    public virtual void NetReceive(BinaryReader reader) { }
}
