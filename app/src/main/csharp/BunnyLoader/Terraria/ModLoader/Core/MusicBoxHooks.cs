using System;
using BunnyLoader.Interop;
using Terraria.Audio;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static class MusicBoxHooks
{
    internal const uint TileFlag = 1u << 30;
    const int EmptyMusicBox = ItemID.MusicBox;

    static int[] cells = new int[96];

    internal static void Install(int tileType)
    {
        TileLoader.Mark(tileType, TileFlag);
        TileHooks.InstallMusicBoxes();
        ItemHooks.Once("MusicBoxRecord", () => On_Player.UpdateEquips += UpdateEquips);
        MusicHooks.Install();
    }

    internal static int Active()
    {
        if (MusicLoader.tileToMusic.Count == 0) return 0;
        var screen = Main.screenPosition;
        int x = (int)(screen.X / 16), y = (int)(screen.Y / 16);
        int width = (int)Math.Ceiling(Main.screenWidth / 16f) + 1, height = (int)Math.Ceiling(Main.screenHeight / 16f) + 1;
        int count = TileHooks.Scan(ref cells, x, y, width, height, TileFlag);
        for (int k = 0; k < count; k++)
        {
            Tile tile = Main.tile[cells[k * 3], cells[k * 3 + 1]];
            if (tile.frameX < 36) continue;
            int slot = MusicLoader.TileMusic(cells[k * 3 + 2], tile.frameY);
            if (slot > 0) return slot;
        }
        GameArray<Item> armor = Main.LocalPlayer.armor;
        for (int k = 3; k < armor.Length; k++)
        {
            if (k >= 10 && k < 13) continue;
            if (MusicLoader.itemToMusic.TryGetValue(armor[k].type, out int slot)) return slot;
        }
        return 0;
    }

    internal static void Toggle(int i, int j, bool wired)
    {
        Tile origin = Main.tile[i, j];
        int type = origin.type;
        int left = i - origin.frameX / 18 % 2, top = j - origin.frameY / 18 % 2;
        for (int x = left; x < left + 2; x++)
        {
            for (int y = top; y < top + 2; y++)
            {
                Tile cell = Main.tile[x, y];
                if (!cell.active() || cell.type != type) continue;
                cell.frameX = (short)(cell.frameX < 36 ? cell.frameX + 36 : cell.frameX - 36);
                if (wired) Wiring.SkipWire(x, y);
            }
        }
        if (Main.netMode != NetmodeID.SinglePlayer) NetMessage.SendTileSquare(-1, left, top, 2, 2);
    }

    static void UpdateEquips(On_Player.orig_UpdateEquips orig, Player self, int i)
    {
        orig(self, i);
        if (i != Main.myPlayer) return;
        try { Record(self); }
        catch (Exception e) { ItemHooks.Report("music box recording", e); }
    }

    static void Record(Player player)
    {
        int playing = MusicHooks.Playing;
        if (playing <= 0 || !MusicLoader.musicToItem.TryGetValue(playing, out int box)) return;
        GameArray<Item> armor = player.armor;
        for (int k = 3; k < armor.Length && k < 20; k++)
        {
            if (k >= 10 && k < 13) continue;
            Item item = armor[k];
            if (item.type != EmptyMusicBox || Main.rand.Next(540) != 0) continue;
            SoundEngine.PlaySound(SoundID.Item166, player.Center);
            item.SetDefaults(box, default);
        }
    }
}
