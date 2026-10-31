using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;

namespace CsAuto;

public sealed class CsAutoMod : Mod
{
    const float ClientOffset = 12 * 16f;

    static string last = "";
    static bool moved, cleaned;

    public override void Load()
    {
        On_Main.DoUpdate += DoUpdate;
        On_Player.DropTombstone += (orig, self, coinsOwned, deathText, hitDirection) => { };
    }

    static void DoUpdate(On_Main.orig_DoUpdate orig, Main self, GameTime gameTime)
    {
        orig(self, gameTime);
        try
        {
            if (Main.gameMenu) moved = cleaned = false;
            else if (!cleaned && Main.netMode != 1) ClearTombstones();
            else if (!moved && Main.netMode == 1) MoveClient();
            string state = Main.gameMenu
                ? $"menu mode={Main.menuMode} mp={Main.menuMultiplayer} server={Main.menuServer} net={Main.netMode}"
                : $"world net={Main.netMode} players={Players()} paused={Main.gamePaused}";
            if (state == last) return;
            last = state;
            ModContent.GetInstance<CsAutoMod>().Logger.Info($"csauto state {state}");
        }
        catch (Exception e) { ModContent.GetInstance<CsAutoMod>().Logger.Info($"csauto threw {e.Message}"); }
    }

    static void MoveClient()
    {
        Player me = Main.LocalPlayer;
        if (me.IsNull || !me.active || me.velocity.Y != 0f) return;
        moved = true;
        me.position = new Vector2(me.position.X + ClientOffset, me.position.Y);
        NetMessage.SendData(13, number: Main.myPlayer);
        ModContent.GetInstance<CsAutoMod>().Logger.Info($"csauto moved the client {ClientOffset / 16f} tiles right");
    }

    static void ClearTombstones()
    {
        cleaned = true;
        TileData tiles = Main.tile;
        int bottom = Math.Min(Main.maxTilesY - 1, (int)Main.worldSurface + 100), removed = 0;
        for (int x = 1; x < Main.maxTilesX - 1; x++)
            for (int y = 1; y < bottom; y++)
            {
                Tile tile = tiles[x, y];
                if (!tile.HasTile || tile.TileType != TileID.Tombstones) continue;
                WorldGen.KillTile(x, y, noItem: true);
                removed++;
            }
        if (removed == 0) return;
        ModContent.GetInstance<CsAutoMod>().Logger.Info($"csauto removed {removed} tombstone tile(s)");
        if (Main.netMode == 0) WorldFile.SaveWorld();
    }

    static int Players()
    {
        int count = 0;
        for (int i = 0; i < 255; i++)
            if (Main.player[i].active) count++;
        return count;
    }
}
