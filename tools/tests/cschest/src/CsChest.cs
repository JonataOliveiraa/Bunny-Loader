using System;
using System.Reflection;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CsChest;

public sealed class TestChestItem : ModItem
{
    public override void SetDefaults() => Item.DefaultToPlaceableTile(ModContent.TileType<TestChest>());
}

public sealed class DresserItem : ModItem
{
    public override void SetDefaults() => Item.DefaultToPlaceableTile(ModContent.TileType<TestDresser>());
}

public sealed class TestChest : ModTile
{
    public static bool AllowUnlock;
    public static int Unlocks, Locks;

    public override void SetStaticDefaults()
    {
        Main.tileContainer[Type] = true;
        Main.tileFrameImportant[Type] = true;
        Main.tileNoAttach[Type] = true;
        TileID.Sets.BasicChest[Type] = true;
        TileID.Sets.IsAContainer[Type] = true;
        AdjTiles = [TileID.Containers];
        RegisterItemDrop(ModContent.ItemType<TestChestItem>());
        TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
        TileObjectData.newTile.Origin = new Point16(0, 1);
        TileObjectData.newTile.CoordinateHeights = [16, 18];
        TileObjectData.newTile.HookCheckIfCanPlace = new PlacementHook(Chest.FindEmptyChest, -1, 0, true);
        TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(Chest.AfterPlacement_Hook, -1, 0, false);
        TileObjectData.newTile.AnchorInvalidTiles = [TileID.MagicalIceBlock, TileID.Boulder];
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.LavaDeath = false;
        TileObjectData.addTile(Type);
    }

    public override bool IsLockedChest(int i, int j) => Main.tile[i, j].TileFrameX / 36 == 1;

    public override bool UnlockChest(int i, int j, ref short frameXAdjustment, ref int dustType, ref bool manual)
    {
        Unlocks++;
        return AllowUnlock;
    }

    public override bool LockChest(int i, int j, ref short frameXAdjustment, ref bool manual)
    {
        Locks++;
        return TileObjectData.GetTileStyle(Main.tile[i, j]) == 0;
    }
}

public sealed class TestDresser : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileContainer[Type] = true;
        Main.tileFrameImportant[Type] = true;
        Main.tileNoAttach[Type] = true;
        TileID.Sets.BasicDresser[Type] = true;
        TileID.Sets.IsAContainer[Type] = true;
        RegisterItemDrop(ModContent.ItemType<DresserItem>());
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x2);
        TileObjectData.newTile.HookCheckIfCanPlace = new PlacementHook(Chest.FindEmptyChest, -1, 0, true);
        TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(Chest.AfterPlacement_Hook, -1, 0, false);
        TileObjectData.newTile.LavaDeath = false;
        TileObjectData.addTile(Type);
    }
}

public sealed class CsChestMod : Mod
{
    const int Stack = 7;

    static int frames, pass, fail;
    static bool done;
    static int baseX, floor;

    static ILog Log => ModContent.GetInstance<CsChestMod>().Logger;

