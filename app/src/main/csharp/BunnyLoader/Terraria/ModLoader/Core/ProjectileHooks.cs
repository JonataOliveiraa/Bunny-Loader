using System;
using System.Collections.Generic;
using System.Threading;
using BunnyLoader;
using BunnyLoader.Hooks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

[Flags]
internal enum ProjectileMethod : ulong
{
    OnSpawn = 1UL << 0, PreAI = 1UL << 1, AI = 1UL << 2, PostAI = 1UL << 3, SendExtraAI = 1UL << 4, ModifyFishingLine = 1UL << 5,
    ShouldUpdatePosition = 1UL << 6, TileCollideStyle = 1UL << 7, OnTileCollide = 1UL << 8, CanCutTiles = 1UL << 9, CutTiles = 1UL << 10,
    PreKill = 1UL << 11, OnKill = 1UL << 12, CanDamage = 1UL << 13, MinionContactDamage = 1UL << 14, ModifyDamageHitbox = 1UL << 15,
    CanHitNPC = 1UL << 16, Colliding = 1UL << 17, GetAlpha = 1UL << 18, PreDrawExtras = 1UL << 19, PreDraw = 1UL << 20, PostDraw = 1UL << 21,
    GrappleRange = 1UL << 22, NumGrappleHooks = 1UL << 23, GrappleRetreatSpeed = 1UL << 24, GrapplePullSpeed = 1UL << 25,
    GrappleTargetPoint = 1UL << 26, GrappleCanLatchOnTo = 1UL << 27,
}

internal static unsafe class ProjectileHooks
{
    sealed class Movement
    {
        public Projectile Projectile;
        public ModProjectile? Instance;
        public GlobalProjectile?[]? Slots;
        public Vector2 Velocity, Anchor;
        public int Width, Height;
        public bool Reported, PendingKill, Kept, HasCollision, HasStyle, Bypass;
        public Movement? Outer;
    }

    const ProjectileMethod AIMethods = ProjectileMethod.PreAI | ProjectileMethod.AI | ProjectileMethod.PostAI;
    const ProjectileMethod MoveMethods = ProjectileMethod.OnTileCollide | ProjectileMethod.TileCollideStyle;
    const ProjectileMethod KillMethods = ProjectileMethod.PreKill | ProjectileMethod.OnKill;
    const ProjectileMethod DamageMethods = ProjectileMethod.CanDamage | ProjectileMethod.MinionContactDamage;
    const ProjectileMethod SpriteMethods = ProjectileMethod.PreDraw | ProjectileMethod.PostDraw;
    const ProjectileMethod DrawMethods = SpriteMethods | ProjectileMethod.PreDrawExtras;

    static readonly object Gate = new();
    static readonly List<(ProjectileMethod Methods, Action<int> Late)> BorrowGates = new();
    static ProjectileMethod globalMask, anyMask;
    static ProjectileMethod[] borrowedTypes = Array.Empty<ProjectileMethod>();
    static bool[] offsetTypes = Array.Empty<bool>();
    static Movement? moving, spare;
    static int spawning;
    static (IntPtr Projectile, Color Light)? drawLight;

    internal static ProjectileMethod Methods(Type type, Type baseType)
    {
        ProjectileMethod mask = 0;
        foreach (ProjectileMethod method in Enum.GetValues<ProjectileMethod>())
            if (ItemHooks.Overrides(type, baseType, method.ToString())) mask |= method;
        return mask;
    }

