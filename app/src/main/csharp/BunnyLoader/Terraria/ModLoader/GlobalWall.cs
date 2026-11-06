using Microsoft.Xna.Framework.Graphics;

namespace Terraria.ModLoader;

public abstract class GlobalWall : ModType
{
    protected sealed override void Register() => WallLoader.Register(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual bool KillSound(int i, int j, int type, bool fail) => true;

    public virtual void NumDust(int i, int j, int type, bool fail, ref int num)
    {
    }

    public virtual bool CreateDust(int i, int j, int type, ref int dustType) => true;

    public virtual bool CanPlace(int i, int j, int type) => true;

    public virtual bool CanExplode(int i, int j, int type) => true;

    public virtual bool PreDraw(int i, int j, int type, SpriteBatch spriteBatch) => true;

    public virtual void PostDraw(int i, int j, int type, SpriteBatch spriteBatch)
    {
    }

    public virtual void RandomUpdate(int i, int j, int type, bool underground)
    {
    }

    public virtual void PlaceInWorld(int i, int j, int type, Item item)
    {
    }

    public virtual void ModifyLight(int i, int j, int type, ref float r, ref float g, ref float b)
    {
    }

    public virtual bool Drop(int i, int j, int type, ref int dropType) => true;

    public virtual void KillWall(int i, int j, int type, ref bool fail)
    {
    }

    public virtual bool WallFrame(int i, int j, int type, bool randomizeFrame, ref int style, ref int frameNumber) => true;

    public virtual bool CanBeTeleportedTo(int i, int j, int type, Player player, string context) => true;

    public virtual void OnWallConverted(int i, int j, int fromType, int toType, int conversionType)
    {
    }
}
