using System;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsExMp;

public sealed class AnyPylonIsFine : GlobalPylon
{
    public override bool? ValidTeleportCheck_PreNPCCount(TeleportPylonInfo pylonInfo, ref int defaultNecessaryNPCCount) => true;

    public override bool? ValidTeleportCheck_PreBiomeRequirements(TeleportPylonInfo pylonInfo, SceneMetrics sceneData) => true;
}

public sealed class CsExMpMod : Mod
{
    static int frame, pass, fail, stage, waited, hits, sent;
    static bool ended;

    static ILog Log => ModContent.GetInstance<CsExMpMod>().Logger;

    static bool IsHost => (Main.netMode & NetmodeID.Server) != 0;

    static int X => Main.spawnTileX;

    static int BaseY => Main.spawnTileY - 12;

    static int Tile(string name) => ModContent.Find<ModTile>("ExampleMod/" + name).Type;

    public override void Load()
    {
        On_Player.Update += Update;
        On_Wiring.TripWire += TripWire;
    }

    static void TripWire(On_Wiring.orig_TripWire orig, int left, int top, int width, int height)
    {
        hits++;
        orig(left, top, width, height);
    }

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csexmp {(IsHost ? "host" : "client")} {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static int Players()
    {
        int count = 0;
        for (int k = 0; k < Main.maxPlayers; k++)
            if (Main.player[k].active)
                count++;
        return count;
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || ended || Players() < 2)
            return;
        self.creativeGodMode = true;
        frame++;
        try
        {
            if (IsHost) Host(self);
            else Client(self);
        }
        catch (Exception e)
        {
            Report("stage " + stage, false, e.ToString());
            stage++;
        }
    }

    static bool Timeout(string name, int frames)
    {
        if (++waited < frames)
            return false;
        Report(name, false, "timed out");
        Next();
        return true;
    }

    static void Next()
    {
        waited = 0;
        stage++;
    }

    static void End()
    {
        ended = true;
        Log.Info($"csexmp END pass={pass} fail={fail}");
    }

    static void Set(int left, int right, int y, int type)
    {
        for (int x = left; x <= right; x++)
        {
            Tile tile = Main.tile[x, y];
            tile.TileType = (ushort)type;
            tile.HasTile = true;
            tile.slope(0);
            tile.halfBrick(false);
        }
    }

    static void Host(Player self)
    {
        int sand = Tile("ExampleSand");
        if (stage >= 4)
            KeepActive();
        switch (stage)
        {
            case 0:
                if (frame < 120)
                    return;
                for (int x = X - 26; x <= X + 26; x++)
                    for (int y = BaseY - 10; y < BaseY; y++)
                    {
                        Tile tile = Main.tile[x, y];
                        tile.HasTile = false;
                        tile.WallType = 0;
                    }
                Set(X - 26, X + 26, BaseY, TileID.Stone);
                Set(X - 26, X + 26, BaseY + 1, TileID.Stone);
                WorldGen.RangeFrame(X - 26, BaseY - 10, X + 26, BaseY + 1);
                NetMessage.SendTileSquare(-1, X - 26, BaseY - 10, 53, 12);
                Next();
                break;
            case 1:
                if (++waited < 60)
                    return;
                Set(X + 10, X + 10, BaseY - 8, sand);
                WorldGen.SquareTileFrame(X + 10, BaseY - 8);
                Report("the mod sand starts falling", !Main.tile[X + 10, BaseY - 8].HasTile);
                Next();
                break;
            case 2:
                if (++waited < 240)
                    return;
                Report("the mod sand lands on the host", Main.tile[X + 10, BaseY - 1].HasTile && Main.tile[X + 10, BaseY - 1].TileType == sand, $"type={Main.tile[X + 10, BaseY - 1].TileType}");
                Set(X - 14, X - 2, BaseY, sand);
                WorldGen.RangeFrame(X - 14, BaseY - 1, X - 2, BaseY + 1);
                NetMessage.SendTileSquare(-1, X - 14, BaseY - 1, 13, 3);
                Next();
                break;
            case 3:
                if (++waited < 60)
                    return;
                for (int k = 0; k < 6000 && !(Main.tile[X - 8, BaseY - 2].HasTile && Main.tile[X - 8, BaseY - 2].TileType == TileID.Cactus); k++)
                {
                    int top = BaseY;
                    while (top > BaseY - 9 && Main.tile[X - 8, top - 1].HasTile && Main.tile[X - 8, top - 1].TileType == TileID.Cactus)
                        top--;
                    WorldGen.GrowCactus(X - 8, top);
                }
                Report("a cactus grows on the host", Main.tile[X - 8, BaseY - 1].TileType == TileID.Cactus && Main.tile[X - 8, BaseY].TileType == sand, $"top={Main.tile[X - 8, BaseY - 1].TileType} ground={Main.tile[X - 8, BaseY].TileType}");
                Next();
                break;
            case 4:
                if (Simple() == null || Entity(X - 16) == null)
                {
                    Timeout("the host gets the client's pylon entities", 3000);
                    return;
                }
                Report("the host gets the client's pylon entities", true);
                Next();
                break;
            case 5:
                if (++waited < 300)
                    return;
                WorldGen.PlaceTile(X + 2, BaseY - 1, Tile("ExamplePressurePlate"), true, true);
                Tile plate = Main.tile[X + 2, BaseY - 1];
                plate.RedWire = true;
                NetMessage.SendTileSquare(-1, X + 2, BaseY - 1, 1, 1);
                hits = 0;
                Next();
                break;
            case 6:
                if (hits == 0)
                {
                    Timeout("the client's weight trips the plate on the host", 3000);
                    return;
                }
                Report("the client's weight trips the plate on the host", true, $"hits={hits}");
                Next();
                break;
            default:
                End();
                break;
        }
    }