    internal static void Install(Type type, Type baseType, ProjectileMethod mask)
    {
        bool Has(string name) => ItemHooks.Overrides(type, baseType, name);
        lock (Gate)
        {
            if (baseType == typeof(GlobalProjectile)) globalMask |= mask;
            anyMask |= mask;
        }
        if (baseType == typeof(ModProjectile))
            ItemHooks.Once("ProjectileOriginalDamage", () =>
            {
                TypeGate<On_Projectile.hook_ApplyStatsFromSource>(0, type => ProjectileLoader.GetProjectile(type) != null);
                On_Projectile.ApplyStatsFromSource += ApplyStatsFromSource;
            });
        if (Has(nameof(ModProjectile.OnSpawn)))
            ItemHooks.Once("ProjectileSpawn", () =>
            {
                On_Projectile.NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float_NewProjectileModifier += NewProjectile;
                On_Projectile.NewProjectile_IEntitySource_Vector2_Vector2_int_int_float_int_float_float_float_NewProjectileModifier += NewProjectile;
            });
        if (baseType == typeof(ModProjectile) || (mask & AIMethods) != 0)
            ItemHooks.Once("ProjectileAI", () =>
            {
                TypeGate<On_Projectile.hook_AI>(0, type => ProjectileLoader.GetProjectile(type) != null || WantsType(type, AIMethods));
                On_Projectile.AI += AI;
            });
        if ((mask & MoveMethods) != 0)
            ItemHooks.Once("ProjectileMovement", () =>
            {
                TypeGate<On_Projectile.hook_HandleMovement>(MoveMethods);
                On_Projectile.HandleMovement += HandleMovement;
            });
        if ((mask & (MoveMethods | ProjectileMethod.ShouldUpdatePosition)) != 0)
            ItemHooks.Once("ProjectilePosition", () =>
            {
                TypeGate<On_Projectile.hook_UpdatePosition>(MoveMethods | ProjectileMethod.ShouldUpdatePosition);
                On_Projectile.UpdatePosition += UpdatePosition;
            });
        if ((mask & (KillMethods | ProjectileMethod.OnTileCollide)) != 0)
            ItemHooks.Once("ProjectileKill", () =>
            {
                TypeGate<On_Projectile.hook_Kill>(KillMethods | ProjectileMethod.OnTileCollide);
                On_Projectile.Kill += Kill;
            });
        if (Has(nameof(ModProjectile.TileCollideStyle)))
            ItemHooks.Once("ProjectileCollideStyle", () =>
            {
                TypeGate<On_Projectile.hook_GetCollisionParams>(ProjectileMethod.TileCollideStyle);
                On_Projectile.GetCollisionParams += CollisionParams;
            });
        if (Has(nameof(ModProjectile.CanCutTiles)))
            ItemHooks.Once("ProjectileCanCut", () =>
            {
                TypeGate<On_Projectile.hook_CanCutTiles>(ProjectileMethod.CanCutTiles);
                On_Projectile.CanCutTiles += CanCutTiles;
            });
        if (Has(nameof(ModProjectile.CutTiles)))
            ItemHooks.Once("ProjectileCut", () =>
            {
                TypeGate<On_Projectile.hook_CutTiles>(ProjectileMethod.CutTiles);
                On_Projectile.CutTiles += CutTiles;
            });
        if (Has(nameof(ModProjectile.Colliding)))
            ItemHooks.Once("ProjectileColliding", () =>
            {
                TypeGate<On_Projectile.hook_Colliding>(ProjectileMethod.Colliding);
                On_Projectile.Colliding += Colliding;
            });
        if ((mask & DamageMethods) != 0)
            ItemHooks.Once("ProjectileDamage", () =>
            {
                TypeGate<On_Projectile.hook_Damage>(DamageMethods);
                On_Projectile.Damage += Damage;
            });
        if (Has(nameof(ModProjectile.ModifyDamageHitbox)))
            ItemHooks.Once("ProjectileHitbox", () =>
            {
                TypeGate<On_Projectile.hook_Damage_GetHitbox>(ProjectileMethod.ModifyDamageHitbox);
                On_Projectile.Damage_GetHitbox += DamageHitbox;
            });
        if ((mask & (ProjectileMethod.GetAlpha | ProjectileMethod.PreDraw)) != 0)
            ItemHooks.Once("ProjectileAlpha", () =>
            {
                TypeGate<On_Projectile.hook_GetAlpha>(ProjectileMethod.GetAlpha | ProjectileMethod.PreDraw, ProjectileMethod.GetAlpha);
                On_Projectile.GetAlpha += GetAlpha;
            });
        if ((mask & DrawMethods) != 0) WantDraw();
        ProjectileExampleHooks.Install(Has);
        ProjectileHitHooks.Install(Has);
        ProjectileGrappleHooks.Install(Has);
        ProjectileNetHooks.Install(Has);
        FishingLineHooks.Install(Has);
    }

