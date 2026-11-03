using System;
using System.Collections.Generic;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.NetModules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader.Default;
using Terraria.Net;
using Terraria.ObjectData;
using Terraria.UI;

namespace Terraria.ModLoader.Core;

internal static class PylonHooks
{
    const int FirstPylonType = 11;

    static readonly GameClass ObjectClass = new("System", "Object");

    static readonly List<ModPylon> pylons = new();

    static List<(int X, int Y, int Type)> current = new();

    static List<(int X, int Y, int Type)>? stripped;

    static int requests, npcChecks;

    internal static IReadOnlyList<ModPylon> Pylons => pylons;

    internal static ModPylon? OfPylonType(int type) => type >= FirstPylonType && type - FirstPylonType < pylons.Count ? pylons[type - FirstPylonType] : null;

    internal static void Add(ModPylon pylon)
    {
        pylon.PylonType = (TeleportPylonType)(FirstPylonType + pylons.Count);
        pylons.Add(pylon);
        ItemHooks.Once("Pylons", Install);
    }

    internal static void Changed()
    {
        if (Main.netMode == 1 || Main.gameMenu || Main.PylonSystem.IsNull)
            return;
        Main.PylonSystem.RequestImmediateUpdate();
    }

    internal static bool EnoughNPCs(int count, Point16 center)
    {
        npcChecks++;
        try
        {
            return TeleportPylonsSystem.DoesPositionHaveEnoughNPCs(count, center);
        }
        finally
        {
            npcChecks--;
        }
    }

    internal static void AddShopEntries(NPCShop shop)
    {
        if (NPCShopDatabase.NoPylons.Contains(shop.FullName))
            return;
        foreach (ModPylon pylon in pylons)
        {
            try
            {
                if (pylon.GetNPCShopEntry() is NPCShop.Entry entry)
                    shop.Add(entry);
            }
            catch (Exception e)
            {
                ItemHooks.Report($"{pylon.FullName}.GetNPCShopEntry", e);
            }
        }
    }

    static void Install()
    {
        On_TeleportPylonsSystem.UpdatePylonsListAndBroadcastChanges += UpdateList;
        On_TeleportPylonsSystem.IsPlayerNearAPylon += NearAPylon;
        On_TeleportPylonsSystem.HandleTeleportRequest += HandleRequest;
        On_TeleportPylonsSystem.DoesPositionHaveEnoughNPCs += PositionHasEnoughNPCs;
        On_TeleportPylonsMapLayer.Draw += DrawMap;
        On_TileObject.CanPlace += CanPlace;
    }

    static GameList List(TeleportPylonsSystem system) => new(system._pylons);

    static TeleportPylonInfo Info((int X, int Y, int Type) entry) =>
        new() { PositionInTiles = new Point16(entry.X, entry.Y), TypeOfPylon = (TeleportPylonType)entry.Type };

    static List<(int X, int Y, int Type)> Current()
    {
        var found = new List<(int, int, int)>();
        foreach (ModTileEntity entity in TileEntityHooks.ByID.Values)
            if (entity is TEModdedPylon pylon && pylon.TryGetModPylon(out ModPylon modPylon))
                found.Add((entity.Position.X, entity.Position.Y, (int)modPylon.PylonType));
        return found;
    }

    static List<(int X, int Y, int Type)> Strip(GameList list)
    {
        var removed = new List<(int, int, int)>();
        for (int k = list.Count - 1; k >= 0; k--)
        {
            TeleportPylonInfo info = list.Get<TeleportPylonInfo>(k);
            if ((int)info.TypeOfPylon < FirstPylonType)
                continue;
            removed.Add((info.PositionInTiles.X, info.PositionInTiles.Y, (int)info.TypeOfPylon));
            list.RemoveAt(k);
        }

        return removed;
    }

    static void Send((int X, int Y, int Type) entry, NetTeleportPylonModule.SubPacketType kind) =>
        NetManager.Instance.BroadcastOrLoopback(NetTeleportPylonModule.SerializePylonWasAddedOrRemoved(Info(entry), kind));

