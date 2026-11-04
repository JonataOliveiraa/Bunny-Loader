using System;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;

namespace Terraria.ModLoader;

public abstract class GlobalTile : ModType
{
    protected sealed override void Register() => TileLoader.Register(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual bool KillSound(int i, int j, int type, bool fail) => true;

    public virtual void NumDust(int i, int j, int type, bool fail, ref int num)
    {
    }

    public virtual bool CreateDust(int i, int j, int type, ref int dustType) => true;

    public virtual bool CanPlace(int i, int j, int type) => true;

    public virtual bool CanExplode(int i, int j, int type) => true;

    public virtual bool CanDrop(int i, int j, int type) => true;

    public virtual void Drop(int i, int j, int type)
    {
    }

    public virtual bool CanKillTile(int i, int j, int type, ref bool blockDamaged) => true;

    public virtual void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
    {
    }

    public virtual void PlaceInWorld(int i, int j, int type, Item item)
    {
    }

    public virtual void NearbyEffects(int i, int j, int type, bool closer)
    {
    }

    public virtual void ModifyLight(int i, int j, int type, ref float r, ref float g, ref float b)
    {
    }

    public virtual void RandomUpdate(int i, int j, int type, bool underground)
    {
    }

    public virtual void AnimateTile()
    {
    }

    public virtual void SetSpriteEffects(int i, int j, int type, ref SpriteEffects spriteEffects)
    {
    }

    public virtual bool? IsTileDangerous(int i, int j, int type, Player player) => null;

    public virtual bool? IsTileSpelunkable(int i, int j, int type) => null;

    public virtual bool PreDraw(int i, int j, int type, SpriteBatch spriteBatch) => true;

    public virtual void PostDraw(int i, int j, int type, SpriteBatch spriteBatch)
    {
    }

    public virtual void PreShakeTree(int x, int y, Enums.TreeTypes treeType)
    {
    }

    public virtual bool ShakeTree(int x, int y, Enums.TreeTypes treeType) => false;

    public virtual void OnTileConverted(int i, int j, int fromType, int toType, int conversionType)
    {
    }

    public virtual void EmitParticles(int i, int j, Tile tileCache, ushort typeCache, short tileFrameX, short tileFrameY, Color tileLight, bool visible)
    {
    }

    public virtual void DrawEffects(int i, int j, int type, SpriteBatch spriteBatch, ref TileDrawInfo drawData)
    {
    }

    public virtual void SpecialDraw(int i, int j, int type, SpriteBatch spriteBatch)
    {
    }

    public virtual void RightClick(int i, int j, int type)
    {
    }

    public virtual void MouseOver(int i, int j, int type)
    {
    }

    public virtual void MouseOverFar(int i, int j, int type)
    {
    }

    public virtual bool PreHitWire(int i, int j, int type) => true;

    public virtual void HitWire(int i, int j, int type)
    {
    }

    public virtual bool Slope(int i, int j, int type) => true;

    public virtual bool TileFrame(int i, int j, int type, ref bool resetFrame, ref bool noBreak) => true;

    public virtual bool CanReplace(int i, int j, int type, int tileTypeBeingPlaced) => true;

    public virtual int[] AdjTiles(int type) => Array.Empty<int>();
}
