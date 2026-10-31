using System;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;
using Color = Microsoft.Xna.Framework.Graphics.Color;

namespace Terraria.ModLoader.Core;

internal static class GoreHooks
{
    [ThreadStatic] internal static IEntitySource Source;

    internal static void Install(Type type)
    {
        bool Has(string name) => ItemHooks.Overrides(type, typeof(ModGore), name);
        if (Has(nameof(ModGore.OnSpawn))) ItemHooks.Once("GoreSpawn", () => On_Gore.NewGore += NewGore);
        if (Has(nameof(ModGore.GetAlpha))) ItemHooks.Once("GoreAlpha", () => On_Gore.GetAlpha += GetAlpha);
        if (Has(nameof(ModGore.Update))) WantUpdate();
    }

    internal static void WantUpdate() => ItemHooks.Once("GoreUpdate", () => On_Gore.Update += Update);

    static int NewGore(On_Gore.orig_NewGore orig, Vector2 Position, Vector2 Velocity, int Type, float Scale)
    {
        IEntitySource source = Source;
        Source = default;
        int index = orig(Position, Velocity, Type, Scale);
        if ((uint)index >= (uint)Main.gore.Length || GoreLoader.GetModGore(Type) is not ModGore modGore) return index;
        Gore gore = Main.gore[index];
        if (!gore.active || gore.type != Type) return index;
        try { modGore.OnSpawn(gore, source); }
        catch (Exception e) { ItemHooks.Report($"{modGore.FullName}.OnSpawn", e); }
        return index;
    }

    static void Update(On_Gore.orig_Update orig, Gore self)
    {
        if (!self.active || GoreLoader.GetModGore(self.type) is not ModGore modGore)
        {
            orig(self);
            return;
        }
        try
        {
            if (!modGore.Update(self)) return;
        }
        catch (Exception e) { ItemHooks.Report($"{modGore.FullName}.Update", e); }
        if (modGore.UpdateType <= 0)
        {
            orig(self);
            return;
        }
        int type = self.type;
        self.type = modGore.UpdateType;
        try { orig(self); }
        finally
        {
            if (self.type == modGore.UpdateType) self.type = type;
        }
    }

    static Color GetAlpha(On_Gore.orig_GetAlpha orig, Gore self, Color newColor)
    {
        if (GoreLoader.GetModGore(self.type) is ModGore modGore)
        {
            try
            {
                if (modGore.GetAlpha(self, newColor) is Color color) return color;
            }
            catch (Exception e) { ItemHooks.Report($"{modGore.FullName}.GetAlpha", e); }
        }
        return orig(self, newColor);
    }
}
