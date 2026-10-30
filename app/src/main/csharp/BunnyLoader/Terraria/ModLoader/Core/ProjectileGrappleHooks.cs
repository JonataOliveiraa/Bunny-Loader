using System;
using System.Collections.Generic;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;

namespace Terraria.ModLoader.Core;

internal static unsafe class ProjectileGrappleHooks
{
    const int HookStyle = 7;

    static bool canUse, use;
    [ThreadStatic] static IntPtr firing;

    internal static void Install(Func<string, bool> has)
    {
        if (has(nameof(ModProjectile.CanUseGrapple))) canUse = true;
        if (has(nameof(ModProjectile.UseGrapple))) use = true;
        if (canUse || use)
            ItemHooks.Once("ProjectileGrappleFire", () =>
            {
                On_Player.FireGrapple += FireGrapple;
                On_Projectile.NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float_NewProjectileModifier += NewProjectile;
            });
        if (has(nameof(ModProjectile.GrappleRange)) || has(nameof(ModProjectile.NumGrappleHooks)) || has(nameof(ModProjectile.GrappleRetreatSpeed)))
            ItemHooks.Once("ProjectileGrappleAI", () => On_Projectile.AI_007_GrapplingHooks += GrappleAI);
        if (has(nameof(ModProjectile.GrappleCanLatchOnTo)))
            ItemHooks.Once("ProjectileGrappleLatch", () => On_Projectile.AI_007_GrapplingHooks_CanTileBeLatchedOnTo += CanLatch);
        if (has(nameof(ModProjectile.GrapplePullSpeed)) || has(nameof(ModProjectile.GrappleTargetPoint)))
            ItemHooks.Once("ProjectileGrappleForces", () => On_Player.GetGrapplingForces += GrapplingForces);
    }

    static void FireGrapple(On_Player.orig_FireGrapple orig, Player self, Item grappleItem)
    {
        if (grappleItem.IsNull)
        {
            orig(self, grappleItem);
            return;
        }
        int type = grappleItem.shoot;
        bool? allowed = canUse ? ProjectileLoader.CanUseGrapple(type, self) : null;
        if (allowed == false) return;
        List<Projectile>? hidden = allowed == true ? FlyingOverLimit(type) : null;
        IntPtr outer = firing;
        firing = use ? self.Pointer : IntPtr.Zero;
        try { orig(self, grappleItem); }
        finally
        {
            firing = outer;
            if (hidden != null)
                foreach (Projectile p in hidden) p.aiStyle = HookStyle;
        }
    }

    static List<Projectile>? FlyingOverLimit(int type)
    {
        int flying = 0, latched = 0;
        var mine = new List<Projectile>();
        for (int i = 0; i < 1000; i++)
        {
            Projectile p = Main.projectile[i];
            if (!p.active || p.owner != Main.myPlayer || p.aiStyle != HookStyle) continue;
            if (p.ai[0] == 2f) latched++;
            else
            {
                flying++;
                mine.Add(p);
            }
        }
        if (flying < Math.Max(1, FireLimit(type) - latched)) return null;
        foreach (Projectile p in mine) p.aiStyle = 0;
        return mine;
    }

    static int FireLimit(int type) => type switch
    {
        73 or 652 or 256 => 2,
        165 => 8,
        646 => 4,
        13 or 315 or 331 or 753 or 865 or 935 or (>= 230 and <= 235) => 1,
        _ => 3,
    };

    static int NewProjectile(On_Projectile.orig_NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float_NewProjectileModifier orig,
        IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)
    {
        if (firing != IntPtr.Zero)
        {
            var player = new Player(firing);
            firing = IntPtr.Zero;
            ProjectileLoader.UseGrapple(player, ref Type);
        }
        return orig(spawnSource, X, Y, SpeedX, SpeedY, Type, Damage, KnockBack, Owner, ai0, ai1, ai2, modifer);
    }