    static void KeepActive()
    {
        if (Entity(X - 16) is ModTileEntity entity)
        {
            var field = entity.GetType().GetField("isActive");
            if ((bool)field.GetValue(entity))
                return;
            field.SetValue(entity, true);
            NetMessage.SendData(MessageID.TileEntitySharing, number: entity.ID);
            sent++;
        }
    }

    static ModTileEntity Simple() => TileEntity.TryGet(new Point16(X + 15, BaseY - 4), out ModTileEntity entity) ? entity : null;

    static ModTileEntity Entity(int x) => TileEntity.TryGet(new Point16(x, BaseY - 3), out ModTileEntity entity) ? entity : null;

    static void Client(Player self)
    {
        int sand = Tile("ExampleSand");
        switch (stage)
        {
            case 0:
                if (!(Main.tile[X + 10, BaseY - 1].HasTile && Main.tile[X + 10, BaseY - 1].TileType == sand))
                {
                    Timeout("the mod sand lands on the client", 3000);
                    return;
                }
                Report("the mod sand lands on the client", true);
                Next();
                break;
            case 1:
                if (!(Main.tile[X - 8, BaseY - 1].HasTile && Main.tile[X - 8, BaseY - 1].TileType == TileID.Cactus))
                {
                    Timeout("the cactus reaches the client", 3000);
                    return;
                }
                Next();
                break;
            case 2:
                if (++waited < 120)
                    return;
                Report("the cactus keeps the mod sand on the client", Main.tile[X - 8, BaseY].TileType == sand, $"ground={Main.tile[X - 8, BaseY].TileType}");
                Report("the cactus is framed on the client", Main.tile[X - 8, BaseY - 1].TileType == TileID.Cactus);
                WorldGen.PlaceObject(X + 16, BaseY - 1, Tile("ExamplePylonTile"), true);
                ModContent.Find<ModTileEntity>("ExampleMod/SimplePylonTileEntity").Hook_AfterPlacement(X + 16, BaseY - 1, Tile("ExamplePylonTile"), 0, 1, 0);
                WorldGen.PlaceObject(X - 16, BaseY - 1, Tile("ExamplePylonTileAdvanced"), true);
                ModContent.Find<ModTileEntity>("ExampleMod/AdvancedPylonTileEntity").Hook_AfterPlacement(X - 16, BaseY - 1, Tile("ExamplePylonTileAdvanced"), 0, 1, 0);
                Next();
                break;
            case 3:
                if (Simple() == null || Entity(X - 16) == null)
                {
                    Timeout("the entities come back to the client", 3000);
                    return;
                }
                Report("the entities come back to the client", true);
                Next();
                break;
            case 4:
                bool active = Entity(X - 16) is ModTileEntity b && (bool)b.GetType().GetField("isActive").GetValue(b);
                if (!active)
                {
                    Timeout("the entity state reaches the client", 3000);
                    return;
                }
                Report("the entity state reaches the client", true);
                self.position = new Vector2((X + 14) * 16, (BaseY - 4) * 16);
                self.velocity = Vector2.Zero;
                waited = 0;
                stage++;
                break;
            case 5:
                if (++waited < 120)
                    return;
                self.position = new Vector2((X + 14) * 16, (BaseY - 4) * 16);
                self.velocity = Vector2.Zero;
                var info = new TeleportPylonInfo { PositionInTiles = new Point16(X - 16, BaseY - 3), TypeOfPylon = ((ModPylon)ModContent.Find<ModTile>("ExampleMod/ExamplePylonTileAdvanced")).PylonType };
                Main.PylonSystem.RequestTeleportation(info, self);
                Next();
                break;
            case 6:
                if (Math.Abs(self.Center.X / 16 - (X - 16)) > 8)
                {
                    Timeout("the host teleports the client to the mod pylon", 900);
                    return;
                }
                Report("the host teleports the client to the mod pylon", true, $"x={(int)(self.Center.X / 16)}");
                Next();
                break;
            case 7:
                if (!Main.tile[X + 2, BaseY - 1].HasTile)
                {
                    Timeout("the plate reaches the client", 3000);
                    return;
                }
                Report("the plate reaches the client", Main.tile[X + 2, BaseY - 1].TileType == Tile("ExamplePressurePlate"));
                self.position = new Vector2((X + 2) * 16 - 2, (BaseY - 3) * 16);
                self.velocity = Vector2.Zero;
                Next();
                break;
            case 8:
                if (++waited < 300)
                    return;
                End();
                break;
        }
    }
}