    internal static void TypeGate<THook>(int arg, Func<int, bool> wanted) where THook : Delegate
    {
        HookGates.Table<THook>(() => HookGates.Field(arg, Projectile.Class, "type"), () => ProjectileLoader.ProjectileCount);
        HookGates.Want<THook>(wanted);
    }

    static void TypeGate<THook>(ProjectileMethod methods) where THook : Delegate => TypeGate<THook>(methods, methods);

    static void TypeGate<THook>(ProjectileMethod methods, ProjectileMethod borrowed) where THook : Delegate
    {
        lock (Gate) BorrowGates.Add((borrowed, static type => WantLate<THook>(type)));
        TypeGate<THook>(0, type => WantsType(type, methods) || (Borrowed(type) & borrowed) != 0);
    }

    static ProjectileMethod Borrowed(int type)
    {
        ProjectileMethod[] table = Volatile.Read(ref borrowedTypes);
        return (uint)type < (uint)table.Length ? table[type] : 0;
    }

    static void WantBorrowed(int type, ProjectileMethod methods)
    {
        if ((uint)type >= (uint)ProjectileLoader.ProjectileCount || (Borrowed(type) & methods) == methods) return;
        List<Action<int>>? late = null;
        lock (Gate)
        {
            ProjectileMethod[] table = borrowedTypes;
            ProjectileMethod old = (uint)type < (uint)table.Length ? table[type] : 0;
            if ((old & methods) == methods) return;
            var grown = new ProjectileMethod[Math.Max(type + 1, table.Length)];
            Array.Copy(table, grown, table.Length);
            grown[type] = old | methods;
            Volatile.Write(ref borrowedTypes, grown);
            foreach ((ProjectileMethod wanted, Action<int> mark) in BorrowGates)
                if ((wanted & old) == 0 && (wanted & methods) != 0) (late ??= new()).Add(mark);
        }
        if (late == null) return;
        foreach (Action<int> mark in late) mark(type);
    }

    internal static void WantLate<THook>(int type) where THook : Delegate
    {
        Func<int, bool> wanted = value => value == type;
        HookGates.Want<THook>(wanted);
        HookGates.Subscribed(typeof(THook), wanted);
    }

    static bool Marked(bool[] table, int type) => (uint)type < (uint)table.Length && table[type];

    static bool Mark(ref bool[] table, int type)
    {
        bool[] current = table;
        if (Marked(current, type)) return false;
        var grown = new bool[Math.Max(type + 1, Math.Max(current.Length, ProjectileLoader.ProjectileCount))];
        Array.Copy(current, grown, current.Length);
        grown[type] = true;
        Volatile.Write(ref table, grown);
        return true;
    }

    internal static bool WantsType(int type, ProjectileMethod methods) =>
        (globalMask & methods) != 0 || ProjectileLoader.GetProjectile(type) is ModProjectile m && (m.Hooks & methods) != 0;

    internal static bool Wants(Projectile projectile, ProjectileMethod method) => Wanted(projectile, method) != 0;

    internal static ProjectileMethod Wanted(Projectile projectile, ProjectileMethod methods)
    {
        ProjectileMethod found = ProjectileLoader.TemplateOf(projectile) is ModProjectile m ? m.Hooks & methods : 0;
        ProjectileMethod rest = globalMask & methods & ~found;
        if (rest != 0 && ProjectileLoader.Globals.Slots(projectile) != null) found |= rest;
        return found;
    }

    internal static ProjectileMethod Wanted(Projectile projectile, ProjectileMethod methods, out ModProjectile? instance, out GlobalProjectile?[]? slots)
    {
        ProjectileMethod found = 0;
        instance = null;
        if (ProjectileLoader.TemplateOf(projectile) is ModProjectile m && (m.Hooks & methods) != 0)
        {
            found = m.Hooks & methods;
            instance = ProjectileLoader.InstanceOf(projectile);
        }
        slots = (globalMask & methods) != 0 ? ProjectileLoader.Globals.Slots(projectile) : null;
        if (slots != null) found |= globalMask & methods;
        return found;
    }

    static void ApplyStatsFromSource(On_Projectile.orig_ApplyStatsFromSource orig, Projectile self, IEntitySource spawnSource)
    {
        orig(self, spawnSource);
        if (self.originalDamage != 0 || ProjectileLoader.TemplateOf(self) == null) return;
        Item item = BunnyLoader.Interop.GameInterop.IsInstance(spawnSource.Pointer, EntitySource_ItemUse.Class) ? new EntitySource_ItemUse(spawnSource.Pointer).Item : default;
        self.originalDamage = !item.IsNull && item.damage >= 0 ? item.damage : self.damage;
    }