    static readonly Assembly Api = typeof(ModTile).Assembly;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = 0;
            return;
        }
        if (i != Main.myPlayer || done) return;
        try { Step(self, ++frames); }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done) Log.Info($"cschest END pass={pass} fail={fail}");
    }

    static object? Call(string member, params object[] args) =>
        Api.GetType("Terraria.ModLoader.Core.ChestHooks", true)!.GetMethod(member, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args);

    static int Chest2x2Left => baseX;
    static int Chest2x2Top => floor - 2;

    static void Step(Player p, int f)
    {
        switch (f)
        {
            case 10:
                baseX = Main.spawnTileX + 6;
                floor = Ground(baseX);
                Reopened();
                Registration();
                break;
            case 20:
                Clear(baseX - 1, baseX + 14);
                PlaceAndStore();
                Locks();
                break;
            case 30:
                PageIcon(p);
                Dresser(p);
                break;
            case 40:
                Server();
                WorldFile.SaveWorld();
                done = true;
                break;
        }
    }

    static int Ground(int x)
    {
        for (int y = 50; y < Main.maxTilesY - 10; y++)
        {
            Tile tile = Main.tile[x, y];
            if (tile.HasTile && Main.tileSolid[tile.TileType] && tile.TileType != ModContent.TileType<TestChest>() && tile.TileType != ModContent.TileType<TestDresser>()) return y;
        }
        return Main.spawnTileY;
    }

    static void Clear(int from, int to)
    {
        for (int x = from; x <= to; x++)
            for (int y = floor - 6; y < floor; y++)
            {
                Tile tile = Main.tile[x, y];
                if (tile.HasTile && (tile.TileType == ModContent.TileType<TestChest>() || tile.TileType == ModContent.TileType<TestDresser>()))
                {
                    int chest = Chest.FindChest(x, y);
                    if (chest >= 0) Chest.DestroyChestDirect(x, y, chest);
                }
                WorldGen.KillTile(x, y, false, false, true);
            }
    }

    static void Reopened()
    {
        int chest = Chest.FindChest(Chest2x2Left, Chest2x2Top);
        Tile tile = Main.tile[Chest2x2Left, Chest2x2Top];
        bool found = chest >= 0 && tile.HasTile && tile.TileType == ModContent.TileType<TestChest>();
        if (!found)
        {
            Log.Info($"cschest persistence: no saved chest (first run) chest={chest} type={tile.TileType}");
            return;
        }
        Terraria.ChestItem item = Main.chest[chest].item[0];
        Report("the saved chest comes back with its tile and item", item.type == ItemID.DirtBlock && item.stack == Stack, $"type={item.type} stack={item.stack}");
    }

    static void Registration()
    {
        var vanilla = TileObjectData.GetTileData(TileID.Containers, 0, 0);
        var mine = TileObjectData.GetTileData(ModContent.TileType<TestChest>(), 0, 0);
        Report("PlacementHook takes the game's chest hooks", mine.HookCheckIfCanPlace.hook.Pointer == vanilla.HookCheckIfCanPlace.hook.Pointer &&
            mine.HookPostPlaceMyPlayer.hook.Pointer == vanilla.HookPostPlaceMyPlayer.hook.Pointer && mine.HookCheckIfCanPlace.badReturn == -1 && mine.HookCheckIfCanPlace.processedCoordinates,
            $"{mine.HookCheckIfCanPlace.hook.Pointer:X} {vanilla.HookCheckIfCanPlace.hook.Pointer:X}");
        Report("collection expressions fill the game's arrays", mine.AnchorInvalidTiles.Length == 2 && mine.AnchorInvalidTiles[1] == TileID.Boulder, $"{mine.AnchorInvalidTiles.Length}");
    }

    static void PlaceAndStore()
    {
        int id = WorldGen.PlaceChest(Chest2x2Left, floor - 1, (ushort)ModContent.TileType<TestChest>(), false, 0);
        Tile tile = Main.tile[Chest2x2Left, Chest2x2Top];
        Report("PlaceChest places the mod chest and makes its chest", id >= 0 && Chest.FindChest(Chest2x2Left, Chest2x2Top) == id && tile.TileType == ModContent.TileType<TestChest>(),
            $"id={id} type={tile.TileType}");
        if (id < 0) return;
        ref Terraria.ChestItem slot = ref Main.chest[id].item[0];
        slot.type = ItemID.DirtBlock;
        slot.stack = Stack;
        Report("TileObjectData.GetTileStyle reads the style", TileObjectData.GetTileStyle(tile) == 0, $"{TileObjectData.GetTileStyle(tile)}");
    }

    static void Locks()
    {
        int x = Chest2x2Left, y = Chest2x2Top;
        bool locked = Chest.Lock(x, y);
        Report("Chest.Lock calls LockChest and shifts the frame", locked && TestChest.Locks == 1 && Main.tile[x, y].TileFrameX == 36 && Chest.IsLocked(x, y),
            $"locked={locked} locks={TestChest.Locks} frameX={Main.tile[x, y].TileFrameX}");
        bool refused = Chest.Unlock(x, y);
        Report("UnlockChest returning false keeps it locked", !refused && TestChest.Unlocks == 1 && Chest.IsLocked(x, y), $"{refused}");
        TestChest.AllowUnlock = true;
        bool unlocked = Chest.Unlock(x, y);
        Report("UnlockChest returning true unlocks it", unlocked && Main.tile[x, y].TileFrameX == 0 && !Chest.IsLocked(x, y), $"frameX={Main.tile[x, y].TileFrameX}");
    }

    static void PageIcon(Player p)
    {
        int id = Chest.FindChest(Chest2x2Left, Chest2x2Top);
        p.OpenChest(Chest2x2Left, Chest2x2Top, id);
        var icons = GUIInstance.Active.GUIPageIcons;
        var texture = icons.GetCategoryTexture(GUIPageIcons.Category.Chest, false);
        var expected = Terraria.GameContent.TextureAssets.Item[ModContent.ItemType<TestChestItem>()].Value;
        Report("the chest page shows the chest's item", texture.Pointer == expected.Pointer, $"{texture.Width}x{texture.Height}");
        p.chest = -1;
    }

    static int FreeChest()
    {
        for (int i = 0; i < Main.chest.Length; i++)
            if (Main.chest[i].IsNull) return i;
        return -1;
    }

    static void Dresser(Player p)
    {
        int middle = baseX + 5, id = FreeChest();
        WorldGen.PlaceDresserDirect(middle, floor - 1, (ushort)ModContent.TileType<TestDresser>(), 0, id);
        int left = middle - 1, top = floor - 2;
        Report("PlaceDresserDirect places the mod dresser", Main.tile[left, top].TileType == ModContent.TileType<TestDresser>() && Chest.FindChest(left, top) == id,
            $"type={Main.tile[left, top].TileType} chest={Chest.FindChest(left, top)}");
        Vector2 old = p.position;
        p.position = new Vector2(middle * 16 - p.width / 2, (floor - 3) * 16);
        bool near = p.IsInInteractionRangeToMultiTileHitbox(left, top);
        p.position = new Vector2((middle + 60) * 16, old.Y);
        bool far = p.IsInInteractionRangeToMultiTileHitbox(left, top);
        p.position = old;
        Report("a mod dresser is in reach next to it and out of reach far away", near && !far, $"near={near} far={far}");
        Main.interactedDresserTopLeftX = left;
        Main.interactedDresserTopLeftY = top;
        var texture = GUIInstance.Active.GUIPageIcons.GetCategoryTexture(GUIPageIcons.Category.Dresser, false);
        var expected = Terraria.GameContent.TextureAssets.Item[ModContent.ItemType<DresserItem>()].Value;
        Report("the dresser page shows the dresser's item", texture.Pointer == expected.Pointer, $"{texture.Width}x{texture.Height}");
    }

    static void Server()
    {
        int x = baseX + 10, type = ModContent.TileType<TestChest>();
        Call("ServerPlace", x, floor - 1, type, 0, 0);
        int chest = Chest.FindChest(x, floor - 2);
        Report("the server places a client's mod chest", chest >= 0 && Main.tile[x, floor - 2].TileType == type, $"chest={chest}");
        Call("ServerKill", x + 1, floor - 1, type);
        Report("the server breaks it and removes its chest", Chest.FindChest(x, floor - 2) < 0 && !Main.tile[x, floor - 2].HasTile, $"chest={Chest.FindChest(x, floor - 2)}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cschest {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
