using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using BunnyLoader;
using Terraria.Localization;

namespace Terraria.ModLoader.Core;

internal static unsafe class ServerSyncHooks
{
    public const byte KindSyncPlan = 11;

    internal static bool SyncMods = true, AllowTextures = true;

    static bool allowCheats = true;

    internal static bool AllowCheats
    {
        get => allowCheats;
        set
        {
            allowCheats = value;
            Host.Native()->HostRules(value ? 1 : 0);
        }
    }

    static JsonArray? ownMods;
    static JsonArray? ownTextures;

    internal static bool Active { get; private set; }

    internal static void Install()
    {
        JsonObject? info = PackInfo();
        if (info == null)
            return;
        ownMods = info["mods"] as JsonArray ?? new JsonArray();
        ownTextures = info["textures"] as JsonArray ?? new JsonArray();
        var hello = new JsonObject { ["mods"] = ownMods.DeepClone(), ["textures"] = ownTextures.Count };
        ModNet.SetHello(hello.ToJsonString());
        Host.Native()->NetHelloListen(&Hello);
        Active = true;
        Log.Info($"server sync: {ownMods.Count} mods");
    }

    internal static void InstallMenus()
    {
        if (!Active) return;
        HostSettingsMenu.Install();
        ServerSyncMenu.Install();
    }

    static JsonObject? PackInfo()
    {
        try
        {
            int size = 4096;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                byte[] buffer = new byte[size];
                int length;
                fixed (byte* out_ = buffer) length = Host.Native()->PackInfo(out_, buffer.Length);
                if (length < 0)
                    return null;
                if (length < buffer.Length)
                    return JsonNode.Parse(Encoding.UTF8.GetString(buffer, 0, length)) as JsonObject;
                size = length + 1;
            }
        }
        catch (Exception e)
        {
            Log.Error($"server sync: the pack list could not be read: {e}");
        }

        return null;
    }

    internal static string? Call(string method, string? arg = null)
    {
        byte* m = Host.Utf8(method);
        byte* a = arg == null ? null : Host.Utf8(arg);
        try
        {
            int size = 4096;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                byte[] buffer = new byte[size];
                int length;
                fixed (byte* out_ = buffer) length = Host.Native()->ServerSync(m, a, out_, buffer.Length);
                if (length < 0)
                    return null;
                if (length < buffer.Length)
                    return Encoding.UTF8.GetString(buffer, 0, length);
                size = length + 1;
            }

            return null;
        }
        finally
        {
            Host.FreeUtf8(m);
            if (a != null)
                Host.FreeUtf8(a);
        }
    }

    static string Text(string key, string value = "") => Language.GetTextValue("BunnyLoader." + key, value);

    [UnmanagedCallersOnly]
    static void Hello(int from, byte* text)
    {
        try
        {
            Judge(from, Marshal.PtrToStringUTF8((nint)text) ?? "");
        }
        catch (Exception e)
        {
            Log.Error($"server sync: judging player {from} threw: {e}");
        }
    }

    static void Judge(int from, string text)
    {
        if (from < 0 || from >= 255 || from == Main.myPlayer)
            return;
        JsonObject? hello = null;
        try
        {
            hello = JsonNode.Parse(text) as JsonObject;
        }
        catch
        {
        }

        JsonArray theirs = hello?["mods"] as JsonArray ?? new JsonArray();
        int textures = hello?["textures"]?.GetValue<int>() ?? 0;
        if (!AllowTextures && textures > 0)
        {
            Log.Info($"server sync: player {from} refused ({textures} texture pack(s))");
            Boot(from, Text("ServerRefusedTextures", textures.ToString()));
            return;
        }

        JsonArray ours = ownMods ?? new JsonArray();
        if (hello != null && Same(ours, theirs))
            return;
        if (SyncMods && hello != null)
        {
            Log.Info($"server sync: player {from} has other mods; sending the {ours.Count} of this server");
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(KindSyncPlan);
            writer.Write(new JsonObject { ["mods"] = ours.DeepClone() }.ToJsonString());
            writer.Flush();
            ModNet.Send(stream.GetBuffer(), (int)stream.Length, from, -1);
            Boot(from, Text("ServerRefusedSync"));
            return;
        }

        string Names(IEnumerable<JsonNode?> list) => string.Join(", ", list.Select(m => $"{m?["name"]} {m?["version"]}"));
        var ourKeys = ours.Select(Key).ToHashSet();
        var theirKeys = theirs.Select(Key).ToHashSet();
        string missing = Names(ours.Where(m => !theirKeys.Contains(Key(m))));
        string extra = Names(theirs.Where(m => !ourKeys.Contains(Key(m))));
        string reason = Text("ServerRefusedMods") + (missing.Length > 0 ? Text("ServerRefusedMissing", missing) : "") + (extra.Length > 0 ? Text("ServerRefusedExtra", extra) : "");
        Log.Info($"server sync: player {from} refused, other mods (missing: [{missing}]; extra: [{extra}])");
        Boot(from, reason);
    }

    static string Key(JsonNode? mod) => $"{mod?["uid"]}|{mod?["version"]}";

    static bool Same(JsonArray a, JsonArray b)
    {
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
        {
            string version = a[i]?["version"]?.ToString() ?? "";
            if (a[i]?["uid"]?.ToString() != b[i]?["uid"]?.ToString() || version.Trim().Length == 0 || version != b[i]?["version"]?.ToString())
                return false;
        }

        return true;
    }

    static void Boot(int player, string reason) => NetMessage.BootPlayer(player, NetworkText.FromLiteral(reason));

    internal static void Receive(BinaryReader reader, int from)
    {
        if (from != 256 || ModNet.NetMode != 1 || !Active)
            return;
        if (JsonNode.Parse(reader.ReadString()) is not JsonObject plan)
            return;
        string path = "";
        try
        {
            path = Main.ActivePlayerFileData.IsNull ? "" : Main.ActivePlayerFileData.Path ?? "";
        }
        catch
        {
        }

        var offer = new JsonObject
        {
            ["mods"] = plan["mods"]?.DeepClone() ?? new JsonArray(),
            ["address"] = Netplay.ServerIPText ?? "",
            ["port"] = Netplay.ListenPort,
            ["password"] = Netplay.ServerPassword ?? "",
            ["player"] = path.Split('/', '\\').LastOrDefault() ?? "",
            ["textures"] = ownTextures?.DeepClone() ?? new JsonArray()
        };
        Log.Info($"server sync: other mods; offering the sync with {Netplay.ServerIPText}:{Netplay.ListenPort}");
        if (Call("offer", offer.ToJsonString()) != null)
            ServerSyncMenu.Open();
    }
}