    static void UpdateList(On_TeleportPylonsSystem.orig_UpdatePylonsListAndBroadcastChanges orig, TeleportPylonsSystem system)
    {
        Strip(List(system));
        orig(system);
        try
        {
            GameList list = List(system);
            List<(int X, int Y, int Type)> now = Current(), before = current;
            foreach (var entry in now)
                list.Add(Info(entry));
            foreach (var entry in now)
                if (!before.Contains(entry))
                    Send(entry, NetTeleportPylonModule.SubPacketType.PylonWasAdded);
            foreach (var entry in before)
                if (!now.Contains(entry))
                    Send(entry, NetTeleportPylonModule.SubPacketType.PylonWasRemoved);
            current = now;
        }
        catch (Exception e)
        {
            ItemHooks.Report("ModPylon (pylon list)", e);
        }
    }

    static bool NearAPylon(On_TeleportPylonsSystem.orig_IsPlayerNearAPylon orig, Player player)
    {
        if (orig(player))
            return true;
        TileReachCheckSettings reach = TileReachCheckSettings.Pylons;
        foreach (int type in TileID.Sets.CountsAsPylon)
            if (type != TileID.TeleportationPylon && player.IsTileTypeInInteractionRange(type, reach))
                return true;
        return false;
    }

    static bool AcceptsBiome(TeleportPylonsSystem system, ModPylon pylon, TeleportPylonInfo info)
    {
        SceneMetrics metrics = system._sceneMetrics;
        var settings = new SceneMetricsScanSettings { BiomeScanCenterPositionInWorld = info.PositionInTiles.ToWorldCoordinates() };
        metrics.Scan(settings);
        return pylon.ValidTeleportCheck_BiomeRequirements(info, metrics);
    }

    static void HandleRequest(On_TeleportPylonsSystem.orig_HandleTeleportRequest orig, TeleportPylonsSystem system, TeleportPylonInfo info, int playerIndex)
    {
        ModPylon? pylon = OfPylonType((int)info.TypeOfPylon);
        try
        {
            if (pylon != null && !AcceptsBiome(system, pylon, info))
            {
                ChatHelper.SendChatMessageToClient(NetworkText.FromKey("Net.CannotTeleportToPylonBecauseNotMeetingBiomeRequirements", GameArray<GameObject>.New(ObjectClass, 0)), new Color(255, 240, 20), playerIndex);
                return;
            }
        }
        catch (Exception e)
        {
            ItemHooks.Report($"{pylon!.FullName}.ValidTeleportCheck_BiomeRequirements", e);
            return;
        }

        var back = stripped;
        if (back != null)
        {
            GameList list = List(system);
            foreach (var entry in back)
                list.Add(Info(entry));
        }

        requests++;
        try
        {
            orig(system, info, playerIndex);
        }
        finally
        {
            requests--;
            if (back != null)
                Strip(List(system));
        }
    }

    static bool PositionHasEnoughNPCs(On_TeleportPylonsSystem.orig_DoesPositionHaveEnoughNPCs orig, int count, Point16 center)
    {
        if (requests == 0 || npcChecks > 0)
            return orig(count, center);
        foreach (var entry in current)
        {
            if (entry.X != center.X || entry.Y != center.Y || OfPylonType(entry.Type) is not ModPylon pylon)
                continue;
            try
            {
                return pylon.ValidTeleportCheck_NPCCount(Info(entry), count);
            }
            catch (Exception e)
            {
                ItemHooks.Report($"{pylon.FullName}.ValidTeleportCheck_NPCCount", e);
                return false;
            }
        }

        return orig(count, center);
    }

    static bool CanPlace(On_TileObject.orig_CanPlace orig, int x, int y, int type, int style, int dir, out TileObject objectData, bool onlyCheck, GameNullable<int> forcedRandom)
    {
        if (TileLoader.GetTile(type) is ModPylon pylon)
        {
            bool allowed;
            try
            {
                allowed = pylon.CanPlacePylon();
            }
            catch (Exception e)
            {
                ItemHooks.Report($"{pylon.FullName}.CanPlacePylon", e);
                allowed = false;
            }

            if (!allowed)
            {
                objectData = default;
                return false;
            }
        }

        return orig(x, y, type, style, dir, out objectData, onlyCheck, forcedRandom);
    }