    static void GrappleAI(On_Projectile.orig_AI_007_GrapplingHooks orig, Projectile self)
    {
        bool range = ProjectileHooks.Wants(self, nameof(ModProjectile.GrappleRange));
        bool count = ProjectileHooks.Wants(self, nameof(ModProjectile.NumGrappleHooks));
        bool retreat = ProjectileHooks.Wants(self, nameof(ModProjectile.GrappleRetreatSpeed));
        if (!range && !count && !retreat)
        {
            orig(self);
            return;
        }
        Player owner = Main.player[self.owner];
        float state = self.ai[0];
        float distance = 0f;
        if (range && state == 0f && !HasVanillaRange(self.type))
        {
            Vector2 center = owner.MountedCenter;
            float dx = center.X - (self.position.X + self.width * 0.5f), dy = center.Y - (self.position.Y + self.height * 0.5f);
            distance = MathF.Sqrt(dx * dx + dy * dy);
        }
        else range = false;

        int limit = -1;
        List<Projectile>? hidden = null;
        if (count && state == 0f && self.owner == Main.myPlayer)
        {
            int vanilla = HookLimit(self.type), numHooks = vanilla;
            ProjectileLoader.NumGrappleHooks(self, owner, ref numHooks);
            if (numHooks != vanilla)
            {
                limit = numHooks;
                hidden = Latched(self.owner, self.whoAmI);
                foreach (Projectile p in hidden) p.aiStyle = 0;
            }
        }

        float speed = 1f, baseSpeed = 1f;
        if (retreat && state == 1f)
        {
            speed = baseSpeed = RetreatSpeed(self.type);
            ProjectileLoader.GrappleRetreatSpeed(self, owner, ref speed);
        }

        try { orig(self); }
        finally
        {
            if (hidden != null)
                foreach (Projectile p in hidden) p.aiStyle = HookStyle;
        }

        if (!self.active) return;
        if (range && self.ai[0] == 0f && ProjectileLoader.GrappleOutOfRange(distance, self)) self.ai[0] = 1f;
        if (hidden != null && self.ai[0] == 2f) TrimHooks(self.owner, limit);
        if (state == 1f && speed != baseSpeed && self.ai[0] == 1f)
        {
            float scale = speed / baseSpeed;
            self.velocity = new Vector2(self.velocity.X * scale, self.velocity.Y * scale);
        }
    }

    static List<Projectile> Latched(int owner, int except)
    {
        var found = new List<Projectile>();
        for (int i = 0; i < 1000; i++)
        {
            if (i == except) continue;
            Projectile p = Main.projectile[i];
            if (p.active && p.owner == owner && p.aiStyle == HookStyle && p.ai[0] == 2f) found.Add(p);
        }
        return found;
    }

    static void TrimHooks(int owner, int limit)
    {
        int hooks = 0, oldest = -1, oldestTime = 100000;
        for (int i = 0; i < 1000; i++)
        {
            Projectile p = Main.projectile[i];
            if (!p.active || p.owner != owner || p.aiStyle != HookStyle || p.ai[0] != 2f) continue;
            hooks++;
            if (p.timeLeft < oldestTime)
            {
                oldest = i;
                oldestTime = p.timeLeft;
            }
        }
        if (hooks > limit && oldest >= 0) Main.projectile[oldest].Kill();
    }

    static bool HasVanillaRange(int type) => type is 13 or 32 or 73 or 74 or 165 or 256 or 315 or 322 or 331 or 332 or 372 or 396
        or 652 or 865 or 935 or 446 or 753 or (>= 646 and <= 649) or (>= 486 and <= 489) or (>= 230 and <= 235);

    static int HookLimit(int type) => type switch
    {
        165 => 8,
        256 or 372 => 2,
        652 or 73 or 74 => 1,
        >= 646 and <= 649 => 4,
        _ => 3,
    };

    static float RetreatSpeed(int type) => type switch
    {
        32 => 15f,
        73 or 74 or 322 => 22f,
        315 or 446 => 20f,
        >= 230 and <= 235 => 11f + (type - 230) * 0.75f,
        753 => 15f,
        >= 486 and <= 489 => 18f,
        (>= 646 and <= 649) or 652 => 24f,
        332 => 17f,
        _ => 11f,
    };

    static bool CanLatch(On_Projectile.orig_AI_007_GrapplingHooks_CanTileBeLatchedOnTo orig, Projectile self, Tile theTile)
    {
        if (!ProjectileHooks.Wants(self, nameof(ModProjectile.GrappleCanLatchOnTo))) return orig(self, theTile);
        int width = TileWidth(), offset = theTile._tileOffset;
        if (width <= 0 || offset < 0) return orig(self, theTile);
        bool? result = ProjectileLoader.GrappleCanLatchOnTo(self, Main.player[self.owner], offset % width, offset / width);
        return result ?? orig(self, theTile);
    }

    static int TileWidth()
    {
        TileData tiles = Main.tile;
        return tiles.IsNull ? 0 : tiles._width;
    }

