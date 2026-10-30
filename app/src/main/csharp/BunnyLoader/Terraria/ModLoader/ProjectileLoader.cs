using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.ID;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static unsafe class ProjectileLoader
{
    const int VanillaCount = 1136;

    static readonly object Gate = new();
    static ModProjectile?[] projectiles = Array.Empty<ModProjectile?>();
    static readonly EntityInstances<ModProjectile> Instances = new();
    static readonly List<GameHandle> TemplatePins = new();
    static bool hooked;

    public static int ProjectileCount => VanillaCount + Volatile.Read(ref projectiles).Length;

    public static ModProjectile? GetProjectile(int type)
    {
        ModProjectile?[] all = Volatile.Read(ref projectiles);
        uint index = (uint)(type - VanillaCount);
        return index < (uint)all.Length ? all[index] : null;
    }

    internal static ModProjectile? InstanceOf(Projectile projectile)
    {
        if (projectile.IsNull) return null;
        ModProjectile? template = GetProjectile(projectile.type);
        if (template == null) return null;
        ModProjectile? current = Instances.Get(projectile.Pointer);
        return current != null && current.Type == template.Type ? current : Bind(template, projectile);
    }

    static ModProjectile Bind(ModProjectile template, Projectile projectile)
    {
        ModProjectile instance = template.NewInstance(projectile);
        instance.Type = template.Type;
        Instances.Set(projectile.Pointer, instance);
        return instance;
    }

    internal static void Register(ModProjectile projectile)
    {
        string texture = ItemLoader.ResolveTexture(projectile, projectile.Texture);
        string displayName = DisplayNames(projectile);
        int type;
        byte* mod = Host.Utf8(projectile.Mod.Uid), name = Host.Utf8(projectile.Name), tex = Host.Utf8(texture), dn = Host.Utf8(displayName);
        try { type = Host.Native()->RegisterProjectile(mod, name, tex, dn); }
        finally
        {
            Host.FreeUtf8(mod);
            Host.FreeUtf8(name);
            Host.FreeUtf8(tex);
            Host.FreeUtf8(dn);
        }
        if (type == -1) throw new InvalidOperationException($"{projectile.FullName}: the mod already has a projectile named '{projectile.Name}'");
        if (type < 0) throw new InvalidOperationException($"{projectile.FullName}: registration failed ({type})");
        projectile.Type = type;
        lock (Gate)
        {
            int index = type - VanillaCount;
            ModProjectile?[] all = projectiles;
            if (index >= all.Length)
            {
                var grown = new ModProjectile?[index + 1];
                Array.Copy(all, grown, all.Length);
                all = grown;
            }
            all[index] = projectile;
            Volatile.Write(ref projectiles, all);
            if (!hooked)
            {
                hooked = true;
                On_Projectile.SetDefaults += SetDefaults;
                Host.Native()->OnProjectilesInstalled(&Installed);
            }
        }
        ProjectileHooks.Install(projectile.GetType());
    }

    static string DisplayNames(ModProjectile projectile)
    {
        if (projectile.GetType().GetProperty(nameof(ModProjectile.DisplayName))!.DeclaringType == typeof(ModProjectile))
        {
            var texts = LocalizationLoader.Texts(projectile.Mod, projectile.GetLocalizationKey(nameof(ModProjectile.DisplayName)));
            if (texts.Count > 0) return LocalizationLoader.Pack(texts);
        }
        return projectile.DisplayName.IsNull ? "" : projectile.DisplayName.Value ?? "";
    }

    static void SetDefaults(On_Projectile.orig_SetDefaults orig, Projectile self, int Type)
    {
        orig(self, Type);
        ModProjectile? template = GetProjectile(Type);
        if (template == null)
        {
            if (Instances.Count > 0) Instances.Set(self.Pointer, null);
            return;
        }
        ModProjectile instance = Bind(template, self);
        try { instance.SetDefaults(); }
        catch (Exception e) { Log.Error($"projectile {template.FullName}: SetDefaults threw: {e}"); }
        Host.Native()->FinishProjectile(self.Pointer, Type);
    }

    [UnmanagedCallersOnly]
    static void Installed(int first, int last)
    {
        for (int type = first; type <= last; type++)
        {
            ModProjectile? projectile = GetProjectile(type);
            if (projectile == null) continue;
            try
            {
                if (projectile.Entity.IsNull)
                {
                    Projectile template = Projectile.New();
                    TemplatePins.Add(new GameHandle(template.Pointer));
                    template.SetDefaults(type);
                    projectile.Entity = template;
                }
                projectile.SetupContent();
                if (ItemHooks.Overrides(projectile.GetType(), typeof(ModProjectile), nameof(ModProjectile.Colliding)) || ProjectileID.Sets.IsAWhip[type])
                    ProjectileID.Sets.IsAComplexCollision[type] = true;
                Host.Native()->SetProjectileFrames(type, Main.projFrames[type]);
            }
            catch (Exception e)
            {
                Log.Error($"projectile {projectile.FullName}: SetStaticDefaults threw: {e}");
            }
        }
    }
}
