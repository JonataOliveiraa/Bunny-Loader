using System;
using System.Collections.Generic;
using BunnyLoader;
using Microsoft.Xna.Framework;
using Terraria.ModLoader.Core;

namespace Terraria.Graphics.CameraModifiers;

public interface IModCameraModifier
{
    string UniqueIdentity { get; }
    bool IsAScreenShake { get; }
    bool Finished { get; }
    void Update(ref CameraInfo cameraInfo);
}

public readonly partial struct CameraModifierStack
{
    static readonly EntityInstances<List<IModCameraModifier>> Managed = new();

    public void Add(IModCameraModifier modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        ItemHooks.Once("ManagedCameraModifiers", () => On_CameraModifierStack.ApplyTo += ApplyManaged);
        var list = Managed.Get(Pointer);
        if (list == null) Managed.Set(Pointer, list = new());
        if (modifier.UniqueIdentity != null) list.RemoveAll(m => m.UniqueIdentity == modifier.UniqueIdentity);
        list.Add(modifier);
    }

    static void ApplyManaged(On_CameraModifierStack.orig_ApplyTo orig, CameraModifierStack self, ref Vector2 cameraPosition)
    {
        Vector2 original = cameraPosition;
        orig(self, ref cameraPosition);
        var list = Managed.Get(self.Pointer);
        if (list == null) return;
        CameraInfo info = CameraInfo.New(original);
        info.CameraPosition = cameraPosition;
        foreach (var modifier in list.ToArray())
        {
            try { modifier.Update(ref info); }
            catch (Exception e) { ItemHooks.Report(modifier.GetType().FullName ?? "CameraModifier", e); list.Remove(modifier); }
        }
        list.RemoveAll(m => m.Finished);
        cameraPosition = info.CameraPosition;
    }
}