    static void GrapplingForces(On_Player.orig_GetGrapplingForces orig, Player self, Vector2 fromPosition,
        out GameNullable<int> preferredPlayerDirectionToSet, out float preferedPlayerVelocityX, out float preferedPlayerVelocityY)
    {
        orig(self, fromPosition, out preferredPlayerDirectionToSet, out preferedPlayerVelocityX, out preferedPlayerVelocityY);
        int count = self.grapCount;
        if (count <= 0) return;
        Projectile first = Main.projectile[self.grappling[0]];
        bool pull = ProjectileHooks.Wants(first, nameof(ModProjectile.GrapplePullSpeed)), target = false;
        for (int i = 0; i < count && !target; i++)
        {
            Projectile hook = Main.projectile[self.grappling[i]];
            target = hook.ai[0] == 2f && ProjectileHooks.Wants(hook, nameof(ModProjectile.GrappleTargetPoint));
        }
        if (!pull && !target) return;

        float x = 0f, y = 0f;
        int? direction = null;
        int hooks = 0;
        for (int i = 0; i < count; i++)
        {
            Projectile projectile = Main.projectile[self.grappling[i]];
            if (projectile.ai[0] != 2f || float.IsNaN(projectile.position.X) || float.IsNaN(projectile.position.Y)) continue;
            x += projectile.position.X + projectile.width / 2;
            y += projectile.position.Y + projectile.height / 2;
            hooks++;
            int aiType = ProjectileLoader.InstanceOf(projectile)?.AIType ?? 0;
            int type = aiType > 0 ? aiType : projectile.type;
            if (type == 446)
            {
                var input = new Vector2(ToInt(self.controlRight) - ToInt(self.controlLeft), (ToInt(self.controlDown) - ToInt(self.controlUp)) * self.gravDir);
                if (input != Vector2.Zero) input = Normalize(input);
                input = new Vector2(input.X * 100f, input.Y * 100f);
                Vector2 away = Normalize(self.Center - projectile.Center + input);
                if (float.IsNaN(away.X) || float.IsNaN(away.Y)) away = new Vector2(0f, -1f);
                x += away.X * 200f;
                y += away.Y * 200f;
            }
            else if (type == 652)
            {
                Vector2 input = new Vector2(ToInt(self.controlRight) - ToInt(self.controlLeft), (ToInt(self.controlDown) - ToInt(self.controlUp)) * self.gravDir).SafeNormalize(Vector2.Zero);
                Vector2 toHook = projectile.Center - self.Center;
                Vector2 along = toHook.SafeNormalize(Vector2.Zero);
                Vector2 push = Vector2.Zero;
                if (input != Vector2.Zero)
                {
                    float dot = along.X * input.X + along.Y * input.Y;
                    push = new Vector2(along.X * dot, along.Y * dot);
                }
                float strength = 6f;
                if (push.X * toHook.X + push.Y * toHook.Y < 0f && MathF.Sqrt(toHook.X * toHook.X + toHook.Y * toHook.Y) >= 600f) strength = 0f;
                x += -toHook.X + push.X * strength;
                y += -toHook.Y + push.Y * strength;
            }
            else if (type == 865)
            {
                float angle = projectile.rotation - MathF.PI / 2f;
                Vector2 facing = new Vector2(MathF.Cos(angle), MathF.Sin(angle)).SafeNormalize(new Vector2(0f, 1f));
                x += -facing.X * 28f;
                y += -facing.Y * 28f;
                if (facing.X != 0f) direction = Math.Sign(facing.X);
            }
            ProjectileLoader.GrappleTargetPoint(projectile, self, ref x, ref y);
        }

        preferredPlayerDirectionToSet = direction;
        if (hooks == 0)
        {
            preferedPlayerVelocityX = self.velocity.X;
            preferedPlayerVelocityY = self.velocity.Y;
            return;
        }
        preferedPlayerVelocityX = x / hooks - fromPosition.X;
        preferedPlayerVelocityY = y / hooks - fromPosition.Y;
        float length = MathF.Sqrt(preferedPlayerVelocityX * preferedPlayerVelocityX + preferedPlayerVelocityY * preferedPlayerVelocityY);
        float speed = PullSpeed(first.type);
        ProjectileLoader.GrapplePullSpeed(first, self, ref speed);
        float scale = length <= speed ? 1f : speed / length;
        preferedPlayerVelocityX *= scale;
        preferedPlayerVelocityY *= scale;
    }

    static float PullSpeed(int type) => type switch
    {
        73 or 74 => 12.5f,
        315 => 14f,
        487 => 12f,
        >= 646 and <= 649 => 16f,
        _ => 11f,
    };

    static int ToInt(bool value) => value ? 1 : 0;

    static Vector2 Normalize(Vector2 v)
    {
        float length = MathF.Sqrt(v.X * v.X + v.Y * v.Y);
        return new Vector2(v.X / length, v.Y / length);
    }
}
