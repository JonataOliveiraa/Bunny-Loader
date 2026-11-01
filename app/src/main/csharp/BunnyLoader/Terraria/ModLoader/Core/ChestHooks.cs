using System;
using System.IO;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;

namespace Terraria.ModLoader.Core;

internal static class ChestHooks
{
    public const byte KindChest = 8;

    const int ChestMessage = 34;
    const byte Place = 0, Kill = 1, Placed = 2, Killed = 3;

    [ThreadStatic] static int pickX, pickY;
    [ThreadStatic] static bool picking, broke;

    internal static void Install(ModTile tile)
    {
        int type = tile.Type;
        bool chest = TileID.Sets.BasicChest[type], dresser = TileID.Sets.BasicDresser[type];
        if (chest || dresser)
        {
            ItemHooks.Once("Chests", () =>
            {
                ModNet.Install();
                On_NetMessage.SendData += SendData;
                On_Player.PickTile += PickTile;
                On_Player.ClearMiningCacheAt += ClearMiningCacheAt;
                On_GUIPageIcons.GetCategoryTexture += GetCategoryTexture;
            });
        }
        if (dresser)
        {
            ItemHooks.Once("Dressers", () =>
            {
                On_Player.IsInInteractionRangeToMultiTileHitbox += IsInDresserRange;
                On_GUIClothesWindow.Draw += DrawClothesWindow;
            });
        }
        Type t = tile.GetType();
        if (ItemHooks.Overrides(t, typeof(ModTile), nameof(ModTile.IsLockedChest)) || ItemHooks.Overrides(t, typeof(ModTile), nameof(ModTile.UnlockChest)) ||
            ItemHooks.Overrides(t, typeof(ModTile), nameof(ModTile.LockChest)))
        {
            ItemHooks.Once("ChestLocks", () =>
            {
                On_Chest.IsLocked_int_int += IsLocked;
                On_Chest.Unlock += Unlock;
                On_Chest.Lock += Lock;
            });
        }
    }

    static ModTile? At(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        return tile.active() ? TileLoader.GetTile(tile.type) : null;
    }

    static bool IsLocked(On_Chest.orig_IsLocked_int_int orig, int x, int y)
    {
        if (At(x, y) is not ModTile tile) return orig(x, y);
        try { return tile.IsLockedChest(x, y); }
        catch (Exception e)
        {
            ItemHooks.Report($"{tile.FullName}.IsLockedChest", e);
            return false;
        }
    }

    static bool Unlock(On_Chest.orig_Unlock orig, int X, int Y)
    {
        if (At(X, Y) is not ModTile tile) return orig(X, Y);
        short shift = -36;
        int dust = 11;
        bool manual = false;
        try
        {
            if (!tile.UnlockChest(X, Y, ref shift, ref dust, ref manual)) return false;
        }
        catch (Exception e)
        {
            ItemHooks.Report($"{tile.FullName}.UnlockChest", e);
            return false;
        }
        if (!manual) Shift(X, Y, tile.Type, shift, dust);
        return true;
    }

    static bool Lock(On_Chest.orig_Lock orig, int X, int Y)
    {
        if (At(X, Y) is not ModTile tile) return orig(X, Y);
        short shift = 36;
        bool manual = false;
        try
        {
            if (!tile.LockChest(X, Y, ref shift, ref manual)) return false;
        }
        catch (Exception e)
        {
            ItemHooks.Report($"{tile.FullName}.LockChest", e);
            return false;
        }
        if (!manual) Shift(X, Y, tile.Type, shift, -1);
        return true;
    }

    static void Shift(int x, int y, int type, short shift, int dust)
    {
        SoundEngine.PlaySound(SoundID.Unlock, x * 16, y * 16);
        for (int i = x; i <= x + 1; i++)
        {
            for (int j = y; j <= y + 1; j++)
            {
                Tile tile = Main.tile[i, j];
                if (!tile.active() || tile.type != type) continue;
                tile.frameX = (short)(tile.frameX + shift);
                if (dust < 0) continue;
                for (int k = 0; k < 4; k++) Dust.NewDust(new Vector2(i * 16, j * 16), 16, 16, dust);
            }
        }
    }

