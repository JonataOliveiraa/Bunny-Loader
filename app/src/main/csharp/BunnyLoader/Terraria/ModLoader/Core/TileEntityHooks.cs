using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.DataStructures;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader.IO;

namespace Terraria.ModLoader.Core;

internal static class TileEntityHooks
{
    public const byte KindTileEntity = 9;

    const int FirstId = 0x50000000;
    const string SaveKey = "#csTileEntities";
    const int SharingMessage = 86;
    const byte Entity = 0, Killed = 1, PlaceRequest = 2;

    static readonly List<ModTileEntity> Templates = new();
    static readonly Dictionary<string, ModTileEntity> ByKey = new(StringComparer.Ordinal);
    internal static readonly Dictionary<int, ModTileEntity> ByID = new();
    static readonly Dictionary<int, ModTileEntity> ByPosition = new();
    static readonly List<JsonNode> Unloaded = new();
    static int nextId = FirstId;

    internal static int Count => ByID.Count;

    internal static IEnumerable<ModTileEntity> All => Templates;

    internal static void Register(ModTileEntity template)
    {
        template.Type = Templates.Count;
        Templates.Add(template);
        ByKey[KeyOf(template)] = template;
        ItemHooks.Once("TileEntities", () =>
        {
            ModNet.Install();
            On_WorldGen.clearWorld += ClearWorld;
            On_WorldFile.LoadWorld += LoadWorld;
            On_WorldFile.InternalSaveWorld += InternalSaveWorld;
            On_NetMessage.SendSection += SendSection;
            On_NetMessage.SendData += SendData;
        });
        Type type = template.GetType();
        if (new[] { nameof(ModTileEntity.Update), nameof(ModTileEntity.PreGlobalUpdate), nameof(ModTileEntity.PostGlobalUpdate) }.Any(name => ItemHooks.Overrides(type, typeof(ModTileEntity), name)))
            ItemHooks.Once("TileEntityUpdates", () => On_TileEntity.PerformUpdates += PerformUpdates);
    }

    static string KeyOf(ModTileEntity entity) => $"{entity.Mod.Uid}/{entity.Name}";

    static int Key(int x, int y) => (x & 0xFFFF) | ((y & 0xFFFF) << 16);

    internal static ModTileEntity? At(int x, int y) => ByPosition.TryGetValue(Key(x, y), out ModTileEntity? entity) ? entity : null;

    static ModTileEntity Create(ModTileEntity template, int x, int y, int id)
    {
        var entity = (ModTileEntity)Activator.CreateInstance(template.GetType(), true)!;
        entity.Mod = template.Mod;
        entity.Type = template.Type;
        entity.ID = id;
        entity.Position = new Point16(x, y);
        return entity;
    }

    static void Add(ModTileEntity entity)
    {
        int key = Key(entity.Position.X, entity.Position.Y);
        if (ByPosition.TryGetValue(key, out ModTileEntity? old) && old != entity) ByID.Remove(old.ID);
        ByID[entity.ID] = entity;
        ByPosition[key] = entity;
    }

    static void Remove(ModTileEntity entity)
    {
        ByID.Remove(entity.ID);
        int key = Key(entity.Position.X, entity.Position.Y);
        if (ByPosition.TryGetValue(key, out ModTileEntity? here) && here == entity) ByPosition.Remove(key);
    }

    static ModTileEntity PlaceQuiet(ModTileEntity template, int x, int y)
    {
        ModTileEntity entity = Create(template, x, y, nextId++);
        Add(entity);
        return entity;
    }

    internal static int Place(ModTileEntity template, int x, int y)
    {
        ModTileEntity entity = PlaceQuiet(template, x, y);
        if ((Main.netMode & 2) != 0) SendEntity(entity, -1, -1);
        return entity.ID;
    }

    internal static void Kill(ModTileEntity template, int x, int y)
    {
        if (At(x, y) is not ModTileEntity entity || entity.Type != template.Type) return;
        try { entity.OnKill(); }
        catch (Exception e) { ItemHooks.Report($"{entity.FullName}.OnKill", e); }
        Remove(entity);
        if ((Main.netMode & 2) != 0) Send(w => { w.Write(Killed); w.Write(entity.ID); }, -1, -1);
    }

    internal static void RequestPlace(ModTileEntity template, int x, int y) =>
        Send(w =>
        {
            w.Write(PlaceRequest);
            w.Write(KeyOf(template));
            w.Write((short)x);
            w.Write((short)y);
        }, -1, -1);