    static void Spawned(int index, IEntitySource source)
    {
        if (spawning > 0 || (uint)index >= 1000u) return;
        Projectile projectile = Main.projectile[index];
        if (Wants(projectile, ProjectileMethod.OnSpawn)) ProjectileLoader.OnSpawn(projectile, source);
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

    static GlobalProjectile?[]? AISlots(Projectile projectile) => (globalMask & AIMethods) != 0 ? ProjectileLoader.Globals.Slots(projectile) : null;

    static void Refresh(Projectile self, ref int type, ref int generation, ref ModProjectile? instance, ref GlobalProjectile?[]? slots)
    {
        int current = ProjectileLoader.Generation;
        if (self.type == type && current == generation) return;
        type = self.type;
        generation = current;
        instance = ProjectileLoader.InstanceOf(self);
        slots = AISlots(self);
    }

    static void AI(On_Projectile.orig_AI orig, Projectile self)
    {
        int type = self.type, generation = ProjectileLoader.Generation;
        ModProjectile? instance = ProjectileLoader.InstanceOf(self);
        GlobalProjectile?[]? slots = AISlots(self);
        if (instance == null && slots == null)
        {
            orig(self);
            return;
        }
        if (ProjectileLoader.PreAI(self, instance, slots))
        {
            Refresh(self, ref type, ref generation, ref instance, ref slots);
            int aiType = instance?.AIType ?? 0;
            if (aiType > 0)
            {
                WantBorrowed(aiType, instance!.Hooks);
                IntPtr outer = ProjectileLoader.Borrow(self, instance, out ModProjectile? outerInstance);
                self.type = aiType;
                try { orig(self); }
                finally
                {
                    self.type = type;
                    ProjectileLoader.Return(outer, outerInstance);
                }
            }
            else if (instance?.UsesExplosiveMarker != true) orig(self);
            Refresh(self, ref type, ref generation, ref instance, ref slots);
            ProjectileLoader.AI(self, instance, slots);
        }
        Refresh(self, ref type, ref generation, ref instance, ref slots);
        ProjectileLoader.PostAI(self, instance, slots);
    }

    static bool Collided(Projectile p, Movement move) => p.velocity.X != move.Velocity.X || p.velocity.Y != move.Velocity.Y;

    static void ReportCollision(Projectile p, Movement move)
    {
        move.Reported = true;
        bool allowed = ProjectileLoader.OnTileCollide(p, move.Velocity, move.Instance, move.Slots);
        if (!move.PendingKill) return;
        if (!allowed) move.Kept = true;
        else if (p.active) p.Kill();
    }

    static void HandleMovement(On_Projectile.orig_HandleMovement orig, Projectile self, Vector2 wetVelocity)
    {
        if (!self.tileCollide)
        {
            orig(self, wetVelocity);
            return;
        }
        ProjectileMethod wanted = Wanted(self, MoveMethods, out ModProjectile? instance, out GlobalProjectile?[]? slots);
        if (wanted == 0)
        {
            orig(self, wetVelocity);
            return;
        }
        bool collision = (wanted & ProjectileMethod.OnTileCollide) != 0, style = (wanted & ProjectileMethod.TileCollideStyle) != 0;
        float startX = self.position.X, startY = self.position.Y;
        Movement move = Rent(self, collision, instance, slots);
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
            move.Projectile = default;
            move.Instance = null;
            move.Slots = null;
            move.Outer = spare;
            spare = move;
        }
    }

    static Movement Rent(Projectile self, bool collision, ModProjectile? instance, GlobalProjectile?[]? slots)
    {
        Movement? move = spare;
        if (move == null) move = new Movement();
        else spare = move.Outer;
        move.Projectile = self;
        move.Instance = instance;
        move.Slots = slots;
        move.Velocity = new Vector2(self.velocity.X, self.velocity.Y);
        move.Anchor = default;
        move.Width = move.Height = 0;
        move.Reported = move.PendingKill = move.Kept = move.HasStyle = move.Bypass = false;
        move.HasCollision = collision;
        move.Outer = moving;
        return move;
    }

