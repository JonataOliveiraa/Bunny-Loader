using System;
using Terraria.GameContent.Tile_Entities;

namespace Terraria.ModLoader.Core;

internal static class ProjectileExampleHooks
{
    internal static void Install(Func<string, bool> has)
    {
        if (has(nameof(ModProjectile.PrepareBombToBlow)))
            ItemHooks.Once("ProjectilePrepareBomb", () => On_Projectile.PrepareBombToBlow += PrepareBombToBlow);
        if (has(nameof(ModProjectile.DisplayDollSettings)))
            ItemHooks.Once("ProjectileDisplayDoll", () => On_Projectile.AI_DisplayDoll += DisplayDoll);
    }

    static void PrepareBombToBlow(On_Projectile.orig_PrepareBombToBlow orig, Projectile self)
    {
        orig(self);
        if (ProjectileLoader.InstanceOf(self) is ModProjectile m)
        {
            try { m.PrepareBombToBlow(); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.PrepareBombToBlow", e); }
        }
        foreach (GlobalProjectile? g in ProjectileLoader.Globals.Slots(self) ?? Array.Empty<GlobalProjectile?>())
        {
            if (g == null) continue;
            try { g.PrepareBombToBlow(self); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.PrepareBombToBlow", e); }
        }
    }

    static void DisplayDoll(On_Projectile.orig_AI_DisplayDoll orig, Projectile self, Player doll, Item weapon, TEDisplayDoll.DisplayDollPose pose, out bool botherDrawing)
    {
        ModProjectile? m = ProjectileLoader.InstanceOf(self);
        int type = self.type;
        int style = self.aiStyle;
        int aiType = m?.AIType > 0 ? m.AIType : type;
        int aiStyle = style;
        bool vanilla = true;
        self.AI_DisplayDoll_Reset(doll);
        if (m != null)
        {
            try { vanilla &= m.DisplayDollSettings(doll, pose, ref aiStyle, ref aiType); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.DisplayDollSettings", e); }
        }
        foreach (GlobalProjectile? g in ProjectileLoader.Globals.Slots(self) ?? Array.Empty<GlobalProjectile?>())
        {
            if (g == null) continue;
            try { vanilla &= g.DisplayDollSettings(self, doll, pose, ref aiStyle, ref aiType); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.DisplayDollSettings", e); }
        }
        botherDrawing = true;
        if (!vanilla) return;
        self.type = aiType;
        self.aiStyle = aiStyle;
        try { orig(self, doll, weapon, pose, out botherDrawing); }
        finally { self.type = type; self.aiStyle = style; }
    }
}
