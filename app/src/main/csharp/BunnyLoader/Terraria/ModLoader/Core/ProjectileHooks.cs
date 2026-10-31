using System;
using System.Collections.Generic;
using BunnyLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static unsafe class ProjectileHooks
{
    sealed class Movement
    {
        public Projectile Projectile;
        public Vector2 Velocity, Anchor;
        public int Width, Height;
        public bool Reported, PendingKill, Kept, HasCollision, HasStyle, Bypass;
        public Movement? Outer;
    }

    static readonly HashSet<string> GlobalHooks = new();
    static readonly Dictionary<Type, HashSet<string>> ModHooks = new();
    static Movement? moving;
    static int spawning;
    static (IntPtr Projectile, Color Light)? drawLight;

    internal static void Install(Type type, Type baseType)
    {
        bool Has(string name) => ItemHooks.Overrides(type, baseType, name);
        lock (GlobalHooks)
        {
            HashSet<string> names = baseType == typeof(GlobalProjectile) ? GlobalHooks : ModHooks.TryGetValue(type, out var found) ? found : ModHooks[type] = new();
            foreach (var method in baseType.GetMethods())
                if (method.DeclaringType == baseType && Has(method.Name)) names.Add(method.Name);
        }
        if (Has(nameof(ModProjectile.OnSpawn)))
            ItemHooks.Once("ProjectileSpawn", () =>
            {
                On_Projectile.NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float_NewProjectileModifier += NewProjectile;
                On_Projectile.NewProjectile_IEntitySource_Vector2_Vector2_int_int_float_int_float_float_float_NewProjectileModifier += NewProjectile;
            });
        if (baseType == typeof(ModProjectile) || Has(nameof(ModProjectile.PreAI)) || Has(nameof(ModProjectile.AI)) || Has(nameof(ModProjectile.PostAI)))
            ItemHooks.Once("ProjectileAI", () => On_Projectile.AI += AI);
        if (Has(nameof(ModProjectile.OnTileCollide)) || Has(nameof(ModProjectile.TileCollideStyle)) || Has(nameof(ModProjectile.PreKill)) ||
            Has(nameof(ModProjectile.OnKill)) || Has(nameof(ModProjectile.ShouldUpdatePosition)))
            ItemHooks.Once("ProjectileMovement", () =>
            {
                On_Projectile.HandleMovement += HandleMovement;
                On_Projectile.UpdatePosition += UpdatePosition;
                On_Projectile.Kill += Kill;
            });
        if (Has(nameof(ModProjectile.TileCollideStyle))) ItemHooks.Once("ProjectileCollideStyle", () => On_Projectile.GetCollisionParams += CollisionParams);
        if (Has(nameof(ModProjectile.CanCutTiles)) || Has(nameof(ModProjectile.CutTiles))) ItemHooks.Once("ProjectileCanCut", () => On_Projectile.CanCutTiles += CanCutTiles);
        if (Has(nameof(ModProjectile.CutTiles))) ItemHooks.Once("ProjectileCut", () => On_Projectile.CutTiles += CutTiles);
        if (Has(nameof(ModProjectile.Colliding))) ItemHooks.Once("ProjectileColliding", () => On_Projectile.Colliding += Colliding);
        if (Has(nameof(ModProjectile.CanDamage)) || Has(nameof(ModProjectile.MinionContactDamage))) ItemHooks.Once("ProjectileDamage", () => On_Projectile.Damage += Damage);
        if (Has(nameof(ModProjectile.ModifyDamageHitbox))) ItemHooks.Once("ProjectileHitbox", () => On_Projectile.Damage_GetHitbox += DamageHitbox);
        if (Has(nameof(ModProjectile.GetAlpha)) || Has(nameof(ModProjectile.PreDraw))) ItemHooks.Once("ProjectileAlpha", () => On_Projectile.GetAlpha += GetAlpha);
        if (Has(nameof(ModProjectile.PreDraw)) || Has(nameof(ModProjectile.PostDraw)) || Has(nameof(ModProjectile.PreDrawExtras)))
            ItemHooks.Once("ProjectileDraw", () => On_Main.DrawProjDirect += DrawProjDirect);
        ProjectileHitHooks.Install(Has);
        ProjectileGrappleHooks.Install(Has);
    }

    internal static bool Wants(Projectile projectile, string method)
    {
        if (ProjectileLoader.InstanceOf(projectile) is ModProjectile m && ModHooks.TryGetValue(m.GetType(), out var names) && names.Contains(method)) return true;
        return GlobalHooks.Contains(method) && ProjectileLoader.Globals.Slots(projectile) != null;
    }

    static void Spawned(int index, IEntitySource source)
    {
        if (spawning > 0 || (uint)index >= 1000u) return;
        Projectile projectile = Main.projectile[index];
        if (Wants(projectile, nameof(ModProjectile.OnSpawn))) ProjectileLoader.OnSpawn(projectile, source);
    }

    static int NewProjectile(On_Projectile.orig_NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float_NewProjectileModifier orig,
        IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)
    {
        int index;
        spawning++;
        try { index = orig(spawnSource, X, Y, SpeedX, SpeedY, Type, Damage, KnockBack, Owner, ai0, ai1, ai2, modifer); }
        finally { spawning--; }
        Spawned(index, spawnSource);
        return index;
    }

    static int NewProjectile(On_Projectile.orig_NewProjectile_IEntitySource_Vector2_Vector2_int_int_float_int_float_float_float_NewProjectileModifier orig,
        IEntitySource spawnSource, Vector2 position, Vector2 velocity, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)
    {
        int index;
        spawning++;
        try { index = orig(spawnSource, position, velocity, Type, Damage, KnockBack, Owner, ai0, ai1, ai2, modifer); }
        finally { spawning--; }
        Spawned(index, spawnSource);
        return index;
    }

    static void AI(On_Projectile.orig_AI orig, Projectile self)
    {
        if (!ProjectileLoader.HasHooks(self))
        {
            orig(self);
            return;
        }
        if (ProjectileLoader.PreAI(self))
        {
            ModProjectile? instance = ProjectileLoader.InstanceOf(self);
            int aiType = instance?.AIType ?? 0;
            if (aiType > 0)
            {
                int type = self.type;
                IntPtr outer = ProjectileLoader.Borrow(self, instance, out ModProjectile? outerInstance);
                self.type = aiType;
                try { orig(self); }
                finally
                {
                    self.type = type;
                    ProjectileLoader.Return(outer, outerInstance);
                }
            }
            else orig(self);
            ProjectileLoader.AI(self);
        }
        ProjectileLoader.PostAI(self);
    }

    static bool Collided(Projectile p, Movement move) => p.velocity.X != move.Velocity.X || p.velocity.Y != move.Velocity.Y;

    static void ReportCollision(Projectile p, Movement move)
    {
        move.Reported = true;
        bool allowed = ProjectileLoader.OnTileCollide(p, move.Velocity);
        if (!move.PendingKill) return;
        if (!allowed) move.Kept = true;
        else if (p.active) p.Kill();
    }

    static void HandleMovement(On_Projectile.orig_HandleMovement orig, Projectile self, Vector2 wetVelocity)
    {
        bool collision = Wants(self, nameof(ModProjectile.OnTileCollide)), style = Wants(self, nameof(ModProjectile.TileCollideStyle));
        if (!self.tileCollide || !collision && !style)
        {
            orig(self, wetVelocity);
            return;
        }
        float startX = self.position.X, startY = self.position.Y;
        var move = new Movement { Projectile = self, Velocity = new Vector2(self.velocity.X, self.velocity.Y), Outer = moving, HasCollision = collision };
        bool manual = self.decidesManualFallThrough, should = self.shouldFallThrough;
        if (style) ApplyCollideStyle(self, move);
        moving = move;
        try
        {
            orig(self, wetVelocity);
            if (collision && !move.Reported && self.active && (move.PendingKill || self.tileCollide && !move.Bypass && Collided(self, move))) ReportCollision(self, move);
            if (move.Kept && self.active) self.position = new Vector2(startX + self.velocity.X, startY + self.velocity.Y);
        }
        finally
        {
            moving = move.Outer;
            if (style)
            {
                self.decidesManualFallThrough = manual;
                self.shouldFallThrough = should;
                if (move.Bypass) self.tileCollide = true;
            }
        }
    }

    static void ApplyCollideStyle(Projectile self, Movement move)
    {
        self.GetCollisionParams(out Vector2 anchor, out int width, out int height);
        bool fallThrough = VanillaFallThrough(self);
        move.Bypass = !ProjectileLoader.TileCollideStyle(self, ref width, ref height, ref fallThrough, ref anchor);
        move.HasStyle = true;
        move.Anchor = anchor;
        move.Width = width;
        move.Height = height;
        self.decidesManualFallThrough = true;
        self.shouldFallThrough = fallThrough;
        if (move.Bypass) self.tileCollide = false;
    }

    static void CollisionParams(On_Projectile.orig_GetCollisionParams orig, Projectile self, out Vector2 resizeAnchor, out int colWidth, out int colHeight)
    {
        orig(self, out resizeAnchor, out colWidth, out colHeight);
        Movement? move = moving;
        if (move == null || move.Projectile != self || !move.HasStyle) return;
        resizeAnchor = move.Anchor;
        colWidth = move.Width;
        colHeight = move.Height;
    }

    static bool VanillaFallThrough(Projectile p)
    {
        if (p.decidesManualFallThrough) return p.shouldFallThrough;
        int style = p.aiStyle, type = p.type;
        bool fall = true;
        if (Main.projPet[type])
        {
            Player owner = Main.player[p.owner];
            fall = owner.position.Y + owner.height - 12 > p.position.Y + p.height;
        }
        if (type is 500 or 653 or 1018)
        {
            Player owner = Main.player[p.owner];
            fall = owner.position.Y + owner.height > p.position.Y + p.height + 4;
        }
        fall = ((fall && type != 378) || style == 62 || style == 66 || type == 317 || type == 373) && type != 281 && type != 253 && style != 53 || style == 197;
        if (type is 9 or 12 or 13 or 15 or 24 or 663 or 665 or 667 or 677 or 678 or 679 or 688 or 689 or 690 or 691 or 692 or 693 or 1037 or 1049 or 1105) fall = false;
        if (style == 10 && ProjectileID.Sets.FallingBlockDoesNotFallThroughPlatforms[type]) fall = false;
        if (style == 99 && p.ai[0] == -2f) fall = false;
        if (type == 759) fall = true;
        if (type == 1020)
        {
            float rotation = p.rotation % (MathF.PI * 2f);
            if (rotation >= MathF.PI) rotation -= MathF.PI * 2f;
            if (rotation < -MathF.PI) rotation += MathF.PI * 2f;
            fall = rotation >= MathF.PI / 2f || rotation < -MathF.PI / 2f;
        }
        return fall;
    }

    static void UpdatePosition(On_Projectile.orig_UpdatePosition orig, Projectile self, Vector2 wetVelocity)
    {
        Movement? move = moving;
        if (move != null && move.Projectile == self)
        {
            if (move.Bypass) self.tileCollide = true;
            else if (move.HasCollision && !move.Reported && self.active && self.tileCollide && Collided(self, move)) ReportCollision(self, move);
        }
        if (Wants(self, nameof(ModProjectile.ShouldUpdatePosition)) && !ProjectileLoader.ShouldUpdatePosition(self)) return;
        orig(self, wetVelocity);
    }

    static bool CanCutTiles(On_Projectile.orig_CanCutTiles orig, Projectile self)
    {
        if (!Wants(self, nameof(ModProjectile.CanCutTiles))) return orig(self);
        return ProjectileLoader.CanCutTiles(self) ?? orig(self);
    }

    static void CutTiles(On_Projectile.orig_CutTiles orig, Projectile self)
    {
        if (!Wants(self, nameof(ModProjectile.CutTiles)))
        {
            orig(self);
            return;
        }
        if (!self.CanCutTiles()) return;
        orig(self);
        ProjectileLoader.CutTiles(self);
    }

    static void Kill(On_Projectile.orig_Kill orig, Projectile self)
    {
        if (!self.active || !ProjectileLoader.HasHooks(self))
        {
            orig(self);
            return;
        }
        Movement? move = moving;
        if (move != null && move.Projectile == self && move.HasCollision && !move.Reported && self.tileCollide && Collided(self, move))
        {
            move.PendingKill = true;
            return;
        }
        int timeLeft = self.timeLeft;
        if (!ProjectileLoader.PreKill(self, timeLeft))
        {
            self.active = false;
            return;
        }
        ProjectileLoader.OnKill(self, timeLeft);
        orig(self);
    }

    static bool Colliding(On_Projectile.orig_Colliding orig, Projectile self, Rectangle myRect, Rectangle targetRect)
    {
        if (ProjectileLoader.HasHooks(self) && ProjectileLoader.Colliding(self, myRect, targetRect) is bool result) return result;
        return orig(self, myRect, targetRect);
    }

    static void Damage(On_Projectile.orig_Damage orig, Projectile self)
    {
        if (!ProjectileLoader.HasHooks(self))
        {
            orig(self);
            return;
        }
        if (ProjectileLoader.CanDamage(self) == false) return;
        int type = self.type;
        bool pet = Main.projPet[type];
        if (!pet || !ProjectileLoader.MinionContactDamage(self))
        {
            orig(self);
            return;
        }
        Main.projPet[type] = false;
        try { orig(self); }
        finally { Main.projPet[type] = pet; }
    }

    static Rectangle DamageHitbox(On_Projectile.orig_Damage_GetHitbox orig, Projectile self)
    {
        Rectangle box = orig(self);
        if (ProjectileLoader.HasHooks(self)) ProjectileLoader.ModifyDamageHitbox(self, ref box);
        return box;
    }

    static Color GetAlpha(On_Projectile.orig_GetAlpha orig, Projectile self, Color newColor)
    {
        if (drawLight is { } light && light.Projectile == self.Pointer) newColor = light.Light;
        if (Wants(self, nameof(ModProjectile.GetAlpha)) && ProjectileLoader.GetAlpha(self, newColor) is Color result) return result;
        return orig(self, newColor);
    }

    static void DrawProjDirect(On_Main.orig_DrawProjDirect orig, Main self, Projectile proj, Player overridePlayer)
    {
        bool draw = Wants(proj, nameof(ModProjectile.PreDraw)) || Wants(proj, nameof(ModProjectile.PostDraw));
        bool wantsExtras = Wants(proj, nameof(ModProjectile.PreDrawExtras));
        if (!draw && !wantsExtras)
        {
            orig(self, proj, overridePlayer);
            return;
        }
        Player player = overridePlayer.IsNull ? Main.player[proj.owner] : overridePlayer;
        bool extras = !wantsExtras || ProjectileLoader.PreDrawExtras(proj, player);
        Vector2 center = proj.Center;
        Color light = float.IsFinite(center.X) && float.IsFinite(center.Y) ? Lighting.GetColor((int)(center.X / 16f), (int)(center.Y / 16f)) : Color.White;
        Color original = light;
        bool sprite = !draw || ProjectileLoader.PreDraw(proj, player, ref light);
        bool split = sprite != extras && HasExtras(proj) &&
            Host.Native()->BeginProjectileDrawSplit(TextureAssets.Projectile[proj.type].Value.Pointer, extras ? 1 : 0, sprite ? 1 : 0) != 0;
        if (sprite || split)
        {
            var outer = drawLight;
            if (!light.Equals(original)) drawLight = (proj.Pointer, light);
            try { orig(self, proj, overridePlayer); }
            finally
            {
                drawLight = outer;
                if (split) Host.Native()->EndProjectileDrawSplit();
            }
        }
        if (draw) ProjectileLoader.PostDraw(proj, player, light);
    }

    static bool HasExtras(Projectile proj) => proj.bobber || proj.aiStyle is 7 or 13 or 15 or 99;
}
