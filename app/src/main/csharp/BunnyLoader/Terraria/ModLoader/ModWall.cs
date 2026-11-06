using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Localization;

namespace Terraria.ModLoader;

public abstract class ModWall : ModBlockType
{
    public override string LocalizationCategory => "Walls";

    protected sealed override void Register() => WallLoader.Register(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public void AddMapEntry(Color color, LocalizedText? name = null)
    {
        WallLoader.AddMapEntry(this, color, name ?? CreateMapEntryName());
    }

    public void AddMapEntry(Color color, LocalizedText name, Func<string, int, int, string> nameFunc)
    {
        throw new NotSupportedException("Coordinate-dependent map entry names are not supported by the mobile map legend");
    }

    public void RegisterItemDrop(int itemType)
    {
        WallLoader.RegisterItemDrop(Type, itemType);
    }

    public virtual bool Drop(int i, int j, ref int type) => true;

    public virtual bool CanBeTeleportedTo(int i, int j, Player player, string context) => true;

    public virtual void KillWall(int i, int j, ref bool fail)
    {
    }

    public virtual void AnimateWall(ref byte frame, ref byte frameCounter)
    {
    }

    public virtual bool WallFrame(int i, int j, bool randomizeFrame, ref int style, ref int frameNumber) => true;

    public virtual void OnWallConverted(int i, int j, int fromType, int toType, int conversionType)
    {
    }
}
