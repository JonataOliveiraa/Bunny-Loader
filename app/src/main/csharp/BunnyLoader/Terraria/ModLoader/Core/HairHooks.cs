using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.IO;

namespace Terraria.ModLoader.Core;

internal static class HairHooks
{
    public const byte KindHair = 4;

    const string SaveKey = "bunny:hair";

    static readonly Dictionary<int, bool> Unlocked = new();

    internal static void Install() => ItemHooks.Once("Hairs", () =>
    {
        On_HairstyleUnlocksHelper.ListWarrantsRemake += ListWarrantsRemake;
        On_HairstyleUnlocksHelper.RebuildList += RebuildList;
        On_Player.GetHairSettings += GetHairSettings;
        On_Player.InternalSavePlayerFile += InternalSavePlayerFile;
        On_Player.LoadPlayer += LoadPlayer;
        ItemNetHooks.WantSendData();
        ItemNetHooks.AfterSend += AfterSend;
    });

    static string KeyOf(ModHair hair) => $"{hair.Mod.Uid}/{hair.Name}";

    static bool Available(ModHair hair, bool creation)
    {
        try { return creation ? hair.AvailableDuringCharacterCreation : hair.GetUnlockConditions().All(c => c.IsMet()); }
        catch (Exception e)
        {
            ItemHooks.Report($"{hair.FullName}.GetUnlockConditions", e);
            return false;
        }
    }

    static bool ListWarrantsRemake(On_HairstyleUnlocksHelper.orig_ListWarrantsRemake orig, HairstyleUnlocksHelper self)
    {
        bool remake = orig(self);
        bool creation = self._isAtCharacterCreation;
        foreach (ModHair hair in HairLoader.hairs)
        {
            bool now = Available(hair, creation);
            if (!Unlocked.TryGetValue(hair.Type, out bool before) || before != now) remake = true;
            Unlocked[hair.Type] = now;
        }
        return remake;
    }

    static void RebuildList(On_HairstyleUnlocksHelper.orig_RebuildList orig, HairstyleUnlocksHelper self)
    {
        orig(self);
        var list = new GameList(self.AvailableHairstyles);
        if (list.IsNull) return;
        bool creation = self._isAtCharacterCreation;
        foreach (ModHair hair in HairLoader.hairs)
        {
            bool available = Unlocked.TryGetValue(hair.Type, out bool known) ? known : Available(hair, creation);
            if (available && !list.Contains(hair.Type)) list.Add(hair.Type);
        }
    }

    static void GetHairSettings(On_Player.orig_GetHairSettings orig, Player self, out bool fullHair, out bool hatHair, out bool hideHair, out bool backHairDraw, out bool drawsBackHairWithoutHeadgear)
    {
        orig(self, out fullHair, out hatHair, out hideHair, out backHairDraw, out drawsBackHairWithoutHeadgear);
        int hair = self.hair;
        bool[] set = HairID.Sets.DrawBackHair;
        if (hair >= 0 && hair < set.Length) backHairDraw = set[hair];
    }

    static void InternalSavePlayerFile(On_Player.orig_InternalSavePlayerFile orig, PlayerFileData playerFile)
    {
        Player player = playerFile.IsNull ? default : playerFile.Player;
        ModHair? hair = player.IsNull ? null : HairLoader.GetHair(player.hair);
        if (hair != null) player.hair = 0;
        try { orig(playerFile); }
        finally
        {
            if (hair != null) player.hair = hair.Type;
        }
        try
        {
            if (player.IsNull || PlayerHooks.DataFile(playerFile) is not string file) return;
            JsonObject all = PlayerHooks.Read(file);
            if (hair != null) all[SaveKey] = KeyOf(hair);
            else if (all[SaveKey] is JsonValue saved && saved.TryGetValue(out string? key) && HairLoader.hairs.Any(h => KeyOf(h) == key)) all.Remove(SaveKey);
            else return;
            if (all.Count > 0) File.WriteAllText(file, all.ToJsonString());
            else File.Delete(file);
        }
        catch (Exception e) { ItemHooks.Report("ModHair save", e); }
    }

    static PlayerFileData LoadPlayer(On_Player.orig_LoadPlayer orig, string? playerPath, bool cloudSave)
    {
        PlayerFileData data = orig(playerPath, cloudSave);
        try
        {
            if (data.IsNull || PlayerHooks.DataFile(data) is not string file) return data;
            Player player = data.Player;
            if (player.IsNull || PlayerHooks.Read(file)[SaveKey] is not JsonValue saved || !saved.TryGetValue(out string? key)) return data;
            ModHair? hair = HairLoader.hairs.FirstOrDefault(h => KeyOf(h) == key);
            if (hair != null) player.hair = hair.Type;
        }
        catch (Exception e) { ItemHooks.Report("ModHair load", e); }
        return data;
    }

    static byte[] Packet(int player, int hair)
    {
        var data = new byte[6];
        data[0] = KindHair;
        data[1] = (byte)player;
        BitConverter.TryWriteBytes(data.AsSpan(2), hair);
        return data;
    }

    static void AfterSend(int msgType, int remote, int ignore, int number)
    {
        if (msgType != 4 || ModNet.NetMode == 0 || number < 0 || number > 254) return;
        Player player = Main.player[number];
        if (player.IsNull || HairLoader.GetHair(player.hair) is not ModHair hair) return;
        byte[] packet = Packet(number, hair.Type);
        ModNet.Send(packet, packet.Length, remote, ignore);
    }

    internal static void Receive(BinaryReader reader, int from)
    {
        int index = reader.ReadByte();
        int hair = reader.ReadInt32();
        bool server = (ModNet.NetMode & 2) != 0;
        if (server) index = from;
        if (index < 0 || index > 254 || HairLoader.GetHair(hair) == null) return;
        Main.player[index].hair = hair;
        if (!server) return;
        byte[] packet = Packet(index, hair);
        ModNet.Send(packet, packet.Length, -1, from);
    }
}
