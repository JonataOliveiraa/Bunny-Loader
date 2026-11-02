using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using BunnyLoader;
using Terraria.Localization;
using Terraria.ModLoader.Config;

namespace Terraria.ModLoader.Core;

internal static class ConfigNetHooks
{
    public const byte KindConfig = 10;

    const byte Sync = 0, Ask = 1, Reply = 2;
    const int WorldData = 7;

    static readonly Dictionary<ModConfig, JsonObject> synced = new();

    internal static void Install()
    {
        ItemHooks.Once("ConfigNet", () =>
        {
            ItemNetHooks.WantSendData();
            ItemNetHooks.SendingData += Sending;
            On_WorldGen.SaveAndQuit += SaveAndQuit;
            On_Player_Hooks.EnterWorld += EnterWorld;
        });
    }

    static IEnumerable<ModConfig> ServerConfigs => ConfigManager.All.Where(c => c.Mode == ConfigScope.ServerSide);

    static void Sending(int msgType, int remote, int ignore, int number)
    {
        if (msgType != WorldData || (ModNet.NetMode & 2) == 0 || remote < 0 || remote >= 255)
            return;
        ModConfig[] configs = ServerConfigs.ToArray();
        if (configs.Length == 0)
            return;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(KindConfig);
        writer.Write(Sync);
        writer.Write(configs.Length);
        foreach (ModConfig config in configs)
        {
            writer.Write(config.Mod.Name);
            writer.Write(config.Name);
            writer.Write(ConfigManager.ToJson(config).ToJsonString());
        }

        Send(stream, remote, -1);
    }

    internal static void Request(ModConfig pending, bool broadcast)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(KindConfig);
        writer.Write(Ask);
        writer.Write(pending.Mod.Name);
        writer.Write(pending.Name);
        writer.Write(broadcast);
        writer.Write(ConfigManager.ToJson(pending).ToJsonString());