    static bool IsInDresserRange(On_Player.orig_IsInInteractionRangeToMultiTileHitbox orig, Player self, int chestPointX, int chestPointY)
    {
        if (At(chestPointX, chestPointY) is not ModTile tile || !TileID.Sets.BasicDresser[tile.Type]) return orig(self, chestPointX, chestPointY);
        float left = chestPointX * 16 + 1, top = chestPointY * 16 + 1;
        Vector2 center = self.Center;
        float px = Math.Clamp(center.X, left, left + 46), py = Math.Clamp(center.Y, top, top + 30);
        return self.IsInTileInteractionRange((int)(px / 16), (int)(py / 16), new TileReachCheckSettings { TileRangeMultiplier = 1 }, 0);
    }

    static void DrawClothesWindow(On_GUIClothesWindow.orig_Draw orig, GUIClothesWindow self)
    {
        int x = Main.interactedDresserTopLeftX, y = Main.interactedDresserTopLeftY;
        if (At(x, y) is not ModTile tile || !TileID.Sets.BasicDresser[tile.Type])
        {
            orig(self);
            return;
        }
        Tile corner = Main.tile[x, y];
        corner.type = TileID.Dressers;
        try { orig(self); }
        finally
        {
            Tile after = Main.tile[x, y];
            if (after.type == TileID.Dressers) after.type = (ushort)tile.Type;
        }
    }

    static Texture2D GetCategoryTexture(On_GUIPageIcons.orig_GetCategoryTexture orig, GUIPageIcons self, GUIPageIcons.Category category, bool selected)
    {
        try
        {
            int item = 0;
            if (category == GUIPageIcons.Category.Chest)
            {
                Player player = Main.LocalPlayer;
                if (player.chest >= 0) item = ItemOf(player.chestX, player.chestY);
            }
            else if (category == GUIPageIcons.Category.Dresser) item = ItemOf(Main.interactedDresserTopLeftX, Main.interactedDresserTopLeftY);
            if (item > 0) return TextureAssets.Item[item].Value;
        }
        catch (Exception e) { ItemHooks.Report("ModTile (chest page icon)", e); }
        return orig(self, category, selected);
    }

    static int ItemOf(int x, int y) => At(x, y) is ModTile tile ? TileLoader.DropType(tile.Type, TileLoader.StyleAt(x, y)) : 0;

    static int Kind(int type) =>
        TileLoader.GetTile(type) == null ? 0 : TileID.Sets.BasicDresser[type] ? 2 : TileID.Sets.BasicChest[type] ? 1 : 0;

    static void SendData(On_NetMessage.orig_SendData orig, int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3,
        float number4, int number5, int number6, int number7)
    {
        if (msgType == ChestMessage && Main.netMode == NetmodeID.MultiplayerClient && number is 0 or 2 or 4)
        {
            int x = (int)Math.Round(number2), y = (int)Math.Round(number3);
            Tile tile = Main.tile[x, y];
            if (tile.active() && Kind(tile.type) != 0)
            {
                Send(Place, x, y, tile.type, (int)Math.Round(number4), 0, -1, -1);
                return;
            }
        }
        orig(msgType, remoteClient, ignoreClient, text, number, number2, number3, number4, number5, number6, number7);
    }

