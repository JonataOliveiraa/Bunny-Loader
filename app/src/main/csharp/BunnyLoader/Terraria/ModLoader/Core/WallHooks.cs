using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BunnyLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Audio;
using Terraria.DataStructures;

namespace Terraria.ModLoader.Core;

internal static unsafe class WallHooks
{
    const uint FrameFlag = 1u, RandomFlag = 2u, LightFlag = 4u;

    static readonly HashSet<string> installed = new();

    static readonly Dictionary<(int, int, int), int> styles = new();

    static int[] lightCells = new int[12288];

    static Vector3[] lightValues = new Vector3[4096];

    static int lightCount;

    static bool lookupReady;

    [ThreadStatic]
    static int dustBudget, killing, dropOverride;

    [ThreadStatic]
    static bool failing, dropping;

    static int exploding;

    [ThreadStatic]
    static int frameX, frameY, frameNumber;

    static void Once(string name, Action action)
    {
        if (installed.Add(name))
            action();
    }

    static bool Overrides(ModWall wall, string name)
    {
        foreach (var method in wall.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (method.Name != name)
                continue;
            Type? root = method.GetBaseDefinition().DeclaringType;
            if (method.DeclaringType != root && (root == typeof(ModWall) || root == typeof(ModBlockType)))
                return true;
        }

        return false;
    }

