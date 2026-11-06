using System;
using System.Linq;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Dyes;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace CsExE3;

public sealed class CsExE3Mod : Mod
{
    static int frame, pass, fail, floor, sandX, sandTop, sandBall, gunBall, cactusX, cactusTries, plateX, plateFloor, hits;

    static ILog Log => ModContent.GetInstance<CsExE3Mod>().Logger;

    static int Tile(string name) => ModContent.Find<ModTile>("ExampleMod/" + name).Type;

    static int Wall(string name) => ModContent.Find<ModWall>("ExampleMod/" + name).Type;

    static int ItemType(string name) => ModContent.Find<ModItem>("ExampleMod/" + name).Type;

    static int Proj(string name) => ModContent.Find<ModProjectile>("ExampleMod/" + name).Type;

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
        Log.Info($"csexe3 {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer)
            return;
        self.creativeGodMode = true;
        frame++;
        try
        {
            Step(self);
        }
        catch (Exception e)
        {
            Report("frame " + frame, false, e.ToString());
        }
    }

    static int FindFloor(int x, int fromY)
    {
        for (int y = fromY; y < Main.maxTilesY - 10; y++)
        {
            Tile tile = Main.tile[x, y];
            if (tile.HasTile && Main.tileSolid[tile.TileType])
                return y;
        }
        return fromY;
    }

    static void Clear(int left, int right, int top, int bottom)
    {
        for (int x = left; x <= right; x++)
            for (int y = top; y <= bottom; y++)
            {
                Tile tile = Main.tile[x, y];
                tile.HasTile = false;
                tile.WallType = 0;
                tile.LiquidAmount = 0;
            }
    }

    static void Solid(int left, int right, int y, int type)
    {
        for (int x = left; x <= right; x++)
        {
            Tile tile = Main.tile[x, y];
            tile.TileType = (ushort)type;
            tile.HasTile = true;
            tile.slope(0);
            tile.halfBrick(false);
            WorldGen.TileFrame(x, y, true, false);
        }
    }

    static void Step(Player self)
    {
        int cx = (int)(self.Center.X / 16);
        switch (frame)
        {
            case 30:
                floor = FindFloor(cx, (int)(self.Bottom.Y / 16) - 2);
                Sets();
                Dyes(self);
                Ammo(self);
                Conversion(self);
                Header();
                Globals();
                StartSand(cx);
                break;
            case 150:
                EndSand();
                StartCactus(cx);
                break;
            case 151:
            case 152:
            case 153:
            case 154:
            case 155:
            case 156:
            case 157:
            case 158:
                GrowMore();
                break;
            case 170:
                CheckCactus();
                Log.Info("csexe3 SHOT cactus");
                break;
            case 500:
                BreakCactus();
                Plate(self, cx);
                break;
            case 530:
                Report("standing on the mod pressure plate presses it and trips the wire", PressurePlateHelper.IsPressed(new Point(plateX, plateFloor - 1)) && hits > 0, $"pressed={PressurePlateHelper.IsPressed(new Point(plateX, plateFloor - 1))} hits={hits}");
                Log.Info($"csexe3 END pass={pass} fail={fail}");
                break;
        }
    }

    static void Sets()
    {
        int sand = Tile("ExampleSand");
        Report("the falling tables hold ExampleSand", TileID.Sets.FallingBlockProjectile[sand]?.FallingProjectileType == Proj("ExampleSandBallFallingProjectile") && ProjectileID.Sets.FallingBlockTileItem[Proj("ExampleSandBallFallingProjectile")]?.TileType == sand && TileID.Sets.Falling[sand]);
        Report("the vanilla sand keeps its entry", TileID.Sets.FallingBlockProjectile[TileID.Sand]?.FallingProjectileType == ProjectileID.SandBallFalling);
        Report("ExamplePressurePlate has a plate kind", TileID.Sets.PressurePlate[Tile("ExamplePressurePlate")] == -3 && TileID.Sets.PressurePlate[TileID.Stone] == -1);
        Report("ExampleCactus grows on ExampleSand", TileLoader.CanGrowModCactus(sand) && PlantLoader.Get<ModCactus>(TileID.Cactus, sand)?.GetTexture().Value.Width > 0 && PlantLoader.Get<ModCactus>(TileID.Cactus, sand)?.GetFruitTexture().Value.Width > 0 && !TileLoader.CanGrowModCactus(TileID.Stone));
    }

