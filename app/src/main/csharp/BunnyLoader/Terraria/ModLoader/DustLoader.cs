using System;
using System.Runtime.InteropServices;
using System.Threading;
using BunnyLoader;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.ID;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static unsafe class DustLoader
{
    const uint OnSpawnBit = 1, UpdateBit = 2, MidUpdateBit = 4, GetAlphaBit = 8, PreDrawBit = 16;

    static readonly object Gate = new();
    static ModDust?[] dusts = Array.Empty<ModDust?>();
    static bool hooked;

    public static int DustCount => DustID.Count + Volatile.Read(ref dusts).Length;

    public static ModDust? GetDust(int type)
    {
        ModDust?[] all = Volatile.Read(ref dusts);
        uint index = (uint)(type - DustID.Count);
        return index < (uint)all.Length ? all[index] : null;
    }

    internal static void Register(ModDust dust)
    {
        string texture = string.IsNullOrEmpty(dust.Texture) ? "" : ItemLoader.ResolveTexture(dust, dust.Texture);
        uint hooks = 0;
        Type type = dust.GetType();
        if (ItemHooks.Overrides(type, typeof(ModDust), nameof(ModDust.OnSpawn))) hooks |= OnSpawnBit;
        if (ItemHooks.Overrides(type, typeof(ModDust), nameof(ModDust.Update))) hooks |= UpdateBit;
        if (ItemHooks.Overrides(type, typeof(ModDust), nameof(ModDust.MidUpdate))) hooks |= MidUpdateBit;
        if (ItemHooks.Overrides(type, typeof(ModDust), nameof(ModDust.GetAlpha))) hooks |= GetAlphaBit;
        if (ItemHooks.Overrides(type, typeof(ModDust), nameof(ModDust.PreDraw))) hooks |= PreDrawBit;
        int id;
        byte* mod = Host.Utf8(dust.Mod.Uid), name = Host.Utf8(dust.Name), tex = Host.Utf8(texture);
        try { id = Host.Native()->RegisterDust(mod, name, tex, hooks); }
        finally
        {
            Host.FreeUtf8(mod);
            Host.FreeUtf8(name);
            Host.FreeUtf8(tex);
        }
        if (id == -1) throw new InvalidOperationException($"{dust.FullName}: the mod already has a dust named '{dust.Name}'");
        if (id < 0) throw new InvalidOperationException($"{dust.FullName}: registration failed ({id})");
        dust.Type = id;
        lock (Gate)
        {
            int index = id - DustID.Count;
            ModDust?[] all = dusts;
            if (index >= all.Length)
            {
                var grown = new ModDust?[index + 1];
                Array.Copy(all, grown, all.Length);
                all = grown;
            }
            all[index] = dust;
            Volatile.Write(ref dusts, all);
            if (hooked) return;
            hooked = true;
            Host.Native()->SetDustCallbacks(&Spawn, &Update, &MidUpdate, &PreDraw, &GetAlpha);
            Host.Native()->OnDustsInstalled(&Installed);
        }
    }

    internal static Asset<Texture2D> TextureOf(ModDust dust)
    {
        IntPtr asset = Host.Native()->DustTextureAsset(dust.Type);
        return asset == IntPtr.Zero ? null! : Asset<Texture2D>.ForGameAsset(asset, dust.FullName);
    }

    static void Report(ModDust dust, string method, Exception e) => ItemHooks.Report($"{dust.FullName}.{method}", e);

    [UnmanagedCallersOnly]
    static void Installed(int first, int last)
    {
        for (int type = first; type <= last; type++)
        {
            if (GetDust(type) is not ModDust dust) continue;
            try { dust.SetupContent(); }
            catch (Exception e) { Log.Error($"dust {dust.FullName}: SetStaticDefaults threw: {e}"); }
            if (dust.UpdateType >= 0) Host.Native()->SetDustUpdateType(type, dust.UpdateType);
        }
    }

    [UnmanagedCallersOnly]
    static void Spawn(int type, IntPtr pointer)
    {
        if (GetDust(type) is not ModDust dust) return;
        try { dust.OnSpawn(new Dust(pointer)); }
        catch (Exception e) { Report(dust, nameof(ModDust.OnSpawn), e); }
    }

    [UnmanagedCallersOnly]
    static void Update(IntPtr* list, int count, byte* flags)
    {
        for (int i = 0; i < count; i++)
        {
            var d = new Dust(list[i]);
            if (GetDust(d.type) is not ModDust dust) continue;
            try { flags[i] = dust.Update(d) ? (byte)1 : (byte)0; }
            catch (Exception e) { Report(dust, nameof(ModDust.Update), e); }
        }
    }

    [UnmanagedCallersOnly]
    static void MidUpdate(IntPtr* list, int count, byte* flags)
    {
        for (int i = 0; i < count; i++)
        {
            var d = new Dust(list[i]);
            if (GetDust(d.type) is not ModDust dust) continue;
            try { flags[i] = dust.MidUpdate(d) ? (byte)1 : (byte)0; }
            catch (Exception e) { Report(dust, nameof(ModDust.MidUpdate), e); }
        }
    }

    [UnmanagedCallersOnly]
    static void PreDraw(IntPtr* list, int count, byte* flags)
    {
        SpriteBatch batch = Main.spriteBatch;
        try
        {
            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None,
                RasterizerState.CullNone, default, Main.Transform);
        }
        catch (Exception e)
        {
            ItemHooks.Report("ModDust.PreDraw (SpriteBatch.Begin)", e);
            return;
        }
        try
        {
            for (int i = 0; i < count; i++)
            {
                var d = new Dust(list[i]);
                if (GetDust(d.type) is not ModDust dust) continue;
                try { flags[i] = dust.PreDraw(d) ? (byte)1 : (byte)0; }
                catch (Exception e) { Report(dust, nameof(ModDust.PreDraw), e); }
            }
        }
        finally { batch.End(); }
    }

    [UnmanagedCallersOnly]
    static int GetAlpha(IntPtr pointer, uint* color)
    {
        var d = new Dust(pointer);
        if (GetDust(d.type) is not ModDust dust) return 0;
        try
        {
            if (dust.GetAlpha(d, *(Color*)color) is not Color result) return 0;
            *(Color*)color = result;
            return 1;
        }
        catch (Exception e)
        {
            Report(dust, nameof(ModDust.GetAlpha), e);
            return 0;
        }
    }
}