    static bool Overrides(GlobalWall wall, string name)
    {
        foreach (var method in wall.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            if (method.Name == name && method.GetBaseDefinition().DeclaringType == typeof(GlobalWall) && method.DeclaringType != typeof(GlobalWall))
                return true;
        return false;
    }

    static void Converted(On_WorldGen.orig_Convert_ActuallyConvertWall orig, int conversionType, int x, int y, ref Tile tile, int newType)
    {
        int from = tile.wall;
        orig(conversionType, x, y, ref tile, newType);
        int to = tile.wall;
        if (from == to)
            return;
        foreach (GlobalWall global in WallLoader.Globals)
        {
            try { global.OnWallConverted(x, y, from, to, conversionType); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.OnWallConverted", e); }
        }
        WallLoader.GetWall(from)?.OnWallConverted(x, y, from, to, conversionType);
        WallLoader.GetWall(to)?.OnWallConverted(x, y, from, to, conversionType);
    }

    static void WantConverted() => Once("Converted", () => On_WorldGen.Convert_ActuallyConvertWall += Converted);

    static void WantKill()
    {
        Once("Kill", () => On_WorldGen.KillWall += Kill);
        Once("Dust", () => On_WorldGen.KillWall_MakeWallDust += MakeDust);
        Once("Sound", () => On_WorldGen.KillWall_PlaySounds += Sound);
        Once("Drop", () => On_WorldGen.KillWall_DropItems += Drop);
        Once("Drops", () => On_WorldGen.KillWall_GetItemDrops += Drops);
    }

    static void GlobalFrame(On_Framing.orig_WallFrame orig, int i, int j, bool resetFrame)
    {
        Tile tile = Main.tile[i, j];
        int type = tile.wall;
        if (type == 0 || !BuildLookup())
        {
            orig(i, j, resetFrame);
            return;
        }

        int oldX = tile.WallFrameX, oldY = tile.WallFrameY, oldNumber = tile.WallFrameNumber;
        orig(i, j, resetFrame);
        int x = tile.WallFrameX, y = tile.WallFrameY, number = tile.WallFrameNumber;
        bool known = styles.TryGetValue((x, y, number), out int style);
        int newStyle = style, newNumber = number;
        bool keep = true;
        foreach (GlobalWall global in WallLoader.Globals)
        {
            try { keep &= global.WallFrame(i, j, type, resetFrame, ref newStyle, ref newNumber); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.WallFrame", e); }
        }
        if (!keep)
        {
            if (tile.WallFrameX == x && tile.WallFrameY == y && tile.WallFrameNumber == number)
            {
                tile.WallFrameX = oldX;
                tile.WallFrameY = oldY;
                tile.WallFrameNumber = oldNumber;
            }
            return;
        }

        if (known && newStyle == style && newNumber == number)
            return;
        var lookup = Framing.wallFrameLookup;
        if ((uint)newStyle >= (uint)lookup.Length || lookup[newStyle].IsNull || (uint)newNumber >= (uint)lookup[newStyle].Length)
            return;
        Point16 point = lookup[newStyle][newNumber];
        tile.WallFrameNumber = newNumber;
        tile.WallFrameX = point.X;
        tile.WallFrameY = point.Y;
    }

    static void GlobalRandomOver(On_WorldGen.orig_UpdateWorld_OvergroundTile orig, int i, int j, bool checkNPCSpawns, int wallDist)
    {
        orig(i, j, checkNPCSpawns, wallDist);
        GlobalRandom(i, j, false);
    }

    static void GlobalRandomUnder(On_WorldGen.orig_UpdateWorld_UndergroundTile orig, int i, int j, bool checkNPCSpawns, int wallDist)
    {
        orig(i, j, checkNPCSpawns, wallDist);
        GlobalRandom(i, j, true);
    }

    static void GlobalRandom(int i, int j, bool underground)
    {
        int type = Main.tile[i, j].wall;
        if (type == 0)
            return;
        foreach (GlobalWall global in WallLoader.Globals)
        {
            try { global.RandomUpdate(i, j, type, underground); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.RandomUpdate", e); }
        }
    }

    static void GlobalPlace(On_WorldGen.orig_PlaceWall orig, int i, int j, int type, bool mute)
    {
        int before = Main.tile[i, j].wall;
        orig(i, j, type, mute);
        if (Main.tile[i, j].wall == before || Main.tile[i, j].wall != type)
            return;
        Item item = Main.LocalPlayer.HeldItem;
        foreach (GlobalWall global in WallLoader.Globals)
        {
            try { global.PlaceInWorld(i, j, type, item); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.PlaceInWorld", e); }
        }
    }

    internal static void Register(GlobalWall wall)
    {
        WantKill();
        PlayerWorldHooks.WantTeleport();
        if (Overrides(wall, nameof(GlobalWall.OnWallConverted))) WantConverted();
        if (Overrides(wall, nameof(GlobalWall.WallFrame))) Once("GlobalFrame", () => On_Framing.WallFrame += GlobalFrame);
        if (Overrides(wall, nameof(GlobalWall.RandomUpdate)))
            Once("GlobalRandom", () =>
            {
                On_WorldGen.UpdateWorld_OvergroundTile += GlobalRandomOver;
                On_WorldGen.UpdateWorld_UndergroundTile += GlobalRandomUnder;
            });
        if (Overrides(wall, nameof(GlobalWall.PlaceInWorld))) Once("GlobalPlace", () => On_WorldGen.PlaceWall += GlobalPlace);
        if (Overrides(wall, nameof(GlobalWall.CanPlace))) WantCanPlace();
        if (Overrides(wall, nameof(GlobalWall.CanExplode))) WantCanExplode();
        if (Overrides(wall, nameof(GlobalWall.PreDraw)) || Overrides(wall, nameof(GlobalWall.PostDraw))) WantDraw();
        if (Overrides(wall, nameof(GlobalWall.ModifyLight)))
        {
            Once("GlobalLight", () => On_Lighting.LightTiles += GlobalLight);
            Once("ApplyGlobalLight", () => On_Main.DoUpdateInWorld += ApplyGlobalLight);
        }
    }

    static readonly List<(int X, int Y, int Type, Vector3 Color)> globalLights = new();

    static void GlobalLight(On_Lighting.orig_LightTiles orig, int firstX, int lastX, int firstY, int lastY)
    {
        orig(firstX, lastX, firstY, lastY);
        globalLights.Clear();
        int left = Math.Max(firstX - 28, 0), right = Math.Min(lastX + 28, Main.maxTilesX - 1);
        int top = Math.Max(firstY - 28, 0), bottom = Math.Min(lastY + 28, Main.maxTilesY - 1);
        for (int j = top; j <= bottom; j++)
            for (int i = left; i <= right; i++)
            {
                int type = Main.tile[i, j].wall;
                if (type == 0)
                    continue;
                float r = 0, g = 0, b = 0;
                foreach (GlobalWall global in WallLoader.Globals)
                {
                    try { global.ModifyLight(i, j, type, ref r, ref g, ref b); }
                    catch (Exception e) { ItemHooks.Report($"{global.FullName}.ModifyLight", e); }
                }
                if (r != 0 || g != 0 || b != 0)
                    globalLights.Add((i, j, type, new Vector3(r, g, b)));
            }
    }

    static void ApplyGlobalLight(On_Main.orig_DoUpdateInWorld orig, Main self)
    {
        orig(self);
        if (Main.gameMenu)
        {
            globalLights.Clear();
            return;
        }

        foreach (var entry in globalLights)
            if (Main.tile[entry.X, entry.Y].wall == entry.Type)
                Lighting.AddLight(entry.X, entry.Y, entry.Color.X, entry.Color.Y, entry.Color.Z);
    }

    internal static void Register(ModWall wall)
    {
        Once("Kill", () => On_WorldGen.KillWall += Kill);
        Once("Dust", () => On_WorldGen.KillWall_MakeWallDust += MakeDust);
        Once("Sound", () => On_WorldGen.KillWall_PlaySounds += Sound);
        Once("Drop", () => On_WorldGen.KillWall_DropItems += Drop);
        Once("Drops", () => On_WorldGen.KillWall_GetItemDrops += Drops);
        uint flags = 0;
        if (Overrides(wall, nameof(ModWall.OnWallConverted)))
            WantConverted();
        if (Overrides(wall, nameof(ModBlockType.CanPlace)))
            WantCanPlace();
        if (Overrides(wall, nameof(ModBlockType.CanExplode)))
            WantCanExplode();
        if (Overrides(wall, nameof(ModBlockType.PreDraw)) || Overrides(wall, nameof(ModBlockType.PostDraw)))
            WantDraw();
        if (Overrides(wall, nameof(ModWall.CanBeTeleportedTo)))
            PlayerWorldHooks.WantTeleport();
        if (Overrides(wall, nameof(ModWall.WallFrame)))
            flags |= FrameFlag;
        if (Overrides(wall, nameof(ModWall.RandomUpdate)))
            flags |= RandomFlag;
        if (Overrides(wall, nameof(ModWall.ModifyLight)))
        {
            flags |= LightFlag;
            Once("Light", () => On_Lighting.LightTiles += Light);
            Once("ApplyLight", () => On_Main.DoUpdateInWorld += ApplyLight);
        }

        if (Overrides(wall, nameof(ModWall.AnimateWall)))
        {
            WallLoader.Animated.Add(wall);
            Once("Animate", () => On_Main.DoUpdate_AnimateWalls += Animate);
        }

        if (flags != 0 && Host.Native()->WallEvents(wall.Type, flags, &Event) == 0)
            throw new InvalidOperationException($"{wall.FullName}: native wall hooks could not be installed");
    }

    static void WantCanPlace() => Once("CanPlace", () => On_Player.PlaceThing_Walls += PlaceThingWalls);

    static void WantCanExplode()
    {
        Once("Kill", () => On_WorldGen.KillWall += Kill);
        Once("CanExplode", () => On_Projectile.ExplodeTiles += ExplodeTiles);
    }

    static void WantDraw() => Once("WallDraw", () => GameContent.Drawing.On_WallDrawing.DrawWalls += DrawWalls);

    static void PlaceThingWalls(On_Player.orig_PlaceThing_Walls orig, Player self)
    {
        int type = self.HeldItem.createWall;
        if (type > 0)
        {
            int i = Player.tileTargetX, j = Player.tileTargetY;
            if (WallLoader.GetWall(type)?.CanPlace(i, j) == false)
                return;
            foreach (GlobalWall global in WallLoader.Globals)
                if (!global.CanPlace(i, j, type))
                    return;
        }
        orig(self);
    }

    static void ExplodeTiles(On_Projectile.orig_ExplodeTiles orig, Projectile self, Vector2 compareSpot, int radius, int minI, int maxI, int minJ, int maxJ, bool wallSplode, bool explodeHardmodeOres)
    {
        exploding++;
        try { orig(self, compareSpot, radius, minI, maxI, minJ, maxJ, wallSplode, explodeHardmodeOres); }
        finally { exploding--; }
    }

    static readonly List<(int X, int Y)> hiddenWalls = new();

    static void DrawWalls(GameContent.Drawing.On_WallDrawing.orig_DrawWalls orig, GameContent.Drawing.WallDrawing self)
    {
        int extra = Main.drawToScreen ? 2 : Main.offScreenRange / 16;
        int left = Math.Max((int)Main.screenPosition.X / 16 - extra, 0), top = Math.Max((int)Main.screenPosition.Y / 16 - extra, 0);
        int right = Math.Min((int)Main.screenPosition.X / 16 + Main.screenWidth / 16 + extra + 1, Main.maxTilesX - 1), bottom = Math.Min((int)Main.screenPosition.Y / 16 + Main.screenHeight / 16 + extra + 1, Main.maxTilesY - 1);
        SpriteBatch batch = Main.spriteBatch;
        BeginDraw(batch, out bool begun);
        for (int j = top; j <= bottom; j++)
            for (int i = left; i <= right; i++)
            {
                Tile tile = Main.tile[i, j];
                int type = tile.wall;
                if (type == 0 || tile.invisibleWall())
                    continue;
                bool draw = true;
                foreach (GlobalWall global in WallLoader.Globals)
                {
                    try { draw &= global.PreDraw(i, j, type, batch); }
                    catch (Exception e) { ItemHooks.Report($"{global.FullName}.PreDraw", e); }
                }
                if (WallLoader.GetWall(type) is ModWall wall)
                {
                    try { draw &= wall.PreDraw(i, j, batch); }
                    catch (Exception e) { ItemHooks.Report($"{wall.FullName}.PreDraw", e); }
                }
                if (!draw)
                {
                    tile.invisibleWall(true);
                    hiddenWalls.Add((i, j));
                }
            }
        EndDraw(batch, begun);
        orig(self);
        foreach (var (x, y) in hiddenWalls)
            Main.tile[x, y].invisibleWall(false);
        hiddenWalls.Clear();
        BeginDraw(batch, out begun);
        for (int j = top; j <= bottom; j++)
            for (int i = left; i <= right; i++)
            {
                Tile tile = Main.tile[i, j];
                int type = tile.wall;
                if (type == 0 || tile.invisibleWall())
                    continue;
                if (WallLoader.GetWall(type) is ModWall wall)
                {
                    try { wall.PostDraw(i, j, batch); }
                    catch (Exception e) { ItemHooks.Report($"{wall.FullName}.PostDraw", e); }
                }
                foreach (GlobalWall global in WallLoader.Globals)
                {
                    try { global.PostDraw(i, j, type, batch); }
                    catch (Exception e) { ItemHooks.Report($"{global.FullName}.PostDraw", e); }
                }
            }
        EndDraw(batch, begun);
    }

    static void BeginDraw(SpriteBatch batch, out bool begun)
    {
        begun = batch._beginCalled;
        if (!begun)
            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, default, Main.Transform, true);
    }

    static void EndDraw(SpriteBatch batch, bool begun)
    {
        if (!begun)
            batch.End();
    }

    static void Kill(On_WorldGen.orig_KillWall orig, int i, int j, bool fail)
    {
        int wallType = Main.tile[i, j].wall;
        if (exploding > 0 && wallType != 0 && !WallLoader.CanExplode(i, j, wallType))
            return;
        ModWall? wall = WallLoader.GetWall(wallType);
        if (wall == null && (wallType == 0 || WallLoader.Globals.Count == 0))
        {
            orig(i, j, fail);
            return;
        }

        foreach (GlobalWall global in WallLoader.Globals)
        {
            try { global.KillWall(i, j, wallType, ref fail); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.KillWall", e); }
        }
        wall?.KillWall(i, j, ref fail);
        int count = fail ? 1 : 3;
        foreach (GlobalWall global in WallLoader.Globals)
        {
            try { global.NumDust(i, j, wallType, fail, ref count); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.NumDust", e); }
        }
        wall?.NumDust(i, j, fail, ref count);
        int previousBudget = dustBudget;
        bool previousFail = failing;
        dustBudget = Math.Max(0, count);
        failing = fail;
        killing++;
        try
        {
            orig(i, j, fail);
        }
        finally
        {
            dustBudget = previousBudget;
            failing = previousFail;
            killing--;
        }
    }

    static void MakeDust(On_WorldGen.orig_KillWall_MakeWallDust orig, int i, int j, Tile tile)
    {
        ModWall? wall = WallLoader.GetWall(tile.wall);
        if (wall == null && WallLoader.Globals.Count == 0)
        {
            orig(i, j, tile);
            return;
        }

        if (killing > 0)
        {
            if (dustBudget <= 0)
                return;
            dustBudget--;
        }

        int type = wall?.DustType ?? -1;
        foreach (GlobalWall global in WallLoader.Globals)
        {
            try
            {
                if (!global.CreateDust(i, j, tile.wall, ref type))
                    return;
            }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.CreateDust", e); }
        }
        if (wall == null)
        {
            if (type < 0)
                orig(i, j, tile);
            else
                Dust.NewDust(new Vector2(i * 16, j * 16), 16, 16, type, 0, 0, 0, default, 1);
            return;
        }
        if (!wall.CreateDust(i, j, ref type) || type < 0)
            return;
        Dust.NewDust(new Vector2(i * 16, j * 16), 16, 16, type, 0, 0, 0, default, 1);
    }

    static void Sound(On_WorldGen.orig_KillWall_PlaySounds orig, int i, int j, Tile tile)
    {
        foreach (GlobalWall global in WallLoader.Globals)
        {
            try
            {
                if (!global.KillSound(i, j, tile.wall, failing))
                    return;
            }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.KillSound", e); }
        }
        if (WallLoader.GetWall(tile.wall) is not ModWall wall)
        {
            orig(i, j, tile);
            return;
        }

        if (!wall.KillSound(i, j, failing) || wall.HitSound is not SoundStyle sound)
            return;
        SoundEngine.PlaySound(sound.Legacy, new Vector2(i * 16, j * 16), 0, 1);
    }

    static void Drop(On_WorldGen.orig_KillWall_DropItems orig, int i, int j, Tile tile)
    {
        ModWall? wall = WallLoader.GetWall(tile.wall);
        if (wall == null && WallLoader.Globals.Count == 0)
        {
            orig(i, j, tile);
            return;
        }

        int type = wall != null ? WallLoader.ItemDrop(wall) : VanillaDrops(tile);
        foreach (GlobalWall global in WallLoader.Globals)
        {
            try
            {
                if (!global.Drop(i, j, tile.wall, ref type))
                    return;
            }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.Drop", e); }
        }
        if (wall != null && !wall.Drop(i, j, ref type))
            return;
        int previous = dropOverride;
        bool wasDropping = dropping;
        dropOverride = type;
        dropping = true;
        try
        {
            orig(i, j, tile);
        }
        finally
        {
            dropOverride = previous;
            dropping = wasDropping;
        }
    }

    static int VanillaDrops(Tile tile)
    {
        bool was = dropping;
        dropping = false;
        try
        {
            return WorldGen.KillWall_GetItemDrops(tile);
        }
        finally
        {
            dropping = was;
        }
    }

    static int Drops(On_WorldGen.orig_KillWall_GetItemDrops orig, Tile tile)
    {
        if (dropping)
            return dropOverride;
        return WallLoader.GetWall(tile.wall) is ModWall wall ? WallLoader.ItemDrop(wall) : orig(tile);
    }

    static void Animate(On_Main.orig_DoUpdate_AnimateWalls orig)
    {
        orig();
        int size = Main.wallFrame.Length;
        foreach (ModWall wall in WallLoader.Animated)
            if (wall.Type < size)
                wall.AnimateWall(ref Main.wallFrame[wall.Type], ref Main.wallFrameCounter[wall.Type]);
    }

    static void Light(On_Lighting.orig_LightTiles orig, int firstX, int lastX, int firstY, int lastY)
    {
        orig(firstX, lastX, firstY, lastY);
        while (true)
        {
            int count;
            fixed (int* buffer = lightCells)
                count = Host.Native()->ScanWalls(firstX - 28, firstY - 28, lastX - firstX + 57, lastY - firstY + 57, LightFlag, buffer, lightCells.Length / 3);
            if (count >= 0)
            {
                lightCount = count;
                break;
            }

            Array.Resize(ref lightCells, checked(lightCells.Length * 2));
        }

        if (lightValues.Length < lightCount)
            Array.Resize(ref lightValues, lightCells.Length / 3);
        for (int k = 0; k < lightCount; k++)
        {
            int i = lightCells[k * 3], j = lightCells[k * 3 + 1];
            float r = 0, g = 0, b = 0;
            WallLoader.GetWall(lightCells[k * 3 + 2])?.ModifyLight(i, j, ref r, ref g, ref b);
            lightValues[k] = new Vector3(r, g, b);
        }
    }

    static void ApplyLight(On_Main.orig_DoUpdateInWorld orig, Main self)
    {
        orig(self);
        if (Main.gameMenu)
        {
            lightCount = 0;
            return;
        }

        for (int k = 0; k < lightCount; k++)
        {
            Vector3 color = lightValues[k];
            if (color.X == 0 && color.Y == 0 && color.Z == 0)
                continue;
            int i = lightCells[k * 3], j = lightCells[k * 3 + 1];
            if (Main.tile[i, j].wall != lightCells[k * 3 + 2])
                continue;
            Lighting.AddLight(i, j, color.X, color.Y, color.Z);
        }
    }

    static bool BuildLookup()
    {
        if (lookupReady)
            return true;
        var lookup = Framing.wallFrameLookup;
        if (lookup.IsNull)
            return false;
        for (int style = 0; style < lookup.Length; style++)
        {
            var row = lookup[style];
            if (row.IsNull)
                continue;
            for (int n = 0; n < row.Length; n++)
                styles.TryAdd((row[n].X, row[n].Y, n), style);
        }

        return lookupReady = true;
    }

    [UnmanagedCallersOnly]
    static int Event(int kind, int i, int j, int type, void* args)
    {
        try
        {
            ModWall? wall = WallLoader.GetWall(type);
            if (wall == null)
                return 1;
            if (kind == 2)
            {
                wall.RandomUpdate(i, j, *(int*)args != 0);
                return 1;
            }

            Tile tile = Main.tile[i, j];
            if (kind == 0)
            {
                frameX = tile.WallFrameX;
                frameY = tile.WallFrameY;
                frameNumber = tile.WallFrameNumber;
                return 1;
            }

            if (kind != 1 || !BuildLookup())
                return 1;
            int x = tile.WallFrameX, y = tile.WallFrameY, number = tile.WallFrameNumber;
            bool known = styles.TryGetValue((x, y, number), out int style);
            int newStyle = style, newNumber = number;
            if (!wall.WallFrame(i, j, *(int*)args != 0, ref newStyle, ref newNumber))
            {
                if (tile.WallFrameX == x && tile.WallFrameY == y && tile.WallFrameNumber == number)
                {
                    tile.WallFrameX = frameX;
                    tile.WallFrameY = frameY;
                    tile.WallFrameNumber = frameNumber;
                }

                return 1;
            }

            if (known && newStyle == style && newNumber == number)
                return 1;
            var lookup = Framing.wallFrameLookup;
            if ((uint)newStyle >= (uint)lookup.Length || lookup[newStyle].IsNull || (uint)newNumber >= (uint)lookup[newStyle].Length)
                return 1;
            Point16 point = lookup[newStyle][newNumber];
            tile.WallFrameNumber = newNumber;
            tile.WallFrameX = point.X;
            tile.WallFrameY = point.Y;
        }
        catch (Exception e)
        {
            ItemHooks.Report("WallLoader event " + kind, e);
        }

        return 1;
    }
}