    static void Dyes(Player self)
    {
        var dye = new Item(ItemType("ExampleDye"));
        int id = GameShaders.Armor.GetShaderIdFromItemId(dye.type);
        Report("ExampleDye binds an armor shader", dye.dye > 0 && id == dye.dye, $"dye={dye.dye} id={id}");
        int hair = GameShaders.Hair.GetShaderIdFromItemId(ItemType("ExampleHairDye"));
        Color color = GameShaders.Hair.GetColor((short)hair, self, new Color(255, 255, 255, 255));
        Color disco = Main.DiscoColor;
        Report("ExampleHairDye gives the disco color", hair > 0 && Math.Abs(color.R - disco.R) < 40 && Math.Abs(color.G - disco.G) < 40, $"hair={hair} color={color} disco={disco}");
    }

    static void Ammo(Player self)
    {
        int block = ItemType("ExampleSandBlock");
        Report("ExampleSandBlock is sand ammo", new Item(block).ammo == AmmoID.Sand && ItemID.Sets.SandgunAmmoProjectileData[block]?.ProjectileType == Proj("ExampleSandBallGunProjectile"));
        self.inventory[54].SetDefaults(block, default);
        self.inventory[54].stack = 5;
        var gun = new Item(ItemID.Sandgun);
        int proj = gun.shoot, damage = 10;
        float speed = gun.shootSpeed, knock = 0;
        bool can = false;
        self.PickAmmo(gun, ref proj, ref speed, ref can, ref damage, ref knock, out int used, true);
        Report("the sandgun fires the mod's ball with its bonus", used == block && proj == Proj("ExampleSandBallGunProjectile") && damage >= 20, $"used={used} proj={proj} damage={damage}");
        self.inventory[54].TurnToAir();
    }

    static void Conversion(Player self)
    {
        int x = (int)(self.Center.X / 16) - 24, y = FindFloor((int)(self.Center.X / 16) - 24, (int)(self.Center.Y / 16)) - 8;
        Clear(x - 1, x + 1, y - 1, y + 1);
        Solid(x, x, y, TileID.DesertFossil);
        WorldGen.Convert(x, y, BiomeConversionID.Hallow, 0, true, true);
        Report("a fossil converts to the mod's hallowed fossil", Main.tile[x, y].TileType == Tile("HallowedFossilTile"), $"type={Main.tile[x, y].TileType}");
        WorldGen.Convert(x, y, BiomeConversionID.Sand, 0, true, true);
        Report("the desert solution brings the fossil back", Main.tile[x, y].TileType == TileID.DesertFossil, $"type={Main.tile[x, y].TileType}");
        Tile cell = Main.tile[x, y];
        cell.HasTile = false;
        cell.WallType = WallID.DesertFossilEcho;
        WorldGen.Convert(x, y, BiomeConversionID.Corruption, 0, true, true);
        Report("a fossil wall converts to the mod's wall", Main.tile[x, y].WallType == Wall("CorruptFossilWall"), $"wall={Main.tile[x, y].WallType}");
        cell = Main.tile[x, y];
        cell.WallType = 0;
        Solid(x, x, y + 1, TileID.Stone);
        Solid(x, x, y, TileID.Sand);
        ModBiomeConversion mine = ModLoader.GetMod("ExampleMod").GetContent<ModBiomeConversion>().First(c => c.Name == "ExampleSolutionConversion");
        WorldGen.Convert(x, y, mine.Type, 0, true, true);
        Report("ExampleSolution turns sand into ExampleSand", Main.tile[x, y].TileType == Tile("ExampleSand"), $"type={Main.tile[x, y].TileType}");
        Clear(x - 1, x + 1, y - 1, y + 1);
    }

    static void Header()
    {
        ModSystem header = ModLoader.GetMod("ExampleMod").GetContent<ModSystem>().First(s => s.Name == "ExampleWorldHeaderSystem");
        var tag = new TagCompound();
        header.SaveWorldHeader(tag);
        Report("ExampleWorldHeaderSystem writes its header", tag.GetBool("ExampleModExists"));
    }

    static void Globals()
    {
        Mod mod = ModLoader.GetMod("ExampleMod");
        Report("ExampleGlobalWall and ExampleGlobalPylon are loaded", mod.GetContent<GlobalWall>().Any() && mod.GetContent<GlobalPylon>().Any());
        ModPylon advanced = (ModPylon)ModContent.Find<ModTile>("ExampleMod/ExamplePylonTileAdvanced");
        Report("the advanced pylon registers its type", advanced.PylonType >= PylonLoader.VanillaPylonCount && TileID.Sets.CountsAsPylon.Contains(advanced.Type), $"type={(int)advanced.PylonType}");
    }

