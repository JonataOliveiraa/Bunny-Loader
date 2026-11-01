using System;
using System.IO;
using System.Reflection;
using log4net;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace CsTileEntity;

public sealed class TestEntity : ModTileEntity
{
    public static int Updates, Pre, Post, Kills, NetPlaces;
    public int Value;

    public override bool IsTileValidForEntity(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        return tile.HasTile && tile.TileType == ModContent.TileType<EntityTile>();
    }

    public override void Update() => Updates++;

    public override void PreGlobalUpdate() => Pre++;

    public override void PostGlobalUpdate() => Post++;

    public override void OnKill() => Kills++;

    public override void OnNetPlace() => NetPlaces++;

    public override void SaveData(TagCompound tag) => tag["Value"] = Value;

    public override void LoadData(TagCompound tag) => Value = tag.GetInt("Value");

    public override void NetSend(BinaryWriter writer) => writer.Write(Value);

    public override void NetReceive(BinaryReader reader) => Value = reader.ReadInt32();
}

public sealed class EntityTile : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileNoAttach[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
        TileObjectData.newTile.CoordinateHeights = [16, 18];
        TileObjectData.newTile.HookPostPlaceMyPlayer = ModContent.GetInstance<TestEntity>().Generic_HookPostPlaceMyPlayer;
        TileObjectData.addTile(Type);
    }

    public override void KillMultiTile(int i, int j, int frameX, int frameY) => ModContent.GetInstance<TestEntity>().Kill(i, j);
}

public sealed class CsTileEntityMod : Mod
{
    const int Saved = 42;

    static int frames, pass, fail, baseX, floor, updatesAt;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsTileEntityMod>().Logger;

    static readonly Assembly Api = typeof(ModTileEntity).Assembly;

    public override void Load() => On_Player.Update += Update;

    static object? Call(string member, params object[] args) =>
        Api.GetType("Terraria.ModLoader.Core.TileEntityHooks", true)!.GetMethod(member, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args);

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = 0;
            return;
        }
        if (i != Main.myPlayer || done) return;
        try { Step(++frames); }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done) Log.Info($"cstileentity END pass={pass} fail={fail}");
    }

    static string Key => $"{ModContent.GetInstance<CsTileEntityMod>().Uid}/{nameof(TestEntity)}";

    static void Step(int f)
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
                Clear();
                Placement();
                updatesAt = TestEntity.Updates;
                break;
            case 60:
                Report("Update, PreGlobalUpdate and PostGlobalUpdate run every world update", TestEntity.Updates > updatesAt && TestEntity.Pre > 0 && TestEntity.Post > 0,
                    $"updates={TestEntity.Updates - updatesAt} pre={TestEntity.Pre} post={TestEntity.Post}");
                Network();
                Killing();
                WorldFile.SaveWorld();
                string file = Main.worldPathName + ".bl.json";
                Report("the entity is saved in the world's .bl.json", File.Exists(file) && File.ReadAllText(file).Contains("#csTileEntities"), file);
                done = true;
                break;
        }
    }

    static int Ground(int x)
    {
        for (int y = 50; y < Main.maxTilesY - 10; y++)
        {
            Tile tile = Main.tile[x, y];
            if (tile.HasTile && Main.tileSolid[tile.TileType]) return y;
        }
        return Main.spawnTileY;
    }

    static void Clear()
    {
        for (int x = baseX - 1; x <= baseX + 12; x++)
            for (int y = floor - 5; y < floor; y++)
                WorldGen.KillTile(x, y, false, false, true);
    }

    static bool PlaceTile(int x)
    {
        bool placed = WorldGen.PlaceObject(x, floor - 1, ModContent.TileType<EntityTile>(), true);
        return placed && Main.tile[x, floor - 2].TileType == ModContent.TileType<EntityTile>();
    }

    static void Reopened()
    {
        if (!TileEntity.TryGet(baseX, floor - 2, out TestEntity entity))
        {
            Log.Info("cstileentity persistence: no saved entity (first run)");
            return;
        }
        Report("the saved entity comes back with its data", entity.Value == Saved && entity.Position.X == baseX && entity.Position.Y == floor - 2, $"value={entity.Value} at {entity.Position.X},{entity.Position.Y}");
    }

    static void Registration()
    {
        var template = ModContent.GetInstance<TestEntity>();
        var data = TileObjectData.GetTileData(ModContent.TileType<EntityTile>(), 0, 0);
        Report("the template gets its type and the placement hook becomes a loader marker", template.Type == 0 && data.HookPostPlaceMyPlayer.hook.IsNull && data.HookPostPlaceMyPlayer.badReturn >= 0x5C000000,
            $"type={template.Type} badReturn={data.HookPostPlaceMyPlayer.badReturn:X}");
    }

    static void Placement()
    {
        bool placed = PlaceTile(baseX);
        int type = ModContent.TileType<EntityTile>();
        TileObjectData.CallPostPlacementPlayerHook(baseX, floor - 1, type, 0, 1, 0, new TileObject { xCoord = baseX, yCoord = floor - 1, type = type });
        bool found = TileEntity.TryGet(baseX + 1, floor - 1, out TestEntity entity);
        Report("Generic_HookPostPlaceMyPlayer places the entity at the top left", placed && found && entity.Position.X == baseX && entity.Position.Y == floor - 2 && entity.ID >= 0x50000000,
            found ? $"at {entity.Position.X},{entity.Position.Y} id={entity.ID:X}" : $"placed={placed} found={found}");
        if (!found) return;
        entity.Value = Saved;
        var template = ModContent.GetInstance<TestEntity>();
        Report("Find and CountInWorld", template.Find(baseX, floor - 2) == entity.ID && template.Find(baseX + 1, floor - 2) == -1 && ModTileEntity.CountInWorld() == 1,
            $"find={template.Find(baseX, floor - 2)} count={ModTileEntity.CountInWorld()}");
    }

    static void Network()
    {
        TileEntity.TryGet(baseX, floor - 2, out TestEntity entity);
        byte[] payload = (byte[])Call("Payload", entity)!;
        int x = baseX + 4;
        PlaceTile(x);
        Call("Apply", 0x5FFFFFF0, x, floor - 2, Key, payload);
        bool copied = TileEntity.TryGet(x, floor - 2, out TestEntity copy);
        Report("NetSend and NetReceive carry the data to another entity", copied && copy.Value == Saved && copy.ID == 0x5FFFFFF0, copied ? $"value={copy.Value}" : "not created");
        ModContent.GetInstance<TestEntity>().Kill(x, floor - 2);
        int y = baseX + 8;
        PlaceTile(y);
        Call("ServerPlace", Key, y, floor - 2);
        Report("the server places a client's entity and calls OnNetPlace", TileEntity.TryGet(y, floor - 2, out TestEntity placed) && TestEntity.NetPlaces == 1, $"netPlaces={TestEntity.NetPlaces}");
        Call("ServerPlace", Key, y + 2, floor - 2);
        Report("the server refuses a spot without the entity's tile", !TileEntity.TryGet(y + 2, floor - 2, out TestEntity _), "");
    }

    static void Killing()
    {
        int x = baseX + 8, kills = TestEntity.Kills;
        WorldGen.KillTile(x, floor - 2, false, false, true);
        Report("breaking the tile kills its entity with OnKill", !TileEntity.TryGet(x, floor - 2, out TestEntity _) && TestEntity.Kills == kills + 1 && ModTileEntity.CountInWorld() == 1,
            $"kills={TestEntity.Kills - kills} count={ModTileEntity.CountInWorld()}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cstileentity {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