        Send(stream, -1, -1);
    }

    internal static void Broadcast(ModConfig active, bool broadcast)
    {
        NetworkText message = NetworkText.FromKey("tModLoader.ModConfigAccepted");
        int requestor = (ModNet.NetMode & 1) != 0 ? Main.myPlayer : 255;
        SendReply(true, message, active, broadcast, requestor, -1);
        if ((ModNet.NetMode & 1) != 0)
            Replied(active, true, broadcast, requestor, message);
    }

    internal static void Revert(ModConfig active)
    {
        if (synced.TryGetValue(active, out JsonObject? data))
            ConfigManager.Populate(active, data);
        else
            ConfigManager.Load(active);
    }

    internal static void RestoreLocal()
    {
        if (synced.Count == 0)
            return;
        ModConfig[] configs = synced.Keys.ToArray();
        synced.Clear();
        foreach (ModConfig config in configs)
            ConfigManager.Load(config);
        foreach (ModConfig config in configs)
            ConfigManager.Changed(config);
    }

    static void SaveAndQuit(On_WorldGen.orig_SaveAndQuit orig)
    {
        orig();
        RestoreLocal();
    }

    static void EnterWorld(On_Player_Hooks.orig_EnterWorld orig, int playerIndex)
    {
        if (ModNet.NetMode != 1)
            RestoreLocal();
        orig(playerIndex);
    }

    internal static void Receive(BinaryReader reader, int from)
    {
        byte message = reader.ReadByte();
        bool server = (ModNet.NetMode & 2) != 0;
        if (message == Ask && server && from >= 0 && from < 255)
            Asked(reader, from);
        else if (message == Sync && !server && from == 256)
            Synced(reader);
        else if (message == Reply && !server && from == 256)
            ReadReply(reader);
    }

    static void Synced(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        var changed = new List<ModConfig>();
        var restart = new List<string>();
        for (int i = 0; i < count; i++)
        {
            string modName = reader.ReadString(), configName = reader.ReadString(), json = reader.ReadString();
            ModConfig? active = Find(modName, configName);
            if (active == null || JsonNode.Parse(json) is not JsonObject data)
                continue;
            ModConfig pending = active.Clone();
                ConfigManager.Populate(pending, data);
            ModConfig? loadTime = ConfigManager.GetLoadTimeConfig(active.Mod, active.Name);
            if (loadTime != null && loadTime.NeedsReload(pending))
            {
                restart.Add(active.DisplayName.Value ?? active.Name);
                Log.Warn($"{active.FullName}: the server uses other values in [ReloadRequired] options; they need a restart to fully apply");
            }

            ConfigManager.CopyValues(pending, active);
            synced[active] = data;
            changed.Add(active);
        }

        foreach (ModConfig config in changed)
            ConfigManager.Changed(config);
        if (restart.Count > 0)
            Main.NewText(Language.GetTextValue("BunnyLoader.ModConfigServerNeedsRestart", string.Join(", ", restart)), 255, 240, 20);
    }

    static void Asked(BinaryReader reader, int from)
    {
        string modName = reader.ReadString(), configName = reader.ReadString();
        bool broadcast = reader.ReadBoolean();
        string json = reader.ReadString();
        ModConfig? active = Find(modName, configName);

        if (active == null || active.Mode != ConfigScope.ServerSide || JsonNode.Parse(json) is not JsonObject data)
            return;
        ModConfig pending = active.Clone();
        ConfigManager.Populate(pending, data);
        bool success = true;
        NetworkText message = NetworkText.FromKey("tModLoader.ModConfigAccepted");
        ModConfig? loadTime = ConfigManager.GetLoadTimeConfig(active.Mod, active.Name);
        if (loadTime != null && loadTime.NeedsReload(pending))
        {
            success = false;
            message = NetworkText.FromKey("tModLoader.ModConfigCantSaveBecauseChangesWouldRequireAReload");
        }

        try
        {
            success &= active.AcceptClientChanges(pending, from, ref message);
        }
        catch (Exception e)
        {
            Log.Error($"{active.FullName}.AcceptClientChanges: {e}");
            success = false;
        }

        if (!success)
        {
            SendReply(false, message, active, broadcast, from, from);
            return;
        }

        ConfigManager.Save(pending);
        ConfigManager.CopyValues(pending, active);
        ConfigManager.Changed(active);
        SendReply(true, message, active, broadcast, from, -1);
        if ((ModNet.NetMode & 1) != 0)
            Replied(active, true, broadcast, from, message);
    }

    static void SendReply(bool success, NetworkText message, ModConfig active, bool broadcast, int requestor, int toClient)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(KindConfig);
        writer.Write(Reply);
        writer.Write(success);
        message.Serialize(writer);
        writer.Write(active.Mod.Name);
        writer.Write(active.Name);
        writer.Write(broadcast);
        writer.Write((byte)requestor);
        
        if (success)
            writer.Write(ConfigManager.ToJson(active).ToJsonString());
        Send(stream, toClient, -1);
    }

    static void ReadReply(BinaryReader reader)
    {
        bool success = reader.ReadBoolean();
        NetworkText message = NetworkText.Deserialize(reader);
        string modName = reader.ReadString(), configName = reader.ReadString();
        bool broadcast = reader.ReadBoolean();
        int requestor = reader.ReadByte();
        ModConfig? active = Find(modName, configName);
        if (active == null)
            return;
        if (success)
        {
            if (JsonNode.Parse(reader.ReadString()) is not JsonObject data)
                return;
            ConfigManager.Populate(active, data);
            synced[active] = data;
            ConfigManager.Changed(active);
        }

        Replied(active, success, broadcast, requestor, message);
    }

    static void Replied(ModConfig active, bool success, bool broadcast, int requestor, NetworkText message)
    {
        try
        {
            active.HandleAcceptClientChangesReply(success, requestor, message);
        }
        catch (Exception e)
        {
            Log.Error($"{active.FullName}.HandleAcceptClientChangesReply: {e}");
        }

        if (success)
        {
            if (broadcast)
                Main.NewText(Language.GetTextValue("tModLoader.ModConfigSharedConfigChanged", message.ToString(), active.Mod.Name, active.Name));
        }
        else if (broadcast || requestor == Main.myPlayer)
        {
            Main.NewText(Language.GetTextValue("tModLoader.ModConfigServerRejectedChanges", message.ToString()));
        }

        ConfigMenu.Refresh(active);
    }

    static ModConfig? Find(string modName, string configName)
    {
        Mod? mod = Host.FindMod(modName);
        return mod == null ? null : ConfigManager.GetConfig(mod, configName);
    }

    static void Send(MemoryStream stream, int toClient, int ignoreClient)
    {
        if (stream.Length > ModPacket.MaxLength)
        {
            Log.Error($"config net: the message has {stream.Length} bytes; the limit is {ModPacket.MaxLength}");
            return;
        }

        if (!ModNet.Send(stream.GetBuffer(), (int)stream.Length, toClient, ignoreClient))
            Log.Error("config net: the message was not sent");
    }
}
