using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace BunnyLoader;

public static class ScreenEffects
{
    public enum Layer
    {
        World = 0,
        Screen = 1,
    }

    sealed class Registration
    {
        public Mod? Mod;
        public ScreenGraphics.Shader? Shader;
        public bool Distorts;
    }

    static readonly Dictionary<Type, Registration> registered = new();
    static List<ModScreenEffect> active = new();
    static bool submitted;
    static long lastTicks;

    public static bool Enabled { get; set; } = true;

    public static bool HotReload { get; set; }

    public static int Frame { get; private set; }

    public static ScreenStats Stats => ScreenStats.Read();

    public static bool Debug
    {
        get => ScreenDebug.Enabled;
        set => ScreenDebug.Enabled = value;
    }

    public static IReadOnlyList<ModScreenEffect> Active => active.Where(fx => fx.Active).ToList();

    public static IReadOnlyList<Type> Classes
    {
        get
        {
            var types = new List<Type>(ScreenPresets.All);
            lock (registered)
                foreach (Type type in registered.Keys)
                    if (!types.Contains(type)) types.Add(type);
            return types;
        }
    }

    internal static void Register(ModScreenEffect template) => RegistrationOf(template.GetType(), template.Mod);

    static Registration RegistrationOf(Type type, Mod? mod)
    {
        lock (registered)
        {
            if (registered.TryGetValue(type, out Registration? found)) return found;
            if (type.IsAbstract || !typeof(ModScreenEffect).IsAssignableFrom(type))
                throw new ArgumentException($"ScreenEffects: {type.FullName} is not a concrete ModScreenEffect");
            var sample = (ModScreenEffect)Activator.CreateInstance(type)!;
            sample.Mod = mod!;
            sample.SetDefaults();
            var registration = new Registration
            {
                Mod = mod,
                Shader = ResolveShader(type, sample, mod),
                Distorts = type.GetMethod(nameof(ModScreenEffect.DrawDistortion))!.DeclaringType != typeof(ModScreenEffect),
            };
            registered[type] = registration;
            return registration;
        }
    }

    static ScreenGraphics.Shader? ResolveShader(Type type, ModScreenEffect sample, Mod? mod)
    {
        Type? owner = type.GetProperty(nameof(ModScreenEffect.Source))!.GetMethod!.DeclaringType;
        string? source = sample.Source;
        if (owner == type && source != null) return ScreenGraphics.Shader.FromSource(source, type.Name);
        if (sample.Shader != null) return sample.Shader;
        if (mod != null)
        {
            string path = sample.ShaderPath;
            if (path.StartsWith(mod.Name + "/", StringComparison.Ordinal)) path = path[(mod.Name.Length + 1)..];
            string file = path.EndsWith(".frag", StringComparison.OrdinalIgnoreCase) ? path : path + ".frag";
            if (mod.FileExists(file)) return ScreenGraphics.Shader.FromFile(mod, file);
        }
        if (source != null && owner != null && owner != typeof(ModScreenEffect))
            return owner.IsAbstract ? ScreenGraphics.Shader.FromSource(source, owner.Name) : RegistrationOf(owner, null).Shader;
        return null;
    }

    public static T Spawn<T>(Action<T>? setup = null) where T : ModScreenEffect => (T)Spawn(typeof(T), null, null, Wrap(setup));

    public static T Spawn<T>(Vector2 center, Action<T>? setup = null) where T : ModScreenEffect => (T)Spawn(typeof(T), center, null, Wrap(setup));

    public static T Spawn<T>(Player player, Action<T>? setup = null) where T : ModScreenEffect =>
        (T)Spawn(typeof(T), player.Center, () => player.active ? player.Center : null, Wrap(setup));

    public static T Spawn<T>(NPC npc, Action<T>? setup = null) where T : ModScreenEffect
    {
        int type = npc.type;
        return (T)Spawn(typeof(T), npc.Center, () => npc.active && npc.type == type ? npc.Center : null, Wrap(setup));
    }

    public static T Spawn<T>(Projectile projectile, Action<T>? setup = null) where T : ModScreenEffect
    {
        int type = projectile.type;
        return (T)Spawn(typeof(T), projectile.Center, () => projectile.active && projectile.type == type ? projectile.Center : null, Wrap(setup));
    }

    public static ModScreenEffect Spawn(Type type, Vector2? center = null, Action<ModScreenEffect>? setup = null) => Spawn(type, center, null, setup);

    public static ModScreenEffect Spawn(Type type, Player player, Action<ModScreenEffect>? setup = null) =>
        Spawn(type, player.Center, () => player.active ? player.Center : null, setup);

    static Action<ModScreenEffect>? Wrap<T>(Action<T>? setup) where T : ModScreenEffect => setup == null ? null : fx => setup((T)fx);