    static void ApplyCollideStyle(Projectile self, Movement move)
    {
        self.GetCollisionParams(out Vector2 anchor, out int width, out int height);
        bool fallThrough = VanillaFallThrough(self);
        move.Bypass = !ProjectileLoader.TileCollideStyle(self, ref width, ref height, ref fallThrough, ref anchor, move.Instance, move.Slots);
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
        if ((anyMask & ProjectileMethod.ShouldUpdatePosition) != 0 &&
            Wanted(self, ProjectileMethod.ShouldUpdatePosition, out ModProjectile? instance, out GlobalProjectile?[]? slots) != 0 &&
            !ProjectileLoader.ShouldUpdatePosition(self, instance, slots)) return;
        orig(self, wetVelocity);
    }

    static bool CanCutTiles(On_Projectile.orig_CanCutTiles orig, Projectile self)
    {
        if (Wanted(self, ProjectileMethod.CanCutTiles, out ModProjectile? instance, out GlobalProjectile?[]? slots) == 0) return orig(self);
        return ProjectileLoader.CanCutTiles(self, instance, slots) ?? orig(self);
    }

    static void CutTiles(On_Projectile.orig_CutTiles orig, Projectile self)
    {
        if (Wanted(self, ProjectileMethod.CutTiles, out ModProjectile? instance, out GlobalProjectile?[]? slots) == 0)
        {
            orig(self);
            return;
        }
        if (!self.CanCutTiles()) return;
        orig(self);
        ProjectileLoader.CutTiles(self, instance, slots);
    }

    static void Kill(On_Projectile.orig_Kill orig, Projectile self)
    {
        if (!self.active)
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
        if (Wanted(self, KillMethods, out ModProjectile? instance, out GlobalProjectile?[]? slots) == 0)
        {
            orig(self);
            return;
        }
        int timeLeft = self.timeLeft;
        if (!ProjectileLoader.PreKill(self, timeLeft, instance, slots))
        {
            self.active = false;
            return;
        }
        ProjectileLoader.OnKill(self, timeLeft, instance, slots);
        orig(self);
    }

    static bool Colliding(On_Projectile.orig_Colliding orig, Projectile self, Rectangle myRect, Rectangle targetRect)
    {
        if (Wanted(self, ProjectileMethod.Colliding, out ModProjectile? instance, out GlobalProjectile?[]? slots) != 0 &&
            ProjectileLoader.Colliding(self, myRect, targetRect, instance, slots) is bool result) return result;
        return orig(self, myRect, targetRect);
    }

    static void Damage(On_Projectile.orig_Damage orig, Projectile self)
    {
        if (Wanted(self, DamageMethods, out ModProjectile? instance, out GlobalProjectile?[]? slots) == 0)
        {
            orig(self);
            return;
        }
        if (ProjectileLoader.CanDamage(self, instance, slots) == false) return;
        int type = self.type;
        bool pet = Main.projPet[type];
        if (!pet || !ProjectileLoader.MinionContactDamage(self, instance, slots))
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
        if (Wanted(self, ProjectileMethod.ModifyDamageHitbox, out ModProjectile? instance, out GlobalProjectile?[]? slots) != 0)
            ProjectileLoader.ModifyDamageHitbox(self, ref box, instance, slots);
        return box;
    }

    static Color GetAlpha(On_Projectile.orig_GetAlpha orig, Projectile self, Color newColor)
    {
        if (drawLight is { } light && light.Projectile == self.Pointer) newColor = light.Light;
        if (Wanted(self, ProjectileMethod.GetAlpha, out ModProjectile? instance, out GlobalProjectile?[]? slots) != 0 &&
            ProjectileLoader.GetAlpha(self, newColor, instance, slots) is Color result) return result;
        return orig(self, newColor);
    }

    static readonly Dictionary<int, ReLogic.Content.Asset<Texture2D>> glows = new();

    static void WantDraw() => ItemHooks.Once("ProjectileDraw", () =>
    {
        TypeGate<On_Main.hook_DrawProjDirect>(1, DrawsType);
        On_Main.DrawProjDirect += DrawProjDirect;
    });