    static void PickTile(On_Player.orig_PickTile orig, Player self, int x, int y, int pickPower, int dealDamageAsIfBaseNumberIs)
    {
        Tile tile = Main.tile[x, y];
        int type = tile.type;
        if (Main.netMode != NetmodeID.MultiplayerClient || !tile.active() || Kind(type) == 0)
        {
            orig(self, x, y, pickPower, dealDamageAsIfBaseNumberIs);
            return;
        }
        bool outer = picking;
        int outerX = pickX, outerY = pickY;
        picking = true;
        broke = false;
        pickX = x;
        pickY = y;
        try { orig(self, x, y, pickPower, dealDamageAsIfBaseNumberIs); }
        finally
        {
            picking = outer;
            pickX = outerX;
            pickY = outerY;
        }
        if (broke) Send(Kill, x, y, type, 0, 0, -1, -1);
    }

    static void ClearMiningCacheAt(On_Player.orig_ClearMiningCacheAt orig, Player self, int x, int y, int hitTileCacheType)
    {
        if (picking && x == pickX && y == pickY) broke = true;
        orig(self, x, y, hitTileCacheType);
    }

    static void Send(byte action, int x, int y, int type, int style, int id, int toClient, int ignoreClient)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(KindChest);
        writer.Write(action);
        writer.Write((short)x);
        writer.Write((short)y);
        writer.Write((ushort)type);
        writer.Write((short)style);
        writer.Write((short)id);
        writer.Flush();
        byte[] packet = stream.ToArray();
        ModNet.Send(packet, packet.Length, toClient, ignoreClient);
    }

    internal static void Receive(BinaryReader reader, int from)
    {
        byte action = reader.ReadByte();
        int x = reader.ReadInt16(), y = reader.ReadInt16(), type = reader.ReadUInt16(), style = reader.ReadInt16(), id = reader.ReadInt16();
        bool server = (Main.netMode & 2) != 0 && from < 256;
        if (server && action == Place) ServerPlace(x, y, type, style, from);
        else if (server && action == Kill) ServerKill(x, y, type);
        else if (Main.netMode == NetmodeID.MultiplayerClient && action == Placed) ClientPlaced(x, y, type, style, id);
        else if (Main.netMode == NetmodeID.MultiplayerClient && action == Killed) ClientKilled(x, y, id);
    }

    internal static void ServerPlace(int x, int y, int type, int style, int from)
    {
        if (Kind(type) == 0) return;
        int id = WorldGen.PlaceChest(x, y, (ushort)type, false, style);
        if (id == -1)
        {
            Send(Placed, x, y, type, style, -1, from, -1);
            int drop = TileLoader.DropType(type, style);
            if (drop > 0) Item.NewItem(x * 16, y * 16, 32, 32, drop);
            return;
        }
        Send(Placed, x, y, type, style, id, -1, -1);
    }

    internal static void ServerKill(int x, int y, int type)
    {
        int kind = Kind(type);
        Tile tile = Main.tile[x, y];
        if (kind == 0 || !tile.active() || tile.type != type) return;
        if (kind == 2) x -= tile.frameX % 54 / 18;
        else if (tile.frameX % 36 != 0) x--;
        if (tile.frameY % 36 != 0) y--;
        int chest = Chest.FindChest(x, y);
        WorldGen.KillTile(x, y, false, false, false);
        Tile after = Main.tile[x, y];
        if (after.active() && after.type == type) return;
        Send(Killed, x, y, type, 0, chest, -1, -1);
    }

    static void ClientPlaced(int x, int y, int type, int style, int id)
    {
        if (id == -1)
        {
            WorldGen.KillTile(x, y, false, false, false);
            return;
        }
        int kind = Kind(type);
        if (kind == 0) return;
        SoundEngine.PlaySound(SoundID.Dig, new Vector2(x * 16, y * 16));
        if (kind == 2) WorldGen.PlaceDresserDirect(x, y, (ushort)type, style, id);
        else WorldGen.PlaceChestDirect(x, y, (ushort)type, style, id);
    }

    static void ClientKilled(int x, int y, int id)
    {
        Chest.DestroyChestDirect(x, y, id);
        WorldGen.KillTile(x, y, false, false, false);
    }
}