    static void StartSand(int cx)
    {
        sandX = cx + 20;
        sandTop = FindFloor(sandX, floor - 20) - 8;
        Clear(sandX - 1, sandX + 1, sandTop - 2, sandTop + 7);
        Tile cell = Main.tile[sandX, sandTop];
        cell.TileType = (ushort)Tile("ExampleSand");
        cell.HasTile = true;
        WorldGen.SquareTileFrame(sandX, sandTop);
        Report("ExampleSand starts falling", !Main.tile[sandX, sandTop].HasTile, $"has={Main.tile[sandX, sandTop].HasTile}");
        int falling = Proj("ExampleSandBallFallingProjectile");
        sandBall = -1;
        for (int k = 0; k < Main.maxProjectiles; k++)
            if (Main.projectile[k].active && Main.projectile[k].type == falling)
                sandBall = k;
        Report("the falling tile is the mod's projectile", sandBall >= 0, $"index={sandBall}");
        gunBall = Projectile.NewProjectile(Main.LocalPlayer.GetSource_FromThis(), new Vector2((sandX + 3) * 16 + 8, (sandTop) * 16), new Vector2(0, 2), Proj("ExampleSandBallGunProjectile"), 0, 0, Main.myPlayer);
    }

    static void EndSand()
    {
        int sand = Tile("ExampleSand");
        int landed = 0;
        for (int y = sandTop; y < sandTop + 12; y++)
            if (Main.tile[sandX, y].HasTile && Main.tile[sandX, y].TileType == sand)
                landed++;
        Report("the falling sand lands as the mod's tile", landed == 1, $"landed={landed} active={(sandBall >= 0 && Main.projectile[sandBall].active)}");
        int gunLanded = 0;
        for (int y = sandTop; y < sandTop + 12; y++)
            if (Main.tile[sandX + 3, y].HasTile && Main.tile[sandX + 3, y].TileType == sand)
                gunLanded++;
        Report("the sandgun's ball lands as the mod's tile", gunLanded == 1, $"landed={gunLanded}");
    }

    static void StartCactus(int cx)
    {
        cactusX = cx - 14;
        floor = FindFloor(cactusX, floor - 20);
        Clear(cactusX - 8, cactusX + 8, floor - 12, floor - 1);
        Solid(cactusX - 8, cactusX + 8, floor, Tile("ExampleSand"));
        cactusTries = 0;
    }

    static void GrowMore()
    {
        for (int k = 0; k < 400; k++)
        {
            int top = FindCactusTop();
            WorldGen.GrowCactus(cactusX, top);
            cactusTries++;
        }
    }

    static int FindCactusTop()
    {
        int y = floor;
        while (y > floor - 12 && Main.tile[cactusX, y - 1].HasTile && Main.tile[cactusX, y - 1].TileType == TileID.Cactus)
            y--;
        return y;
    }

    static void CheckCactus()
    {
        int height = floor - FindCactusTop();
        Report("a cactus grows on ExampleSand", height > 0, $"height={height} tries={cactusTries}");
        Report("the mod sand is back under it", Main.tile[cactusX, floor].TileType == Tile("ExampleSand"));
        WorldGen.CheckCactus(cactusX, floor - 1);
        Report("CheckCactus keeps a cactus on ExampleSand", Main.tile[cactusX, floor - 1].HasTile && Main.tile[cactusX, floor - 1].TileType == TileID.Cactus);
        Solid(cactusX + 7, cactusX + 7, floor - 1, TileID.Stone);
    }

    static void BreakCactus()
    {
        Tile ground = Main.tile[cactusX, floor];
        ground.TileType = TileID.Stone;
        WorldGen.SquareTileFrame(cactusX, floor);
        WorldGen.CheckCactus(cactusX, floor - 1);
        Report("a cactus on stone breaks", !Main.tile[cactusX, floor - 1].HasTile || Main.tile[cactusX, floor - 1].TileType != TileID.Cactus, $"has={Main.tile[cactusX, floor - 1].HasTile}");
    }

    static void Plate(Player self, int cx)
    {
        plateX = cx + 2;
        plateFloor = FindFloor(plateX, (int)(self.Bottom.Y / 16) - 2);
        Clear(plateX - 1, plateX + 1, plateFloor - 3, plateFloor - 1);
        WorldGen.PlaceTile(plateX, plateFloor - 1, Tile("ExamplePressurePlate"), true, true);
        Tile wire = Main.tile[plateX, plateFloor - 1];
        wire.RedWire = true;
        Report("the plate is placed", Main.tile[plateX, plateFloor - 1].HasTile && Main.tile[plateX, plateFloor - 1].TileType == Tile("ExamplePressurePlate"));
        self.position = new Vector2(plateX * 16 - self.width / 2 + 8, (plateFloor - 3) * 16 - 6);
        self.velocity = Vector2.Zero;
        hits = 0;
    }
}
