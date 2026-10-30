using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
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
    internal static readonly GlobalList<Projectile, GlobalProjectile> Globals = new();
    static bool hooked, installed;

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
            EnsureHooked();
            if (!installed)
            {
                installed = true;
                Host.Native()->OnProjectilesInstalled(&Installed);
            }
        }
        ProjectileHooks.Install(projectile.GetType(), typeof(ModProjectile));
    }

    internal static void RegisterGlobal(GlobalProjectile global)
    {
        Globals.Add(global);
        lock (Gate) EnsureHooked();
        ProjectileHooks.Install(global.GetType(), typeof(GlobalProjectile));
    }

    static void EnsureHooked()
    {
        if (hooked) return;
        hooked = true;
        On_Projectile.SetDefaults += SetDefaults;
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
            if (Globals.Count == 0) return;
            Globals.Finish(self, Globals.Begin(self));
            GlobalSetDefaults(self);
            return;
        }
        ModProjectile instance = Bind(template, self);
        GlobalProjectile?[]? slots = Globals.Begin(self);
        try { instance.SetDefaults(); }
        catch (Exception e) { Log.Error($"projectile {template.FullName}: SetDefaults threw: {e}"); }
        Globals.Finish(self, slots);
        Host.Native()->FinishProjectile(self.Pointer, Type);
        GlobalSetDefaults(self);
    }

    static void GlobalSetDefaults(Projectile projectile)
    {
        foreach (GlobalProjectile? g in GlobalsOf(projectile))
        {
            if (g == null) continue;
            try { g.SetDefaults(projectile); }
            catch (Exception e) { Report(g, nameof(GlobalProjectile.SetDefaults), e); }
        }
    }

    internal static GlobalProjectile?[] GlobalsOf(Projectile projectile) => Globals.Slots(projectile) ?? Array.Empty<GlobalProjectile?>();

    internal static bool HasHooks(Projectile projectile) => InstanceOf(projectile) != null || Globals.Slots(projectile) != null;

    static void Report(ModType owner, string method, Exception e) => ItemHooks.Report($"{owner.FullName}.{method}", e);

    internal static void OnSpawn(Projectile projectile, IEntitySource source)
    {
        if (InstanceOf(projectile) is ModProjectile m)
        {
            try { m.OnSpawn(source); }
            catch (Exception e) { Report(m, nameof(ModProjectile.OnSpawn), e); }
        }
        foreach (GlobalProjectile? g in GlobalsOf(projectile))
        {
            if (g == null) continue;
            try { g.OnSpawn(projectile, source); }
            catch (Exception e) { Report(g, nameof(GlobalProjectile.OnSpawn), e); }
        }
    }

    public static bool PreAI(Projectile projectile)
    {
        bool result = true;
        foreach (GlobalProjectile? g in GlobalsOf(projectile))
        {
            if (g == null) continue;
            try { result &= g.PreAI(projectile); }
            catch (Exception e) { Report(g, nameof(GlobalProjectile.PreAI), e); }
        }
        if (result && InstanceOf(projectile) is ModProjectile m)
        {
            try { return m.PreAI(); }
            catch (Exception e) { Report(m, nameof(ModProjectile.PreAI), e); }
        }
        return result;
    }

    public static void AI(Projectile projectile)
    {
        if (InstanceOf(projectile) is ModProjectile m)
        {
            try { m.AI(); }
            catch (Exception e) { Report(m, nameof(ModProjectile.AI), e); }
        }
        foreach (GlobalProjectile? g in GlobalsOf(projectile))
        {
            if (g == null) continue;
            try { g.AI(projectile); }
            catch (Exception e) { Report(g, nameof(GlobalProjectile.AI), e); }
        }
    }

    public static void PostAI(Projectile projectile)
    {
        if (InstanceOf(projectile) is ModProjectile m)
        {
            try { m.PostAI(); }
            catch (Exception e) { Report(m, nameof(ModProjectile.PostAI), e); }
        }
        foreach (GlobalProjectile? g in GlobalsOf(projectile))
        {
            if (g == null) continue;
            try { g.PostAI(projectile); }
            catch (Exception e) { Report(g, nameof(GlobalProjectile.PostAI), e); }
        }
    }

    public static bool ShouldUpdatePosition(Projectile projectile)
    {
        if (InstanceOf(projectile) is ModProjectile m)
        {
            try
            {
                if (!m.ShouldUpdatePosition()) return false;
            }
            catch (Exception e) { Report(m, nameof(ModProjectile.ShouldUpdatePosition), e); }
        }
        foreach (GlobalProjectile? g in GlobalsOf(projectile))
        {
            if (g == null) continue;
            try
            {
                if (!g.ShouldUpdatePosition(projectile)) return false;
            }
            catch (Exception e) { Report(g, nameof(GlobalProjectile.ShouldUpdatePosition), e); }
        }
        return true;
    }

    public static bool OnTileCollide(Projectile projectile, Vector2 oldVelocity)
    {
        bool result = true;
        foreach (GlobalProjectile? g in GlobalsOf(projectile))
        {
            if (g == null) continue;
            try { result &= g.OnTileCollide(projectile, oldVelocity); }
            catch (Exception e) { Report(g, nameof(GlobalProjectile.OnTileCollide), e); }
        }
        if (result && InstanceOf(projectile) is ModProjectile m)
        {
            try { return m.OnTileCollide(oldVelocity); }
            catch (Exception e) { Report(m, nameof(ModProjectile.OnTileCollide), e); }
        }
        return result;
    }

    public static bool PreKill(Projectile projectile, int timeLeft)
    {
        bool result = true;
        foreach (GlobalProjectile? g in GlobalsOf(projectile))
        {
            if (g == null) continue;
            try { result &= g.PreKill(projectile, timeLeft); }
            catch (Exception e) { Report(g, nameof(GlobalProjectile.PreKill), e); }
        }
        if (result && InstanceOf(projectile) is ModProjectile m)
        {
            try { return m.PreKill(timeLeft); }
            catch (Exception e) { Report(m, nameof(ModProjectile.PreKill), e); }
        }
        return result;
    }

    public static void OnKill(Projectile projectile, int timeLeft)
    {
        if (InstanceOf(projectile) is ModProjectile m)
        {
            try { m.OnKill(timeLeft); }
            catch (Exception e) { Report(m, nameof(ModProjectile.OnKill), e); }
        }
        foreach (GlobalProjectile? g in GlobalsOf(projectile))
        {
            if (g == null) continue;
            try { g.OnKill(projectile, timeLeft); }
            catch (Exception e) { Report(g, nameof(GlobalProjectile.OnKill), e); }
        }
    }

    public static bool? CanDamage(Projectile projectile)
    {
        bool? result = null;
        foreach (GlobalProjectile? g in GlobalsOf(projectile))
        {
            if (g == null) continue;
            bool? canDamage = null;
            try { canDamage = g.CanDamage(projectile); }
            catch (Exception e) { Report(g, nameof(GlobalProjectile.CanDamage), e); }
            if (canDamage == false) return false;
            if (canDamage == true) result = true;
        }
        if (result.HasValue || InstanceOf(projectile) is not ModProjectile m) return result;
        try { return m.CanDamage(); }
        catch (Exception e) { Report(m, nameof(ModProjectile.CanDamage), e); }
        return null;
    }

    public static bool MinionContactDamage(Projectile projectile)
    {
        if (InstanceOf(projectile) is ModProjectile m)
        {
            try
            {
                if (m.MinionContactDamage()) return true;
            }
            catch (Exception e) { Report(m, nameof(ModProjectile.MinionContactDamage), e); }
        }
        foreach (GlobalProjectile? g in GlobalsOf(projectile))
        {
            if (g == null) continue;
            try
            {
                if (g.MinionContactDamage(projectile)) return true;
            }
            catch (Exception e) { Report(g, nameof(GlobalProjectile.MinionContactDamage), e); }
        }
        return false;
    }

    public static void ModifyDamageHitbox(Projectile projectile, ref Rectangle hitbox)
    {
        if (InstanceOf(projectile) is ModProjectile m)
        {
            try { m.ModifyDamageHitbox(ref hitbox); }
            catch (Exception e) { Report(m, nameof(ModProjectile.ModifyDamageHitbox), e); }
        }
        foreach (GlobalProjectile? g in GlobalsOf(projectile))
        {
            if (g == null) continue;
            try { g.ModifyDamageHitbox(projectile, ref hitbox); }
            catch (Exception e) { Report(g, nameof(GlobalProjectile.ModifyDamageHitbox), e); }
        }
    }

    public static bool? CanHitNPC(Projectile projectile, NPC target)
    {
        bool? flag = null;
        foreach (GlobalProjectile? g in GlobalsOf(projectile))
        {
            if (g == null) continue;
            bool? canHit = null;
            try { canHit = g.CanHitNPC(projectile, target); }
            catch (Exception e) { Report(g, nameof(GlobalProjectile.CanHitNPC), e); }
            if (canHit == false) return false;
            if (canHit.HasValue) flag = canHit.Value;
        }
        if (InstanceOf(projectile) is ModProjectile m)
        {
            bool? canHit = null;
            try { canHit = m.CanHitNPC(target); }
            catch (Exception e) { Report(m, nameof(ModProjectile.CanHitNPC), e); }
            if (canHit == false) return false;
            if (canHit.HasValue) flag = canHit.Value;
        }
        return flag;
    }

    public static void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
    {
        if (InstanceOf(projectile) is ModProjectile m)
        {
            try { m.ModifyHitNPC(target, ref modifiers); }
            catch (Exception e) { Report(m, nameof(ModProjectile.ModifyHitNPC), e); }
        }
        foreach (GlobalProjectile? g in GlobalsOf(projectile))
        {
            if (g == null) continue;
            try { g.ModifyHitNPC(projectile, target, ref modifiers); }
            catch (Exception e) { Report(g, nameof(GlobalProjectile.ModifyHitNPC), e); }
        }
    }

    public static void OnHitNPC(Projectile projectile, NPC target, in NPC.HitInfo hit, int damageDone)
    {
        if (InstanceOf(projectile) is ModProjectile m)
        {
            try { m.OnHitNPC(target, hit, damageDone); }
            catch (Exception e) { Report(m, nameof(ModProjectile.OnHitNPC), e); }
        }
        foreach (GlobalProjectile? g in GlobalsOf(projectile))
        {
            if (g == null) continue;
            try { g.OnHitNPC(projectile, target, hit, damageDone); }
            catch (Exception e) { Report(g, nameof(GlobalProjectile.OnHitNPC), e); }
        }
    }

    public static bool CanHitPvp(Projectile projectile, Player target)
    {
        foreach (GlobalProjectile? g in GlobalsOf(projectile))
        {
            if (g == null) continue;
            try
            {
                if (!g.CanHitPvp(projectile, target)) return false;
            }
            catch (Exception e) { Report(g, nameof(GlobalProjectile.CanHitPvp), e); }
        }
        if (InstanceOf(projectile) is not ModProjectile m) return true;
        try { return m.CanHitPvp(target); }
        catch (Exception e) { Report(m, nameof(ModProjectile.CanHitPvp), e); }
        return true;
    }

    public static bool CanHitPlayer(Projectile projectile, Player target)
    {
        foreach (GlobalProjectile? g in GlobalsOf(projectile))
        {
            if (g == null) continue;
            try
            {
                if (!g.CanHitPlayer(projectile, target)) return false;
            }
            catch (Exception e) { Report(g, nameof(GlobalProjectile.CanHitPlayer), e); }
        }
        if (InstanceOf(projectile) is not ModProjectile m) return true;
        try { return m.CanHitPlayer(target); }
        catch (Exception e) { Report(m, nameof(ModProjectile.CanHitPlayer), e); }
        return true;
    }

    public static void ModifyHitPlayer(Projectile projectile, Player target, ref Player.HurtModifiers modifiers)
    {
        if (InstanceOf(projectile) is ModProjectile m)
        {
            try { m.ModifyHitPlayer(target, ref modifiers); }
            catch (Exception e) { Report(m, nameof(ModProjectile.ModifyHitPlayer), e); }
        }
        foreach (GlobalProjectile? g in GlobalsOf(projectile))
        {
            if (g == null) continue;
            try { g.ModifyHitPlayer(projectile, target, ref modifiers); }
            catch (Exception e) { Report(g, nameof(GlobalProjectile.ModifyHitPlayer), e); }
        }
    }

    public static void OnHitPlayer(Projectile projectile, Player target, in Player.HurtInfo hurtInfo)
    {
        if (InstanceOf(projectile) is ModProjectile m)
        {
            try { m.OnHitPlayer(target, hurtInfo); }
            catch (Exception e) { Report(m, nameof(ModProjectile.OnHitPlayer), e); }
        }
        foreach (GlobalProjectile? g in GlobalsOf(projectile))
        {
            if (g == null) continue;
            try { g.OnHitPlayer(projectile, target, hurtInfo); }
            catch (Exception e) { Report(g, nameof(GlobalProjectile.OnHitPlayer), e); }
        }
    }

    public static bool? Colliding(Projectile projectile, Rectangle projHitbox, Rectangle targetHitbox)
    {
        foreach (GlobalProjectile? g in GlobalsOf(projectile))
        {
            if (g == null) continue;
            bool? colliding = null;
            try { colliding = g.Colliding(projectile, projHitbox, targetHitbox); }
            catch (Exception e) { Report(g, nameof(GlobalProjectile.Colliding), e); }
            if (colliding.HasValue) return colliding.Value;
        }
        if (InstanceOf(projectile) is not ModProjectile m) return null;
        try { return m.Colliding(projHitbox, targetHitbox); }
        catch (Exception e) { Report(m, nameof(ModProjectile.Colliding), e); }
        return null;
    }

    public static Color? GetAlpha(Projectile projectile, Color lightColor)
    {
        foreach (GlobalProjectile? g in GlobalsOf(projectile))
        {
            if (g == null) continue;
            Color? color = null;
            try { color = g.GetAlpha(projectile, lightColor); }
            catch (Exception e) { Report(g, nameof(GlobalProjectile.GetAlpha), e); }
            if (color.HasValue) return color;
        }
        if (InstanceOf(projectile) is not ModProjectile m) return null;
        try { return m.GetAlpha(lightColor); }
        catch (Exception e) { Report(m, nameof(ModProjectile.GetAlpha), e); }
        return null;
    }

    public static bool PreDraw(Projectile projectile, Player player, ref Color lightColor)
    {
        bool result = true;
        foreach (GlobalProjectile? g in GlobalsOf(projectile))
        {
            if (g == null) continue;
            try { result &= g.PreDraw(projectile, player, ref lightColor); }
            catch (Exception e) { Report(g, nameof(GlobalProjectile.PreDraw), e); }
        }
        if (result && InstanceOf(projectile) is ModProjectile m)
        {
            try { return m.PreDraw(player, ref lightColor); }
            catch (Exception e) { Report(m, nameof(ModProjectile.PreDraw), e); }
        }
        return result;
    }

    public static void PostDraw(Projectile projectile, Player player, Color lightColor)
    {
        if (InstanceOf(projectile) is ModProjectile m)
        {
            try { m.PostDraw(player, lightColor); }
            catch (Exception e) { Report(m, nameof(ModProjectile.PostDraw), e); }
        }
        foreach (GlobalProjectile? g in GlobalsOf(projectile))
        {
            if (g == null) continue;
            try { g.PostDraw(projectile, player, lightColor); }
            catch (Exception e) { Report(g, nameof(GlobalProjectile.PostDraw), e); }
        }
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
