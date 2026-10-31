using System.IO;
using BunnyLoader;

namespace Terraria.ModLoader;

public static unsafe partial class NPCLoader
{
    static void ReserveBossHead(ModNPC npc)
    {
        string texture = npc.BossHeadTexture;
        if (string.IsNullOrEmpty(texture)) return;
        string file;
        try { file = ItemLoader.ResolveTexture(npc, texture); }
        catch (FileNotFoundException) { return; }
        Reserve(file, texture, npc.Type);
    }

    internal static int ReserveBossHead(Mod mod, string texture, int npcType)
    {
        string? file = BossHeadFile(mod, texture);
        if (file == null) throw new FileNotFoundException($"{mod.Name}: boss head texture '{texture}' not found", texture);
        int slot = Reserve(file, texture, npcType);
        if (slot < 0) throw new System.InvalidOperationException($"{mod.Name}: boss head texture '{texture}' must be added during loading");
        return slot;
    }

    internal static int BossHeadSlot(string texture)
    {
        (string modName, string path) = ModContent.SplitName(texture);
        if (Host.FindMod(modName) is not Mod mod || BossHeadFile(mod, path) is not string file) return -1;
        byte* name = Host.Utf8(file);
        try { return Host.Native()->BossHeadSlotOf(name); }
        finally { Host.FreeUtf8(name); }
    }

    static string? BossHeadFile(Mod mod, string texture)
    {
        string clean = texture.Replace('\\', '/');
        if (clean.StartsWith(mod.Name + "/", System.StringComparison.Ordinal)) clean = clean[(mod.Name.Length + 1)..];
        if (!clean.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) clean += ".png";
        string? file = mod.ResolveFile(clean);
        return file != null && File.Exists(file) ? file : null;
    }

    static int Reserve(string file, string assetName, int npcType)
    {
        byte* path = Host.Utf8(file), asset = Host.Utf8(assetName);
        try { return Host.Native()->ReserveBossHead(path, asset, npcType); }
        finally
        {
            Host.FreeUtf8(path);
            Host.FreeUtf8(asset);
        }
    }
}
