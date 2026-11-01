using System;
using System.IO;
using BunnyLoader;
using Terraria.GameContent.Personalities;

namespace Terraria.ModLoader.Core;

internal static class BiomeHooks
{
    public const byte KindBiomes = 20;

    internal static void Install() => ItemHooks.Once("Biomes", () =>
    {
        On_Player.UpdateBiomes += UpdateBiomes;
        On_NetMessage.SyncOnePlayer += SyncOnePlayer;
        On_WorldGen.SaveAndQuit += SaveAndQuit;
        On_WorldGen.clearWorld += ClearWorld;
    });

    internal static void InstallShopping() => ItemHooks.Once("BiomeShopping", () => On_MushroomBiome.IsInBiome += IsInBiome);

    static void UpdateBiomes(On_Player.orig_UpdateBiomes orig, Player self)
    {
        orig(self);
        if (BiomeLoader.Count > 0) BiomeLoader.UpdateBiomes(self);
        SceneEffectLoader.UpdateSceneEffect(self);
    }

    static void SaveAndQuit(On_WorldGen.orig_SaveAndQuit orig)
    {
        BiomeLoader.LeaveWorld();
        orig();
    }

    static void ClearWorld(On_WorldGen.orig_clearWorld orig)
    {
        orig();
        BiomeLoader.LeaveWorld();
    }

    static bool IsInBiome(On_MushroomBiome.orig_IsInBiome orig, MushroomBiome self, Player player)
    {
        ModBiome? biome = BiomeLoader.OfShopping(self.Pointer);
        if (biome == null) return orig(self, player);
        return !player.IsNull && BiomeLoader.InBiome(player, biome);
    }

    internal static void Changed(Player player)
    {
        if (ModNet.NetMode == 0) return;
        Send(player.whoAmI, -1, -1);
    }

    static void SyncOnePlayer(On_NetMessage.orig_SyncOnePlayer orig, int plr, int toWho, int fromWho)
    {
        orig(plr, toWho, fromWho);
        if ((ModNet.NetMode & 2) == 0 || BiomeLoader.Count == 0 || (uint)plr >= 255u) return;
        try
        {
            if (Main.player[plr].active) Send(plr, toWho, fromWho);
        }
        catch (Exception e) { ItemHooks.Report("ModBiome (sync)", e); }
    }

    internal static byte[] Write(int who)
    {
        bool[] flags = BiomeLoader.FlagsOf(who);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(KindBiomes);
        writer.Write((byte)who);
        int count = 0;
        foreach (bool on in flags)
            if (on) count++;
        writer.Write((short)count);
        for (int i = 0; i < flags.Length; i++)
            if (flags[i]) writer.Write(BiomeLoader.KeyOf(BiomeLoader.All[i]));
        writer.Flush();
        return stream.ToArray();
    }

    static void Send(int who, int toClient, int ignoreClient)
    {
        byte[] packet = Write(who);
        ModNet.Send(packet, packet.Length, toClient, ignoreClient);
    }

    internal static void Receive(BinaryReader reader, int from)
    {
        int who = reader.ReadByte();
        int count = reader.ReadInt16();
        if (who >= 255) return;
        bool client = ModNet.NetMode == 1;
        if (client ? who == Main.myPlayer : who != from) return;
        bool[] flags = BiomeLoader.FlagsOf(who);
        Array.Clear(flags);
        for (int i = 0; i < count; i++)
        {
            ModBiome? biome = BiomeLoader.ByKey(reader.ReadString());
            if (biome != null) flags[biome.Type] = true;
        }
        if (client) return;
        byte[] packet = Write(who);
        ModNet.Send(packet, packet.Length, -1, from);
    }
}