    static ModScreenEffect Spawn(Type type, Vector2? center, Func<Vector2?>? follow, Action<ModScreenEffect>? setup)
    {
        Registration registration = RegistrationOf(type, null);
        var fx = (ModScreenEffect)Activator.CreateInstance(type)!;
        fx.Mod = registration.Mod!;
        fx.SetDefaults();
        fx.Shader = registration.Shader ?? fx.Shader;
        setup?.Invoke(fx);

        if (fx.Key != null && Find(fx.Key) is ModScreenEffect existing)
        {
            if (existing.GetType() == type)
            {
                Place(existing, center, follow);
                setup?.Invoke(existing);
                existing.Age = 0;
                return existing;
            }
            existing.Kill();
        }

        Place(fx, center, follow);
        fx.OnSpawn();
        active.Add(fx);
        active = active.OrderBy(e => e.Order).ToList();
        Install();
        return fx;
    }

    static void Place(ModScreenEffect fx, Vector2? center, Func<Vector2?>? follow)
    {
        fx.follow = follow;
        fx.Center = center;
    }

    public static ModScreenEffect? Find(string key) => active.FirstOrDefault(fx => fx.Key == key && fx.Active);

    public static bool Kill(string key)
    {
        ModScreenEffect? fx = Find(key);
        fx?.Kill();
        return fx != null;
    }

    public static void KillAll()
    {
        foreach (ModScreenEffect fx in active) fx.Kill();
    }

    public static int Reload()
    {
        int changed = 0;
        foreach (ScreenGraphics.Shader shader in ScreenGraphics.Shader.Files)
        {
            try
            {
                if (shader.Reload()) changed++;
            }
            catch (Exception e) { ItemHooks.Report($"ScreenEffects.Reload {shader.Name}", e); }
        }
        return changed;
    }

    internal static void Install() =>
        ItemHooks.Once("ScreenEffects", () =>
        {
            ScreenEffectsSetting.Restore();
            On_Main.DoDraw += DoDraw;
        });

    static void DoDraw(On_Main.orig_DoDraw orig, Main self, GameTime gameTime)
    {
        orig(self, gameTime);
        try { Tick(); }
        catch (Exception e) { ItemHooks.Report("ScreenEffects (frame)", e); }
    }

    static float Delta()
    {
        long now = Stopwatch.GetTimestamp();
        float elapsed = lastTicks != 0 ? (float)((now - lastTicks) * 60.0 / Stopwatch.Frequency) : 1;
        lastTicks = now;
        if (Main.gamePaused) return 0;
        return Math.Clamp(elapsed, 0, 3);
    }

    static void Clear()
    {
        if (submitted) ScreenFrame.SubmitEmpty();
        submitted = false;
    }

    static void Tick()
    {
        if (active.Count == 0 && ScreenUtils.Tween.Count == 0)
        {
            Clear();
            lastTicks = 0;
            return;
        }

        float delta = Delta();
        Frame++;
        if (HotReload && Frame % 30 == 0) Reload();
        ScreenUtils.Space.BeginFrame(Frame);
        ScreenUtils.Tween.Update(delta);

        if (active.Count == 0)
        {
            Clear();
            return;
        }

        var frame = new ScreenFrame();
        var distortion = new DistortionMap();
        int distortionAt = -1;
        foreach (ModScreenEffect fx in active.ToArray())
        {
            int before = distortion.Count;
            int at = frame.Draws.Count;
            Step(fx, delta, frame, distortion);
            if (before == 0 && distortion.Count > 0) distortionAt = at;
            if (ScreenDebug.Enabled) ScreenDebug.Track(fx, frame.Draws, at);
        }

        ScreenDraw? pass = distortion.Build();
        if (pass != null) frame.Draws.Insert(Math.Max(distortionAt, 0), pass);
        if (ScreenDebug.Enabled) ScreenDebug.Decorate(frame, pass);

        active = active.Where(fx => fx.Active).ToList();
        frame.Submit();
        submitted = true;
    }

    static void Step(ModScreenEffect fx, float delta, ScreenFrame frame, DistortionMap distortion)
    {
        string name = fx.GetType().Name;
        try
        {
            if (fx.Active && fx.follow != null)
            {
                Vector2? at = fx.follow();
                if (at == null) fx.Kill();
                else fx.Center = at;
            }
            if (fx.Active)
            {
                fx.Age += delta;
                fx.Update();
                if (fx.Duration > 0 && fx.Age >= fx.Duration) fx.Kill();
            }
            if (fx.Active && Enabled && fx.ShouldDraw())
            {
                if (RegistrationOf(fx.GetType(), null).Distorts)
                {
                    distortion.Begin(fx);
                    fx.DrawDistortion(distortion);
                }
                fx.Draw(new ScreenCommands(fx, frame));
            }
        }
        catch (Exception e)
        {
            ItemHooks.Report($"screen effect {name}", e);
            fx.Kill();
        }
        if (!fx.Active && !fx.Ended)
        {
            fx.Ended = true;
            try { fx.OnKill(); }
            catch (Exception e) { ItemHooks.Report($"screen effect {name}.OnKill", e); }
        }
    }
}
