using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CsTile;

public sealed class Block : ModTile
{
    public override string Texture => "CsTile/Block";
    public override void SetStaticDefaults() {
        Main.tileSolid[Type]=true; Main.tileLighted[Type]=true; MineResist=2; MinPick=50; DustType=DustID.Stone;
        AddMapEntry(new Color(30,160,240),this.GetLocalization("MapEntry",()=>"C# tile test"));
    }
    public override void KillTile(int i,int j,ref bool fail,ref bool effects,ref bool noItem) => Checks.Kills++;
    public override bool CanKillTile(int i,int j,ref bool damaged) => !Checks.Veto;
    public override void NumDust(int i,int j,bool fail,ref int num) { Checks.NumDust++; num=1; }
    public override bool CreateDust(int i,int j,ref int type) { Checks.Dust++; return true; }
    public override void RandomUpdate(int i,int j,bool underground) => Checks.Random++;
    public override void ModifyLight(int i,int j,ref float r,ref float g,ref float b) { Checks.Light++; r=0.4f; g=0.6f; b=0.8f; }
    public override bool IsTileDangerous(int i,int j,Player player) { Checks.Danger++; Checks.HighlightCoordinatesValid &= Main.tile[i,j].type==Type; return true; }
    public override bool IsTileSpelunkable(int i,int j) { Checks.Spelunk++; Checks.HighlightCoordinatesValid &= Main.tile[i,j].type==Type; return true; }
    public override bool CanDrop(int i,int j) => !Checks.DropVeto;
    public override void SetSpriteEffects(int i,int j,ref SpriteEffects effects) { Checks.Sprite++; effects=SpriteEffects.FlipHorizontally; }
    public override void AnimateIndividualTile(int type,int i,int j,ref int x,ref int y) => Checks.Individual++;
    public override bool RightClick(int i,int j) { Checks.Clicks++; return true; }
    public override void MouseOver(int i,int j) => Checks.Mouse++;
    public override void HitWire(int i,int j) => Checks.Wires++;
    public override void NearbyEffects(int i,int j,bool closer) => Checks.Near++;
    public override void AnimateTile(ref int frame,ref int counter) { Checks.Anim++; if(++counter>=4) {counter=0; frame=(frame+1)%2;} }
    public override bool PreDraw(int i,int j,SpriteBatch batch) { Checks.Pre++; return true; }
    public override void DrawEffects(int i,int j,SpriteBatch batch,ref TileDrawInfo data) { Checks.Effects++; Main.instance.TilesRenderer.AddSpecialLegacyPoint(i,j); }
    public override void PostDraw(int i,int j,SpriteBatch batch) => Checks.Post++;
    public override void SpecialDraw(int i,int j,SpriteBatch batch) => Checks.Special++;
}
public sealed class ObjectTile : ModTile
{
    public override string Texture => "CsTile/ObjectTile";
    public override void SetStaticDefaults() {
        Main.tileFrameImportant[Type]=true; Main.tileNoAttach[Type]=true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
        TileObjectData.newTile.Origin=new Point16(0,1);
        TileObjectData.newTile.CoordinateHeights=new[]{16,18};
        TileObjectData.newTile.AnchorBottom=new AnchorData(AnchorType.SolidTile,2,0);
        TileObjectData.newTile.StyleHorizontal=true;
        TileObjectData.addTile(Type);
        AdjTiles=new[]{(int)TileID.WorkBenches};
        RegisterItemDrop(ItemID.DirtBlock);
        AddMapEntry(new Color(180,90,40));
    }
    public override void PlaceInWorld(int i,int j,Item item) => Checks.Placed++;
    public override void KillMultiTile(int i,int j,int x,int y) => Checks.Multi++;
}
public sealed class BlockItem : ModItem
{
    public override string Texture => "CsTile/BlockItem";
    public override void SetDefaults() => Item.DefaultToPlaceableTile(ModContent.TileType<Block>());
}
public sealed class TileGlobal : GlobalTile
{
    public override void PostDraw(int i,int j,int type,SpriteBatch batch) { if(type==TileID.Dirt) Checks.GlobalDraw++; }
    public override bool CanKillTile(int i,int j,int type,ref bool damaged) { if(type==TileID.Dirt) Checks.GlobalCan++; return !Checks.GlobalVeto; }
    public override void KillTile(int i,int j,int type,ref bool fail,ref bool effects,ref bool noItem) { if(type==TileID.Dirt) Checks.GlobalKill++; }
    public override void Drop(int i,int j,int type) { if(type==TileID.Dirt) Checks.GlobalDrop++; }
    public override void ModifyLight(int i,int j,int type,ref float r,ref float g,ref float b) { if(type==TileID.Dirt) Checks.GlobalLight++; }
    public override bool TileFrame(int i,int j,int type,ref bool reset,ref bool keep) { if(type==TileID.Dirt) Checks.GlobalFrame++; return true; }
}