    static void Send(Action<BinaryWriter> write, int toClient, int ignoreClient)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(KindTileEntity);
        write(writer);
        writer.Flush();
        byte[] packet = stream.ToArray();
        ModNet.Send(packet, packet.Length, toClient, ignoreClient);
    }

    internal static byte[] Payload(ModTileEntity entity)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        try { entity.NetSend(writer); }
        catch (Exception e) { ItemHooks.Report($"{entity.FullName}.NetSend", e); }
        writer.Flush();
        return stream.ToArray();
    }

    static void SendEntity(ModTileEntity entity, int toClient, int ignoreClient)
    {
        byte[] payload = Payload(entity);
        Send(w =>
        {
            w.Write(Entity);
            w.Write(entity.ID);
            w.Write((short)entity.Position.X);
            w.Write((short)entity.Position.Y);
            w.Write(KeyOf(entity));
            w.Write(payload.Length);
            w.Write(payload);
        }, toClient, ignoreClient);
    }

    internal static void Receive(BinaryReader reader, int from)
    {
        byte action = reader.ReadByte();
        if (action == Entity && Main.netMode == 1)
        {
            int id = reader.ReadInt32(), x = reader.ReadInt16(), y = reader.ReadInt16();
            string key = reader.ReadString();
            byte[] payload = reader.ReadBytes(reader.ReadInt32());
            Apply(id, x, y, key, payload);
        }
        else if (action == Killed && Main.netMode == 1)
        {
            if (ByID.TryGetValue(reader.ReadInt32(), out ModTileEntity? entity)) Remove(entity);
        }
        else if (action == PlaceRequest && (Main.netMode & 2) != 0 && from < 256)
        {
            string key = reader.ReadString();
            ServerPlace(key, reader.ReadInt16(), reader.ReadInt16());
        }
    }

    internal static void Apply(int id, int x, int y, string key, byte[] payload)
    {
        if (!ByKey.TryGetValue(key, out ModTileEntity? template)) return;
        if (!ByID.TryGetValue(id, out ModTileEntity? entity) || entity.Type != template.Type)
        {
            entity = Create(template, x, y, id);
            Add(entity);
        }
        using var reader = new BinaryReader(new MemoryStream(payload));
        try { entity.NetReceive(reader); }
        catch (Exception e) { ItemHooks.Report($"{entity.FullName}.NetReceive", e); }
    }

    internal static void ServerPlace(string key, int x, int y)
    {
        if (!ByKey.TryGetValue(key, out ModTileEntity? template) || !WorldGen.InWorld(x, y) || At(x, y) != null) return;
        try
        {
            if (!template.IsTileValidForEntity(x, y)) return;
        }
        catch (Exception e)
        {
            ItemHooks.Report($"{template.FullName}.IsTileValidForEntity", e);
            return;
        }
        ModTileEntity entity = PlaceQuiet(template, x, y);
        try { entity.OnNetPlace(); }
        catch (Exception e) { ItemHooks.Report($"{entity.FullName}.OnNetPlace", e); }
        SendEntity(entity, -1, -1);
    }

    static void SendSection(On_NetMessage.orig_SendSection orig, int whoAmi, int sectionX, int sectionY)
    {
        orig(whoAmi, sectionX, sectionY);
        if ((Main.netMode & 2) == 0 || ByID.Count == 0) return;
        try
        {
            int x0 = sectionX * 200, y0 = sectionY * 150;
            foreach (ModTileEntity entity in ByID.Values.ToArray())
            {
                int x = entity.Position.X, y = entity.Position.Y;
                if (x >= x0 && x < x0 + 200 && y >= y0 && y < y0 + 150) SendEntity(entity, whoAmi, -1);
            }
        }
        catch (Exception e) { ItemHooks.Report("ModTileEntity (world section)", e); }
    }

    static void SendData(On_NetMessage.orig_SendData orig, int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3,
        float number4, int number5, int number6, int number7)
    {
        if (msgType != SharingMessage || number < FirstId)
        {
            orig(msgType, remoteClient, ignoreClient, text, number, number2, number3, number4, number5, number6, number7);
            return;
        }
        if ((Main.netMode & 2) != 0 && ByID.TryGetValue(number, out ModTileEntity? entity)) SendEntity(entity, remoteClient, ignoreClient);
    }

    static void PerformUpdates(On_TileEntity.orig_PerformUpdates orig)
    {
        if (ByID.Count == 0)
        {
            orig();
            return;
        }
        ModTileEntity[] list = ByID.Values.ToArray();
        foreach (ModTileEntity entity in list) Call(entity, e => e.PreGlobalUpdate(), nameof(ModTileEntity.PreGlobalUpdate));
        orig();
        foreach (ModTileEntity entity in list)
            if (entity.RequiresUpdates && ByID.TryGetValue(entity.ID, out ModTileEntity? still) && still == entity)
                Call(entity, e => e.Update(), nameof(ModTileEntity.Update));
        foreach (ModTileEntity entity in list) Call(entity, e => e.PostGlobalUpdate(), nameof(ModTileEntity.PostGlobalUpdate));
    }

    static void Call(ModTileEntity entity, Action<ModTileEntity> action, string what)
    {
        try { action(entity); }
        catch (Exception e) { ItemHooks.Report($"{entity.FullName}.{what}", e); }
    }

    static void Clear()
    {
        ByID.Clear();
        ByPosition.Clear();
        Unloaded.Clear();
        nextId = FirstId;
    }

    static void ClearWorld(On_WorldGen.orig_clearWorld orig)
    {
        orig();
        Clear();
    }

    static string? DataFile() => Main.worldPathName is { Length: > 0 } path ? path + ".bl.json" : null;

    static void LoadWorld(On_WorldFile.orig_LoadWorld orig, bool canCheckFileState)
    {
        orig(canCheckFileState);
        try { Load(); }
        catch (Exception e) { ItemHooks.Report("ModTileEntity (load)", e); }
    }

    static void Load()
    {
        Clear();
        if (SystemHooks.ReadObject(DataFile())[SaveKey] is not JsonArray entries) return;
        int loaded = 0, dropped = 0;
        foreach (JsonNode? node in entries)
        {
            if (node is not JsonObject entry) continue;
            string? key = entry["t"]?.GetValue<string>();
            if (key == null || !ByKey.TryGetValue(key, out ModTileEntity? template))
            {
                Unloaded.Add(entry.DeepClone());
                continue;
            }
            int x = entry["x"]?.GetValue<int>() ?? -1, y = entry["y"]?.GetValue<int>() ?? -1;
            bool valid;
            try { valid = WorldGen.InWorld(x, y) && template.IsTileValidForEntity(x, y); }
            catch (Exception e)
            {
                ItemHooks.Report($"{template.FullName}.IsTileValidForEntity", e);
                valid = false;
            }
            if (!valid)
            {
                dropped++;
                continue;
            }
            ModTileEntity entity = Create(template, x, y, nextId++);
            try { entity.LoadData(SystemHooks.TagOf(entry["d"])); }
            catch (Exception e) { ItemHooks.Report($"{entity.FullName}.LoadData", e); }
            Add(entity);
            loaded++;
        }
        if (loaded > 0 || dropped > 0 || Unloaded.Count > 0)
            Log.Info($"mod tile entities: {loaded} loaded, {dropped} without their tile, {Unloaded.Count} of missing mods");
    }

    static void InternalSaveWorld(On_WorldFile.orig_InternalSaveWorld orig, bool useCloudSaving, bool resetTime)
    {
        orig(useCloudSaving, resetTime);
        if (useCloudSaving) return;
        try { Save(); }
        catch (Exception e) { ItemHooks.Report("ModTileEntity (save)", e); }
    }

    static void Save()
    {
        if (DataFile() is not string file) return;
        JsonObject all = SystemHooks.ReadObject(file);
        var entries = new JsonArray();
        foreach (JsonNode node in Unloaded) entries.Add(node.DeepClone());
        foreach (ModTileEntity entity in ByID.Values)
        {
            var tag = new TagCompound();
            try { entity.SaveData(tag); }
            catch (Exception e) { ItemHooks.Report($"{entity.FullName}.SaveData", e); }
            var entry = new JsonObject { ["t"] = KeyOf(entity), ["x"] = (int)entity.Position.X, ["y"] = (int)entity.Position.Y };
            if (tag.Count > 0) entry["d"] = JsonNode.Parse(TagJson.Write(tag));
            entries.Add(entry);
        }
        if (entries.Count > 0) all[SaveKey] = entries;
        else if (!all.ContainsKey(SaveKey)) return;
        else all.Remove(SaveKey);
        SystemHooks.WriteObject(file, all);
    }
}