    static bool DrawsType(int type) => WantsType(type, DrawMethods) || glows.ContainsKey(type) || Marked(Volatile.Read(ref offsetTypes), type) ||
        type >= ProjectileID.Count && ProjectileID.Sets.IsInteractable[type];

    internal static void WantGlow(ModProjectile projectile)
    {
        if (!ModContent.RequestIfExists<Texture2D>(projectile.GlowTexture, out var glow))
            return;
        glows[projectile.Type] = glow;
        WantDraw();
    }

    internal static void WantInteractable(ModProjectile projectile)
    {
        if (!ProjectileID.Sets.IsInteractable[projectile.Type])
            return;
        ItemHooks.Once("ProjectileInteractable", () =>
        {
            TypeGate<On_Projectile.hook_IsInteractable>(0, type => type >= ProjectileID.Count);
            On_Projectile.IsInteractable += IsInteractable;
            On_Player.PushTouchCursorState += PushTouchCursorState;
        });
        WantDraw();
    }

    static bool drawOffsets;

    [ThreadStatic] static Vector3 drawOffset;

    internal static void WantDrawOffset(ModProjectile projectile)
    {
        int type = projectile.Type;
        if (type <= 0 || Marked(Volatile.Read(ref offsetTypes), type)) return;
        lock (Gate)
        {
            if (!Mark(ref offsetTypes, type)) return;
        }
        drawOffsets = true;
        WantDraw();
        WantLate<On_Main.hook_DrawProjDirect>(type);
    }