    static void DrawMap(On_TeleportPylonsMapLayer.orig_Draw orig, TeleportPylonsMapLayer layer, ref MapOverlayDrawContext context, ref GameString text)
    {
        GameList list = List(Main.PylonSystem);
        var mine = Strip(list);
        stripped = mine.Count > 0 ? mine : null;
        try
        {
            if (mine.Count > 0)
                DrawLines(mine, ref context);
            orig(layer, ref context, ref text);
        }
        catch (Exception e)
        {
            ItemHooks.Report("ModPylon (map lines)", e);
        }
        finally
        {
            stripped = null;
            foreach (var entry in mine)
                list.Add(Info(entry));
        }

        if (mine.Count == 0)
            return;
        bool near = TeleportPylonsSystem.IsPlayerNearAPylon(Main.LocalPlayer);
        Color color = near ? new Color(255, 255, 255, 255) : new Color(64, 64, 64, 127);
        string mouseOver = text;
        string before = mouseOver;
        foreach (var entry in mine)
        {
            TeleportPylonInfo info = Info(entry);
            if (!TeleportPylonsMapLayer.IsRevealed(info) || OfPylonType(entry.Type) is not ModPylon pylon)
                continue;
            try
            {
                pylon.DrawMapIcon(ref context, ref mouseOver, info, near, color, 1f, 2f);
            }
            catch (Exception e)
            {
                ItemHooks.Report($"{pylon.FullName}.DrawMapIcon", e);
            }
        }

        if (!string.Equals(mouseOver, before, StringComparison.Ordinal))
            text = mouseOver;
    }

    static void DrawLines(List<(int X, int Y, int Type)> entries, ref MapOverlayDrawContext context)
    {
        Point16 from = Main.MapPylonTile;
        if (!Main.mapFullscreen || from.X == -1 || from.Y == -1)
            return;
        Texture2D texture = TextureAssets.Extra[182].Value;
        var frame = new SpriteFrame(11, 1, 0, 0) { PaddingY = 0 };
        Vector2 start = Center(ref context, texture, frame, from.X, from.Y);
        foreach (var entry in entries)
        {
            if ((entry.X == from.X && entry.Y == from.Y) || !TeleportPylonsMapLayer.IsRevealed(Info(entry)))
                continue;
            Vector2 end = Center(ref context, texture, frame, entry.X, entry.Y);
            TeleportPylonsMapLayer.DrawLine(Main.spriteBatch, start, end, new Color(0, 0, 0, 255), 6);
            TeleportPylonsMapLayer.DrawLine(Main.spriteBatch, start, end, new Color(255, 255, 255, 255), 2);
        }
    }

    static Vector2 Center(ref MapOverlayDrawContext context, Texture2D texture, SpriteFrame frame, int x, int y)
    {
        Rectangle region = context.GetUnclampedDrawRegion(texture, new Vector2(x + 1.5f, y + 2f), frame, 1f, Alignment.Center);
        return new Vector2(region.X + region.Width / 2, region.Y + region.Height / 2);
    }

    internal static bool OpenMap(int i, int j)
    {
        Main.LocalPlayer.TryOpeningFullscreenMap();
        if (!Main.mapFullscreen)
            return false;
        Point16 origin = TileObjectData.TopLeft(i, j);
        int left = origin.X, top = origin.Y, right = origin.X, bottom = origin.Y;
        GameList list = List(Main.PylonSystem);
        for (int k = 0; k < list.Count; k++)
        {
            TeleportPylonInfo info = list.Get<TeleportPylonInfo>(k);
            if (!TeleportPylonsMapLayer.IsRevealed(info))
                continue;
            left = Math.Min(left, info.PositionInTiles.X);
            right = Math.Max(right, info.PositionInTiles.X);
            top = Math.Min(top, info.PositionInTiles.Y);
            bottom = Math.Max(bottom, info.PositionInTiles.Y);
        }

        int x = left - 20, y = top - 20, width = right - left + 40, height = bottom - top + 40;
        XNAUICameraSettings camera = XNAUnityRunner._instance.UICameraSettings;
        float scale = Math.Min(Main.mapFullscreenScale, Math.Min((camera._cameraWidth - 40f) / width, (camera._cameraHeight - 40f) / height));
        Main.MapPylonTile = origin;
        Main.PanTargetMapFullscreen = false;
        Main.resetMapFull = false;
        Main.mapFullscreenPos = new Vector2(x + width / 2, y + height / 2);
        Main.mapFullscreenScale = scale;
        return true;
    }
}
