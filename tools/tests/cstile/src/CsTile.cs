using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CsTile;

public sealed class CsTileMod : Mod
{
    public override void Load() { On_Player.Update+=Update; On_Lighting.AddLight_int_int_float_float_float+=Light; }
    static void Light(On_Lighting.orig_AddLight_int_int_float_float_float orig,int i,int j,float r,float g,float b) { Checks.ObserveLight(i,j,r,g,b); orig(i,j,r,g,b); }
    static void Update(On_Player.orig_Update orig,Player self,int i) { orig(self,i); if(!Main.gameMenu && i==Main.myPlayer) Checks.Frame(self); }
}
public static class Checks
{
    public static bool Veto,GlobalVeto,DropVeto;
    public static bool HighlightCoordinatesValid=true;
    public static int Danger,Spelunk,GlobalDraw,AppliedLight,Sprite,Individual,Placed;
    public static int Kills,NumDust,Dust,Random,Light,Clicks,Mouse,Wires,Near,Anim,Pre,Post,Effects,Special,Multi,GlobalCan,GlobalKill,GlobalDrop,GlobalLight,GlobalFrame;
    static int frames,pass,fail,x,y;
    static bool done;
    public static void ObserveLight(int i,int j,float r,float g,float b) { if(frames>=120 && i==x && j==y && r>=0.39f && g>=0.59f && b>=0.79f) AppliedLight++; }
    static void Log(string text)=>ModContent.GetInstance<CsTileMod>().Logger.Info(text);
    static void Check(string name,bool ok,string detail="") { if(ok)pass++;else fail++; Log($"cstile {name}: {(ok?"ok":"FAIL")} {detail}"); }
    public static void Frame(Player player) {
        if(done)return;
        if(frames>=120) { player.dangerSense=true; player.findTreasure=true; }
        try {
            if(++frames==120) Run(player);
            if(frames==240) Finish();
        } catch(Exception e) { Check("run",false,e.ToString()); End(); }
    }
    static bool Active(int i,int j,int type) { Tile t=Main.tile[i,j]; return t.active() && t.type==type; }
    static void Run(Player player) {
        player.dangerSense=true; player.findTreasure=true;
        int block=ModContent.TileType<Block>(),obj=ModContent.TileType<ObjectTile>();
        x=(int)player.Center.X/16+5; y=(int)player.Center.Y/16-2;
        Tile saved=Main.tile[64,64];
        bool reopened=saved.active() && saved.type==block;
        Log($"cstile persistence reopened={reopened}");
        string priorSidecar=(Main.ActiveWorldFileData.Path??"")+".tiles.bl";
        bool expectedReload=File.Exists(priorSidecar) && File.ReadAllText(priorSidecar).Contains("cbd56088-bc74-47ac-8e52-577889e145b1/Block");
        if(expectedReload) Check("tile restored after reopening",reopened && saved.frameX==18 && saved.frameY==36,$"type={saved.type}, frame={saved.frameX},{saved.frameY}");
        Check("tile registration",block>=TileID.Count && obj>block);
        Check("static tables",Main.tileSolid[block] && Main.tileLighted[block] && Main.tileFrameImportant[obj]);
        Check("object data locked after setup",TileObjectData.readOnlyData);
        var data=TileObjectData.GetTileData(obj,0,0);
        Check("object shape",!data.IsNull && data.Width==2 && data.Height==2 && data.CoordinateHeights[1]==18 && data.Origin.Y==1);
        Item item=new(ModContent.ItemType<BlockItem>());
        Check("placeable item",item.createTile==block && item.consumable);
        int map=MapHelper.tileLookup[block];
        Check("map entry",map>0 && Lang.GetMapObjectName(map)=="C# tile test",$"map={map}");
        for(int i=x-1;i<=x+12;i++) for(int j=y-2;j<=y+2;j++) WorldGen.KillTile(i,j,false,false,true);
        Check("place simple",WorldGen.PlaceTile(x,y,block,true,true,-1,0) && Active(x,y,block));
        Veto=true; Check("own CanKill veto",!WorldGen.CanKillTile(x,y,out _)); Veto=false;
        Check("minimum pick power",player.GetPickaxeDamage(x,y,49,0,Main.tile[x,y])==0);
        int wired=Wires; Wiring.HitWireSingle(x,y); Check("wire callback",Wires==wired+1);
        DropVeto=true; WorldGen.KillTile_GetItemDrops(x,y,Main.tile[x,y],out int suppressed,out _,out _,out _,out _,false); DropVeto=false;
        Check("CanDrop veto",suppressed==0);
        int random=Random; for(int n=0;n<8;n++) WorldGen.UpdateWorld_OvergroundTile(x,y,false,0);
        Check("filtered random update",Random-random==8,$"calls={Random-random}");
        Player.tileTargetX=x; Player.tileTargetY=y; player.TileInteractionsMouseOver(x,y);
        Check("cursor icon",Mouse>0 && player.cursorItemIconEnabled);
        player.tileInteractAttempted=true; player.releaseUseTile=true;
        int clicked=Clicks;
        player.TileInteractionsUse(x,y); player.TileInteractionsUse(x,y); player.TileInteractionsUse(x,y);
        Check("one right click per frame",Clicks-clicked==1,$"calls={Clicks-clicked}");
        WorldGen.KillTile_GetItemDrops(x,y,Main.tile[x,y],out int drop,out _,out _,out _,out _,false);
        Check("automatic item drop",drop==item.type,$"drop={drop}");
        int kills=Kills; WorldGen.KillTile(x,y,false,false,false);
        Check("break simple",!Active(x,y,block) && Kills>kills);
        Check("dust hooks",NumDust>0 && Dust>0,$"num={NumDust}, dust={Dust}");
        WorldGen.PlaceTile(x,y,block,true,true,-1,0);
        for(int i=x+3;i<=x+4;i++) WorldGen.PlaceTile(i,y+2,TileID.Stone,true,true,-1,0);
        bool can=TileObject.CanPlace(x+3,y+1,obj,0,1,out TileObject objectData,false,default);
        bool placed=can && TileObject.Place(objectData);
        Check("place 2x2",placed && Active(x+3,y,obj) && Active(x+4,y+1,obj),$"can={can}");
        if(placed) {
            TileObjectData.CallPostPlacementPlayerHook(x+3,y+1,obj,0,1,0,objectData);
            Check("placement callback",Placed==1);
            WorldGen.KillTile_DropItems(x+3,y,Main.tile[x+3,y],false);
            WorldGen.KillTile_GetItemDrops(x+3,y,Main.tile[x+3,y],out int small,out _,out _,out _,out _,false);
            WorldGen.KillTile_GetItemDrops(x+3,y,Main.tile[x+3,y],out int large,out _,out _,out _,out _,true);
            Check("multi-cell drop",small==0 && large==ItemID.DirtBlock);
            WorldGen.KillTile(x+3,y,false,false,false); WorldGen.TileFrame(x+4,y,false,false);
            Check("break whole object",!Active(x+4,y+1,obj) && Multi==1,$"multi={Multi}");
        }
        WorldGen.PlaceTile(x+7,y,TileID.Dirt,true,true,-1,0);
        GlobalVeto=true; Check("global CanKill veto",!WorldGen.CanKillTile(x+7,y,out _)); GlobalVeto=false;
        WorldGen.TileFrame(x+7,y,false,false);
        WorldGen.KillTile(x+7,y,false,false,false);
        Check("global vanilla hooks",GlobalCan>0 && GlobalKill>0 && GlobalDrop>0 && GlobalFrame>0,$"can={GlobalCan},kill={GlobalKill},drop={GlobalDrop},frame={GlobalFrame}");
        WorldGen.PlaceTile(x+8,y,TileID.Dirt,true,true,-1,0);
        WorldGen.PlaceTile(64,64,block,true,true,-1,0);
        Tile marker=Main.tile[64,64]; marker.frameX=18; marker.frameY=36;
    }
    static void Finish() {
        Check("animation callback",Anim>0,$"calls={Anim}");
        Check("light callback",Light>0 && GlobalLight>0 && AppliedLight>0,$"own={Light},global={GlobalLight},applied={AppliedLight}");
        Check("draw callbacks",Pre>0 && Effects>0 && Post>0 && Special>0,$"pre={Pre},effects={Effects},post={Post},special={Special}");
        Check("native draw data callbacks",Sprite>0 && Individual>0,$"sprite={Sprite},individual={Individual}");
        Check("tile highlighting",Danger>0 && Spelunk>0 && HighlightCoordinatesValid,$"danger={Danger},spelunk={Spelunk}");
        Check("global vanilla drawing",GlobalDraw>0,$"calls={GlobalDraw}");
        Check("nearby effects",Near>0,$"calls={Near}");
        WorldFile.SaveWorld();
        string? path=Main.ActiveWorldFileData.Path;
        string sidecar=(path??"")+".tiles.bl";
        Check("native tile sidecar",File.Exists(sidecar) && File.ReadAllText(sidecar).StartsWith("bunny-tiles 1\n") && File.ReadAllText(sidecar).Contains("cbd56088-bc74-47ac-8e52-577889e145b1/Block"),sidecar);
        End();
    }
    static void End() { done=true; Log($"cstile END pass={pass} fail={fail}"); }
}