    static void Original(On_Main.orig_DrawProjDirect orig, Main self, Projectile proj, Player overridePlayer)
    {
        ModProjectile? m = drawOffsets && proj.type >= ProjectileID.Count ? ProjectileLoader.InstanceOf(proj) : null;
        if (m == null || !m.HasDrawOffset)
        {
            orig(self, proj, overridePlayer);
            return;
        }
        Vector3 previous = drawOffset;
        drawOffset = new Vector3(m.DrawOffsetX + m.DrawOriginOffsetX, m.DrawOriginOffsetX, -m.DrawOriginOffsetY);
        bool shifted = Host.Native()->BeginDrawCapture(TextureAssets.Projectile[proj.type].Value.Pointer, &ShiftByDrawOffset) != 0;
        try { orig(self, proj, overridePlayer); }
        finally
        {
            if (shifted) Host.Native()->EndDrawCapture();
            drawOffset = previous;
        }
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    static int ShiftByDrawOffset(BunnyLoader.Interop.DrawCapture* draw)
    {
        draw->X += drawOffset.X;
        draw->OriginX += drawOffset.Y;
        draw->OriginY += drawOffset.Z;
        return 0;
    }

    static bool IsInteractable(On_Projectile.orig_IsInteractable orig, Projectile self) =>
        self.type >= ProjectileID.Count ? ProjectileID.Sets.IsInteractable[self.type] : orig(self);

    static void PushTouchCursorState(On_Player.orig_PushTouchCursorState orig, Player self)
    {
        orig(self);
        if (!Main.SmartInteractShowingGenuine) return;
        int index = Main.SmartInteractProj;
        var projectiles = Main.projectile;
        if ((uint)index >= (uint)projectiles.Length) return;
        Projectile proj = projectiles[index];
        if (proj.IsNull || !proj.active || proj.type < ProjectileID.Count || !ProjectileID.Sets.IsInteractable[proj.type]) return;
        self.cursorItemIconEnabled = true;
        self.cursorItemIconID = ProjectileID.Sets.InteractableProjectileItemIcons[proj.type];
    }

    static void DrawGlow(Projectile proj)
    {
        if (!glows.TryGetValue(proj.type, out var glow) || ProjectileLoader.InstanceOf(proj) is not ModProjectile mod)
            return;
        Texture2D texture = TextureAssets.Projectile[proj.type].Value;
        int frames = Main.projFrames[proj.type], width = texture.Width, height = texture.Height;
        float originX = (width - proj.width) * 0.5f + proj.width * 0.5f + mod.DrawOriginOffsetX;
        Rectangle source = frames > 1 ? new Rectangle(0, height / frames * proj.frame, width, height / frames - 1) : new Rectangle(0, 0, width, height);
        Vector2 position = proj.position - Main.screenPosition + new Vector2(originX + mod.DrawOffsetX, proj.height / 2 + proj.gfxOffY);
        Vector2 origin = new(originX, proj.height / 2 - mod.DrawOriginOffsetY);
        Main.EntitySpriteDraw(glow.Value, position, source, new Color(250, 250, 250, proj.alpha), proj.rotation, origin, proj.scale, proj.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
    }

    static void DrawProjDirect(On_Main.orig_DrawProjDirect orig, Main self, Projectile proj, Player overridePlayer)
    {
        ProjectileMethod wanted = Wanted(proj, DrawMethods, out ModProjectile? instance, out GlobalProjectile?[]? slots);
        bool draw = (wanted & SpriteMethods) != 0, wantsExtras = (wanted & ProjectileMethod.PreDrawExtras) != 0;
        if (!draw && !wantsExtras && !glows.ContainsKey(proj.type))
        {
            Original(orig, self, proj, overridePlayer);
            return;
        }
        instance ??= ProjectileLoader.InstanceOf(proj);
        Player player = overridePlayer.IsNull ? Main.player[proj.owner] : overridePlayer;
        IntPtr outer = ProjectileLoader.Borrow(proj, instance, out ModProjectile? outerInstance);
        bool tapped = proj.type >= ProjectileID.Count && ProjectileID.Sets.IsInteractable[proj.type] && Main.LocalPlayer.checkProjectilesFrame == XNAUnityRunner._frame;
        bool right = Main.mouseRight, release = Main.mouseRightRelease;
        if (tapped)
        {
            Main.mouseRight = true;
            Main.mouseRightRelease = true;
        }
        try { Draw(orig, self, proj, overridePlayer, player, draw, wantsExtras, instance, slots); }
        finally
        {
            if (tapped)
            {
                Main.mouseRight = right;
                Main.mouseRightRelease = release;
            }
            ProjectileLoader.Return(outer, outerInstance);
        }
    }

    static void Draw(On_Main.orig_DrawProjDirect orig, Main self, Projectile proj, Player overridePlayer, Player player, bool draw, bool wantsExtras,
        ModProjectile? instance, GlobalProjectile?[]? slots)
    {
        int type = proj.type;
        bool extras = !wantsExtras || ProjectileLoader.PreDrawExtras(proj, player, instance, slots);
        bool retyped = wantsExtras && Retyped(proj, type, instance);
        if (extras && retyped && HasExtras(proj))
        {
            if (Host.Native()->BeginProjectileDrawSplit(TextureAssets.Projectile[proj.type].Value.Pointer, 1, 0) != 0)
            {
                try { Original(orig, self, proj, overridePlayer); }
                finally { Host.Native()->EndProjectileDrawSplit(); }
                extras = false;
            }
        }
        Vector2 center = proj.Center;
        Color light = float.IsFinite(center.X) && float.IsFinite(center.Y) ? Lighting.GetColor((int)(center.X / 16f), (int)(center.Y / 16f)) : Color.White;
        Color original = light;
        bool sprite = !draw || ProjectileLoader.PreDraw(proj, player, ref light, instance, slots);
        if (draw) Retyped(proj, type, instance);
        bool split = sprite != extras && HasExtras(proj) &&
            Host.Native()->BeginProjectileDrawSplit(TextureAssets.Projectile[proj.type].Value.Pointer, extras ? 1 : 0, sprite ? 1 : 0) != 0;
        if (sprite || split)
        {
            var outer = drawLight;
            if (!light.Equals(original)) drawLight = (proj.Pointer, light);
            try { Original(orig, self, proj, overridePlayer); }
            finally
            {
                drawLight = outer;
                if (split) Host.Native()->EndProjectileDrawSplit();
            }
            if (sprite)
                DrawGlow(proj);
        }
        if (draw) ProjectileLoader.PostDraw(proj, player, light, instance, slots);
    }

    static bool Retyped(Projectile proj, int type, ModProjectile? instance)
    {
        int current = proj.type;
        if (current == type) return false;
        WantBorrowed(current, (instance?.Hooks ?? 0) | ProjectileMethod.GetAlpha);
        return true;
    }

    static bool HasExtras(Projectile proj) => proj.bobber || proj.aiStyle is 7 or 13 or 15 or 99;
}
