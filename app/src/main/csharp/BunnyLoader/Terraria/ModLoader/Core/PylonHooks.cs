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
    internal const int FirstPylonType = 11;

    const int PylonSpan = 4;

    static readonly GameClass ObjectClass = new("System", "Object");

    static readonly List<ModPylon> pylons = new();

    static List<(int X, int Y, int Type)> current = new();

    static List<(int X, int Y, int Type)>? stripped;

    static int requests, npcChecks;

    internal static IReadOnlyList<ModPylon> Pylons => pylons;

    internal static readonly List<GlobalPylon> Globals = new();

    internal static void Add(GlobalPylon pylon)
    {
        Globals.Add(pylon);
        ItemHooks.Once("Pylons", Install);
    }

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
        bool? modNear = null;
        foreach (int type in TileID.Sets.CountsAsPylon)
        {
            if (type == TileID.TeleportationPylon)
                continue;
            if (TileLoader.GetTile(type) is ModPylon)
            {
                modNear ??= ModPylonInRegion(player, reach);
                if (!modNear.Value)
                    continue;
            }
            if (player.IsTileTypeInInteractionRange(type, reach))
                return true;
        }
        return false;
    }

    static bool ModPylonInRegion(Player player, TileReachCheckSettings reach)
    {
        bool any = false;
        foreach (ModTileEntity entity in TileEntityHooks.ByID.Values)
            if (entity is TEModdedPylon)
            {
                any = true;
                break;
            }
        if (!any)
            return false;
        reach.GetTileRegion(player, out int lx, out int ly, out int hx, out int hy);
        foreach (ModTileEntity entity in TileEntityHooks.ByID.Values)
            if (entity is TEModdedPylon && entity.Position.X <= hx && entity.Position.X + PylonSpan > lx && entity.Position.Y <= hy && entity.Position.Y + PylonSpan > ly)
                return true;
        return false;
    }

    static bool EnoughNPCsAround(TeleportPylonsSystem system, TeleportPylonInfo info, Player player)
    {
        int count = system.HowManyNPCsDoesPylonNeed(info, player);
        if (PylonLoader.ValidTeleportCheck_PreNPCCount(info, ref count) is bool value)
            return value;
        if (OfPylonType((int)info.TypeOfPylon) is ModPylon pylon)
            return pylon.ValidTeleportCheck_NPCCount(info, count);
        return count <= 0 || EnoughNPCs(count, info.PositionInTiles);
    }

    static bool AcceptsTeleport(TeleportPylonsSystem system, TeleportPylonInfo info, Player player)
    {
        SceneMetrics metrics = system._sceneMetrics;
        metrics.Scan(new SceneMetricsScanSettings { BiomeScanCenterPositionInWorld = info.PositionInTiles.ToWorldCoordinates() });
        if (PylonLoader.ValidTeleportCheck_PreBiomeRequirements(info, metrics) is bool value)
            return value;
        if (OfPylonType((int)info.TypeOfPylon) is ModPylon pylon)
            return pylon.ValidTeleportCheck_BiomeRequirements(info, metrics);
        return system.DoesPylonAcceptTeleportation(info, player);
    }

    static void Port(TeleportPylonsSystem system, TeleportPylonInfo info, int playerIndex)
    {
        Player player = Main.player[playerIndex];
        string? key = null;
        bool valid = TeleportPylonsSystem.IsPlayerNearAPylon(player);
        if (!valid)
            key = "Net.CannotTeleportToPylonBecausePlayerIsNotNearAPylon";
        if (valid)
        {
            valid = EnoughNPCsAround(system, info, player);
            if (!valid)
                key = "Net.CannotTeleportToPylonBecauseNotEnoughNPCs";
        }
        if (valid)
        {
            if (!NPC.downedPlantBoss && info.PositionInTiles.Y > Main.worldSurface && Framing.GetTileSafely(info.PositionInTiles.X, info.PositionInTiles.Y).wall == WallID.LihzahrdBrickUnsafe)
                valid = false;
            if (!valid)
                key = "Net.CannotTeleportToPylonBecauseAccessingLihzahrdTempleEarly";
        }
        if (valid)
        {
            valid = AcceptsTeleport(system, info, player);
            if (!valid)
                key = "Net.CannotTeleportToPylonBecauseNotMeetingBiomeRequirements";
        }

        if (OfPylonType((int)info.TypeOfPylon) is ModPylon destination)
            destination.ValidTeleportCheck_DestinationPostCheck(info, ref valid, ref key!);

        TeleportPylonInfo nearby = default;
        bool nearbyValid = false;
        int progress = 0;
        GameList list = List(system);
        for (int k = 0; k < list.Count; k++)
        {
            TeleportPylonInfo other = list.Get<TeleportPylonInfo>(k);
            if (!player.InTileEntityInteractionRange(other.PositionInTiles.X, other.PositionInTiles.Y, 3, 4, TileReachCheckSettings.Pylons))
                continue;
            nearby = other;
            if (progress < 1)
                progress = 1;
            if (!EnoughNPCsAround(system, other, player))
                continue;
            if (progress < 2)
                progress = 2;
            if (AcceptsTeleport(system, other, player))
            {
                nearbyValid = true;
                break;
            }
        }
        if (!nearbyValid)
            key = progress switch
            {
                1 => "Net.CannotTeleportToPylonBecauseNotEnoughNPCsAtCurrentPylon",
                2 => "Net.CannotTeleportToPylonBecauseNotMeetingBiomeRequirements",
                _ => "Net.CannotTeleportToPylonBecausePlayerIsNotNearAPylon",
            };

        if (OfPylonType((int)nearby.TypeOfPylon) is ModPylon nearbyPylon)
            nearbyPylon.ValidTeleportCheck_NearbyPostCheck(nearby, ref valid, ref nearbyValid, ref key!);
        PylonLoader.PostValidTeleportCheck(info, nearby, ref valid, ref nearbyValid, ref key!);

        if (valid && nearbyValid)
        {
            Vector2 target = info.PositionInTiles.ToWorldCoordinates() - new Vector2(0f, player.HeightOffsetBoost);
            if (OfPylonType((int)info.TypeOfPylon) is ModPylon final)
                final.ModifyTeleportationPosition(info, ref target);
            int style = 9, pylonType = (int)info.TypeOfPylon;
            player.Teleport(target, style, pylonType);
            player.velocity = Vector2.Zero;
            if ((Main.netMode & NetmodeID.Server) == 0)
                return;
            RemoteClient.CheckSection(player.whoAmI, player.position);
            NetMessage.SendData(MessageID.TeleportEntity, -1, -1, default, 0, player.whoAmI, target.X, target.Y, style, 0, pylonType);
        }
        else
        {
            ChatHelper.SendChatMessageToClient(NetworkText.FromKey(key ?? "Net.CannotTeleportToPylonBecausePlayerIsNotNearAPylon", GameArray<GameObject>.New(ObjectClass, 0)), new Color(255, 240, 20), playerIndex);
        }
    }

    static void HandleRequest(On_TeleportPylonsSystem.orig_HandleTeleportRequest orig, TeleportPylonsSystem system, TeleportPylonInfo info, int playerIndex)
    {
        var restored = stripped;
        if (restored != null)
        {
            GameList strippedList = List(system);
            foreach (var entry in restored)
                strippedList.Add(Info(entry));
        }

        requests++;
        try
        {
            Port(system, info, playerIndex);
        }
        catch (Exception e)
        {
            ItemHooks.Report("TeleportPylonsSystem.HandleTeleportRequest", e);
        }
        finally
        {
            requests--;
            if (restored != null)
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

    static TeleportPylonType? VanillaPylonOf(int type, int style) =>
        type == TileID.TeleportationPylon ? (TeleportPylonType)style : null;

    static bool CanPlace(On_TileObject.orig_CanPlace orig, int x, int y, int type, int style, int dir, out TileObject objectData, bool onlyCheck, GameNullable<int> forcedRandom)
    {
        ModPylon? modPylon = TileLoader.GetTile(type) as ModPylon;
        TeleportPylonType? pylonType = modPylon?.PylonType ?? VanillaPylonOf(type, style);
        if (pylonType is TeleportPylonType placing && Globals.Count > 0)
        {
            bool? decision = PylonLoader.PreCanPlacePylon(x, y, type, placing);
            if (decision == false)
            {
                objectData = default;
                return false;
            }

            if (decision == true)
                return orig(x, y, type, style, dir, out objectData, onlyCheck, forcedRandom);
        }

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
                bool nearNow = near;
                Color iconColor = color;
                float deselected = 1f, selected = 2f;
                bool draw = true;
                foreach (GlobalPylon global in Globals)
                    draw &= global.PreDrawMapIcon(ref context, ref mouseOver, ref info, ref nearNow, ref iconColor, ref deselected, ref selected);
                if (draw)
                    pylon.DrawMapIcon(ref context, ref mouseOver, info, nearNow, iconColor, deselected, selected);
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
