using System;
using System.Collections.Generic;
using BunnyLoader;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader.Core;

namespace Terraria.Graphics.Renderers;

public interface IModParticle
{
    bool ShouldBeRemovedFromRenderer { get; }
    void Update(ref ParticleRendererSettings settings);
    void Draw(ref ParticleRendererSettings settings, SpriteBatch spriteBatch);
}

public readonly partial struct ParticleRenderer
{
    static readonly EntityInstances<List<IModParticle>> Managed = new();

    public void Add(IModParticle particle)
    {
        ArgumentNullException.ThrowIfNull(particle);
        ItemHooks.Once("ManagedParticles", () =>
        {
            On_ParticleRenderer.Update += UpdateManaged;
            On_ParticleRenderer.Draw += DrawManaged;
            On_ParticleRenderer.Clear += ClearManaged;
        });
        var list = Managed.Get(Pointer);
        if (list == null) Managed.Set(Pointer, list = new());
        list.Add(particle);
    }

    static void UpdateManaged(On_ParticleRenderer.orig_Update orig, ParticleRenderer self)
    {
        orig(self);
        var list = Managed.Get(self.Pointer);
        if (list == null) return;
        foreach (var particle in list.ToArray())
        {
            try { particle.Update(ref self.Settings); }
            catch (Exception e) { ItemHooks.Report(particle.GetType().FullName ?? "Particle", e); list.Remove(particle); }
        }
        list.RemoveAll(p => p.ShouldBeRemovedFromRenderer);
    }

    static void DrawManaged(On_ParticleRenderer.orig_Draw orig, ParticleRenderer self, SpriteBatch batch)
    {
        orig(self, batch);
        var list = Managed.Get(self.Pointer);
        if (list == null) return;
        foreach (var particle in list.ToArray())
        {
            try { particle.Draw(ref self.Settings, batch); }
            catch (Exception e) { ItemHooks.Report(particle.GetType().FullName ?? "Particle", e); list.Remove(particle); }
        }
    }

    static void ClearManaged(On_ParticleRenderer.orig_Clear orig, ParticleRenderer self)
    {
        Managed.Set(self.Pointer, null);
        orig(self);
    }
}
