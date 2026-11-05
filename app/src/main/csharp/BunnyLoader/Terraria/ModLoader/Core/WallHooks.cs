using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BunnyLoader;
using Microsoft.Xna.Framework;
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

    internal static void Register(ModWall wall)
    {
        Once("Kill", () => On_WorldGen.KillWall += Kill);
        Once("Dust", () => On_WorldGen.KillWall_MakeWallDust += MakeDust);
        Once("Sound", () => On_WorldGen.KillWall_PlaySounds += Sound);
        Once("Drop", () => On_WorldGen.KillWall_DropItems += Drop);
        Once("Drops", () => On_WorldGen.KillWall_GetItemDrops += Drops);
        uint flags = 0;
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

    static void Kill(On_WorldGen.orig_KillWall orig, int i, int j, bool fail)
    {
        if (WallLoader.GetWall(Main.tile[i, j].wall) is not ModWall wall)
        {
            orig(i, j, fail);
            return;
        }

        wall.KillWall(i, j, ref fail);
        int count = fail ? 1 : 3;
        wall.NumDust(i, j, fail, ref count);
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
        if (WallLoader.GetWall(tile.wall) is not ModWall wall)
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

        int type = wall.DustType;
        if (!wall.CreateDust(i, j, ref type) || type < 0)
            return;
        Dust.NewDust(new Vector2(i * 16, j * 16), 16, 16, type, 0, 0, 0, default, 1);
    }

    static void Sound(On_WorldGen.orig_KillWall_PlaySounds orig, int i, int j, Tile tile)
    {
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
        if (WallLoader.GetWall(tile.wall) is not ModWall wall)
        {
            orig(i, j, tile);
            return;
        }

        int type = WallLoader.ItemDrop(wall);
        if (!wall.Drop(i, j, ref type))
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

    static int Drops(On_WorldGen.orig_KillWall_GetItemDrops orig, Tile tile)
    {
        if (WallLoader.GetWall(tile.wall) is not ModWall wall)
            return orig(tile);
        return dropping ? dropOverride : WallLoader.ItemDrop(wall);
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
