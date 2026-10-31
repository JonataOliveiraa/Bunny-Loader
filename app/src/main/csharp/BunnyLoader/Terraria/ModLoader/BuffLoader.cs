using System;
using System.Collections.Generic;
using System.Threading;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static unsafe class BuffLoader
{
    static readonly object Gate = new();
    static ModBuff?[] buffs = Array.Empty<ModBuff?>();
    static GlobalBuff[] globals = Array.Empty<GlobalBuff>();
    static bool hooked;

    public static int BuffCount => BuffID.Count + Volatile.Read(ref buffs).Length;

    internal static GlobalBuff[] Globals => Volatile.Read(ref globals);

    public static ModBuff? GetBuff(int type)
    {
        ModBuff?[] all = Volatile.Read(ref buffs);
        uint index = (uint)(type - BuffID.Count);
        return index < (uint)all.Length ? all[index] : null;
    }

    internal static void Register(ModBuff buff)
    {
        string texture = ItemLoader.ResolveTexture(buff, buff.Texture);
        int type = Native(buff, texture, Localized(buff, buff.DisplayName, nameof(ModBuff.DisplayName)),
            Localized(buff, buff.Description, nameof(ModBuff.Description)));
        if (type == -1) throw new InvalidOperationException($"{buff.FullName}: the mod already has a buff named '{buff.Name}'");
        if (type < 0) throw new InvalidOperationException($"{buff.FullName}: registration failed ({type})");
        buff.Type = type;
        lock (Gate)
        {
            EnsureHooked();
            int index = type - BuffID.Count;
            ModBuff?[] all = buffs;
            if (index >= all.Length)
            {
                var grown = new ModBuff?[index + 1];
                Array.Copy(all, grown, all.Length);
                all = grown;
            }
            all[index] = buff;
            Volatile.Write(ref buffs, all);
        }
        BuffHooks.Install(buff.GetType(), typeof(ModBuff));
    }

    internal static void RegisterGlobal(GlobalBuff global)
    {
        lock (Gate)
        {
            var grown = new GlobalBuff[globals.Length + 1];
            Array.Copy(globals, grown, globals.Length);
            grown[^1] = global;
            Volatile.Write(ref globals, grown);
        }
        BuffHooks.Install(global.GetType(), typeof(GlobalBuff));
    }

    static void EnsureHooked()
    {
        if (hooked) return;
        hooked = true;
        Host.Native()->OnBuffsInstalled(&BuffsInstalled);
    }

    static string Localized(ModBuff buff, LocalizedText text, string property)
    {
        if (buff.GetType().GetProperty(property)!.DeclaringType == typeof(ModBuff))
        {
            var texts = LocalizationLoader.Texts(buff.Mod, buff.GetLocalizationKey(property));
            if (texts.Count > 0) return LocalizationLoader.Pack(texts);
        }
        return text.IsNull ? "" : text.Value ?? "";
    }

    static int Native(ModBuff buff, string texture, string displayName, string description)
    {
        byte* mod = Host.Utf8(buff.Mod.Uid), name = Host.Utf8(buff.Name), tex = Host.Utf8(texture);
        byte* dn = Host.Utf8(displayName), desc = Host.Utf8(description);
        try { return Host.Native()->RegisterBuff(mod, name, tex, dn, desc); }
        finally
        {
            Host.FreeUtf8(mod);
            Host.FreeUtf8(name);
            Host.FreeUtf8(tex);
            Host.FreeUtf8(dn);
            Host.FreeUtf8(desc);
        }
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    static void BuffsInstalled(int first, int last)
    {
        for (int type = first; type <= last; type++)
        {
            ModBuff? buff = GetBuff(type);
            if (buff == null) continue;
            try { buff.SetupContent(); }
            catch (Exception e) { Log.Error($"buff {buff.FullName}: SetStaticDefaults threw: {e}"); }
        }
    }
}
