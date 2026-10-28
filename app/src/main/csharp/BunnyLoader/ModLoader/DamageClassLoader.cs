using System;
using System.Collections.Generic;
using System.Threading;
using BunnyLoader;
using Terraria.ID;

namespace Terraria.ModLoader;

internal static class DamageClassLoader
{
    internal struct Flags
    {
        public bool Melee, Ranged, Magic, Summon, NoSpeed, Whip;
    }

    internal sealed class VanillaSet
    {
        public readonly DamageClass Default = new DefaultDamageClass();
        public readonly DamageClass Generic = new GenericDamageClass();
        public readonly DamageClass Melee = new MeleeDamageClass();
        public readonly DamageClass MeleeNoSpeed = new MeleeNoSpeedDamageClass();
        public readonly DamageClass Ranged = new RangedDamageClass();
        public readonly DamageClass Magic = new MagicDamageClass();
        public readonly DamageClass Summon = new SummonDamageClass();
        public readonly DamageClass SummonMeleeSpeed = new SummonMeleeSpeedDamageClass();
        public readonly DamageClass MagicSummonHybrid = new MagicSummonHybridDamageClass();
        public readonly DamageClass Throwing = new ThrowingDamageClass();

        public DamageClass[] All => new[] { Default, Generic, Melee, MeleeNoSpeed, Ranged, Magic, Summon, SummonMeleeSpeed, MagicSummonHybrid, Throwing };
    }

    internal struct ClassStats
    {
        public StatModifier Damage;
        public float Crit;
        public float Speed;
        public float ArmorPen;
        public StatModifier Knockback;

        public static ClassStats Fresh() => new()
        {
            Damage = StatModifier.Default, Crit = 0f, Speed = 1f, ArmorPen = 0f, Knockback = StatModifier.Default,
        };
    }

    internal struct Totals
    {
        public StatModifier Damage;
        public float Crit;
        public float Speed;
        public float ArmorPen;
        public StatModifier Knockback;
    }

    sealed class PlayerData
    {
        public ClassStats[] Stats = Array.Empty<ClassStats>();
        public ClassStats[] Applied = Array.Empty<ClassStats>();
        public bool HasSnap;
        public float SnapMelee, SnapSummon;
    }

    sealed class Assigned
    {
        public int Type;
        public DamageClass Class = null!;
    }

    sealed class Relations
    {
        public int Count;
        public StatInheritanceData?[][] Inherit = Array.Empty<StatInheritanceData?[]>();
        public bool[][] Counts = Array.Empty<bool[]>();
    }

    static readonly object Gate = new();
    static readonly Dictionary<DamageClass, Flags> Native = new();
    static readonly EntityInstances<Assigned> Items = new();
    static readonly EntityInstances<Assigned> Projectiles = new();
    static readonly EntityInstances<PlayerData> Players = new();

    static DamageClass[] classes = Array.Empty<DamageClass>();
    static Relations? relations;
    static bool installed, itemHooks, projectileHooks, hookedProjectiles;
    static Projectile firing;
    static bool isFiring;

    internal static readonly VanillaSet Vanilla = BuildVanilla();

    public static int DamageClassCount => Volatile.Read(ref classes).Length;

    public static DamageClass? GetDamageClass(int type)
    {
        DamageClass[] all = Volatile.Read(ref classes);
        return (uint)type < (uint)all.Length ? all[type] : null;
    }

    static VanillaSet BuildVanilla()
    {
        var v = new VanillaSet();
        DamageClass[] all = v.All;
        for (int i = 0; i < all.Length; i++)
        {
            all[i].Type = i;
            ModContent.Add(all[i]);
        }
        classes = all;
        Native[v.Default] = new Flags();
        Native[v.Melee] = new Flags { Melee = true };
        Native[v.MeleeNoSpeed] = new Flags { Melee = true, NoSpeed = true };
        Native[v.Ranged] = new Flags { Ranged = true };
        Native[v.Magic] = new Flags { Magic = true };
        Native[v.Summon] = new Flags { Summon = true };
        Native[v.SummonMeleeSpeed] = new Flags { Summon = true, Whip = true };
        return v;
    }

    public static void Register(DamageClass damageClass)
    {
        lock (Gate)
        {
            DamageClass[] all = classes;
            var grown = new DamageClass[all.Length + 1];
            Array.Copy(all, grown, all.Length);
            damageClass.Type = all.Length;
            grown[^1] = damageClass;
            Volatile.Write(ref classes, grown);
            Volatile.Write(ref relations, null);
        }
        Activate();
    }

    public static bool IsHooked(DamageClass damageClass) => !Native.ContainsKey(damageClass);

    public static bool UsesCrit(DamageClass damageClass) =>
        Safe(damageClass, nameof(DamageClass.UseStandardCritCalcs), () => damageClass.UseStandardCritCalcs, true);

    public static bool CountsAs(DamageClass damageClass, DamageClass other)
    {
        if (damageClass == other) return true;
        Relations r = Related();
        return damageClass.Type < r.Count && other.Type < r.Count && r.Counts[damageClass.Type][other.Type];
    }

    static StatInheritanceData? Inheritance(Relations r, DamageClass damageClass, DamageClass other) =>
        damageClass.Type < r.Count && other.Type < r.Count ? r.Inherit[damageClass.Type][other.Type] : null;

    static Relations Related()
    {
        Relations? r = Volatile.Read(ref relations);
        DamageClass[] all = Volatile.Read(ref classes);
        if (r != null && r.Count == all.Length) return r;
        lock (Gate)
        {
            r = relations;
            if (r != null && r.Count == all.Length) return r;
            r = new Relations
            {
                Count = all.Length,
                Inherit = new StatInheritanceData?[all.Length][],
                Counts = new bool[all.Length][],
            };
            for (int i = 0; i < all.Length; i++)
            {
                DamageClass c = all[i];
                r.Inherit[i] = new StatInheritanceData?[all.Length];
                r.Counts[i] = new bool[all.Length];
                for (int j = 0; j < all.Length; j++)
                {
                    DamageClass other = all[j];
                    if (i == j)
                    {
                        r.Counts[i][j] = true;
                        continue;
                    }
                    StatInheritanceData data = Safe(c, nameof(DamageClass.GetModifierInheritance), () => c.GetModifierInheritance(other), StatInheritanceData.None);
                    r.Inherit[i][j] = data.IsNone ? null : data;
                    r.Counts[i][j] = Safe(c, nameof(DamageClass.GetEffectInheritance), () => c.GetEffectInheritance(other), false);
                }
            }
            Volatile.Write(ref relations, r);
            return r;
        }
    }

    internal static T Safe<T>(DamageClass damageClass, string member, Func<T> body, T fallback)
    {
        try
        {
            return body();
        }
        catch (Exception e)
        {
            ItemHooks.Report($"{damageClass.FullName}.{member}", e);
            return fallback;
        }
    }

    public static DamageClass ItemClass(Item item)
    {
        if (item.IsNull) return Vanilla.Default;
        int type = item.type;
        if (Items.Count > 0 && Items.Get(item.Pointer) is Assigned own && own.Type == type) return own.Class;
        return ItemFromFlags(item, type);
    }

    public static void SetItemClass(Item item, DamageClass damageClass)
    {
        if (damageClass == null) throw new ArgumentNullException(nameof(damageClass), "DamageType: use DamageClass.Default for none");
        if (item.IsNull) throw new NullReferenceException("DamageType: the item is null");
        Flags flags = FlagsOf(damageClass);
        item.melee = flags.Melee;
        item.ranged = flags.Ranged;
        item.magic = flags.Magic;
        item.summon = flags.Summon;
        int type = item.type;
        if (type >= ItemID.Count)
        {
            if (flags.NoSpeed) ItemID.Sets.NoMeleeSpeedBonus[type] = true;
            if (flags.Whip) ItemID.Sets.SummonerWeaponThatScalesWithAttackSpeed[type] = true;
        }
        EnsureItemHooks();
        Items.Set(item.Pointer, new Assigned { Type = type, Class = damageClass });
        if (IsHooked(damageClass)) Activate();
    }

    public static DamageClass ProjectileClass(Projectile projectile)
    {
        if (projectile.IsNull) return Vanilla.Default;
        int type = projectile.type;
        if (Projectiles.Count > 0 && Projectiles.Get(projectile.Pointer) is Assigned own && own.Type == type) return own.Class;
        return ProjectileFromFlags(projectile, type);
    }

    public static void SetProjectileClass(Projectile projectile, DamageClass damageClass)
    {
        if (damageClass == null) throw new ArgumentNullException(nameof(damageClass), "DamageType: use DamageClass.Default for none");
        if (projectile.IsNull) throw new NullReferenceException("DamageType: the projectile is null");
        Flags flags = FlagsOf(damageClass);
        projectile.melee = flags.Melee;
        projectile.ranged = flags.Ranged;
        projectile.magic = flags.Magic;
        EnsureProjectileHooks();
        Projectiles.Set(projectile.Pointer, new Assigned { Type = projectile.type, Class = damageClass });
        if (IsHooked(damageClass))
        {
            hookedProjectiles = true;
            Activate();
        }
    }

    static Flags FlagsOf(DamageClass damageClass)
    {
        if (Native.TryGetValue(damageClass, out Flags own)) return own;
        VanillaSet v = Vanilla;
        bool Takes(DamageClass b) => CountsAs(damageClass, b) || damageClass.GetsPrefixesFor(b);
        return new Flags
        {
            Melee = Takes(v.Melee) || Takes(v.MeleeNoSpeed),
            Ranged = Takes(v.Ranged),
            Magic = Takes(v.Magic),
            Summon = Takes(v.Summon) || Takes(v.SummonMeleeSpeed),
        };
    }

    static DamageClass ItemFromFlags(Item item, int type)
    {
        VanillaSet v = Vanilla;
        if (item.melee) return ItemID.Sets.NoMeleeSpeedBonus[type] ? v.MeleeNoSpeed : v.Melee;
        if (item.ranged) return v.Ranged;
        if (item.magic) return v.Magic;
        if (item.summon) return ItemID.Sets.SummonerWeaponThatScalesWithAttackSpeed[type] ? v.SummonMeleeSpeed : v.Summon;
        return v.Default;
    }

    static DamageClass ProjectileFromFlags(Projectile projectile, int type)
    {
        VanillaSet v = Vanilla;
        if (projectile.melee) return v.Melee;
        if (projectile.ranged) return v.Ranged;
        if (projectile.magic) return v.Magic;
        if (ProjectileID.Sets.IsAWhip[type]) return v.SummonMeleeSpeed;
        if (projectile.minion || projectile.sentry || ProjectileID.Sets.MinionShot[type] || ProjectileID.Sets.SentryShot[type]) return v.Summon;
        return v.Default;
    }

    static void EnsureItemHooks()
    {
        lock (Gate)
        {
            if (itemHooks) return;
            itemHooks = true;
        }
        On_Item.ResetStats += (orig, self, type) =>
        {
            orig(self, type);
            if (Items.Count > 0) Items.Set(self.Pointer, null);
        };
        On_Item.Clone += (orig, self) => CopyClass(self, orig(self));
        On_Item.DeepClone += (orig, self) => CopyClass(self, orig(self));
        On_Item.clientClone += (orig, self, cloneDestination) => CopyClass(self, orig(self, cloneDestination));
    }

    static Item CopyClass(Item self, Item copy)
    {
        if (copy.IsNull || copy == self || Items.Count == 0) return copy;
        if (Items.Get(self.Pointer) is Assigned own && own.Type == copy.type)
            Items.Set(copy.Pointer, new Assigned { Type = own.Type, Class = own.Class });
        return copy;
    }

    static void EnsureProjectileHooks()
    {
        lock (Gate)
        {
            if (projectileHooks) return;
            projectileHooks = true;
        }
        On_Projectile.SetDefaults += (orig, self, type) =>
        {
            if (Projectiles.Count > 0) Projectiles.Set(self.Pointer, null);
            orig(self, type);
        };
    }

    static PlayerData Data(Player player)
    {
        PlayerData? d = Players.Get(player.Pointer);
        if (d == null)
        {
            Activate();
            d = new PlayerData();
            Players.Set(player.Pointer, d);
            Reset(player, d);
        }
        else if (d.Stats.Length < DamageClassCount)
        {
            Reset(player, d);
        }
        return d;
    }

    static bool HasData(Player player) => Players.Count > 0 && Players.Get(player.Pointer) != null;

    public static ref ClassStats Stats(Player player, DamageClass damageClass)
    {
        if (damageClass == null) throw new ArgumentNullException(nameof(damageClass));
        if (player.IsNull) throw new NullReferenceException("the player is null");
        return ref Data(player).Stats[damageClass.Type];
    }

    static void Reset(Player player, PlayerData d)
    {
        DamageClass[] all = Volatile.Read(ref classes);
        if (d.Stats.Length != all.Length)
        {
            d.Stats = new ClassStats[all.Length];
            d.Applied = new ClassStats[all.Length];
        }
        for (int i = 0; i < all.Length; i++)
        {
            d.Stats[i] = ClassStats.Fresh();
            d.Applied[i] = ClassStats.Fresh();
        }
        foreach (DamageClass c in all)
        {
            if (c.IsVanilla) continue;
            try
            {
                c.SetDefaultStats(player);
            }
            catch (Exception e)
            {
                ItemHooks.Report($"{c.FullName}.SetDefaultStats", e);
            }
        }
    }

    public static Totals Total(Player player, DamageClass damageClass)
    {
        if (damageClass == null) throw new ArgumentNullException(nameof(damageClass));
        PlayerData d = Data(player);
        Relations r = Related();
        DamageClass[] all = Volatile.Read(ref classes);
        Totals t = Effective(player, d, damageClass);
        foreach (DamageClass other in all)
        {
            if (other == damageClass) continue;
            if (Inheritance(r, damageClass, other) is not StatInheritanceData inh) continue;
            Totals s = Effective(player, d, other);
            t.Damage = t.Damage.CombineWith(s.Damage.Scale(inh.damageInheritance));
            t.Crit += s.Crit * inh.critChanceInheritance;
            t.Speed += (s.Speed - 1f) * inh.attackSpeedInheritance;
            t.ArmorPen += s.ArmorPen * inh.armorPenInheritance;
            t.Knockback = t.Knockback.CombineWith(s.Knockback.Scale(inh.knockbackInheritance));
        }
        return t;
    }

    static Totals Effective(Player player, PlayerData d, DamageClass damageClass)
    {
        VanillaSet v = Vanilla;
        ref ClassStats own = ref d.Stats[damageClass.Type];
        bool folded = damageClass == v.Generic || damageClass == v.Melee || damageClass == v.Ranged || damageClass == v.Magic || damageClass == v.Summon;
        if (!folded)
            return new Totals { Damage = own.Damage, Crit = own.Crit, Speed = own.Speed, ArmorPen = own.ArmorPen, Knockback = own.Knockback };

        float melee = player.meleeDamage, ranged = player.rangedDamage, magic = player.magicDamage, minion = player.minionDamage;
        float common = Math.Min(Math.Min(melee, ranged), Math.Min(magic, minion));
        float commonCrit = Math.Min(Math.Min((float)player.meleeCrit, player.rangedCrit), player.magicCrit);
        float genericSpeed = d.Stats[v.Generic.Type].Speed;
        float add, crit, speed, pen;
        if (damageClass == v.Generic)
        {
            add = common;
            crit = commonCrit;
            speed = genericSpeed;
            pen = player.armorPenetration;
        }
        else if (damageClass == v.Melee)
        {
            add = 1f + melee - common;
            crit = player.meleeCrit - commonCrit;
            speed = d.HasSnap ? d.SnapMelee - genericSpeed + 1f : own.Speed;
            pen = player.meleeArmorPenetration;
        }
        else if (damageClass == v.Ranged)
        {
            add = 1f + ranged - common;
            crit = player.rangedCrit - commonCrit;
            speed = own.Speed;
            pen = own.ArmorPen;
        }
        else if (damageClass == v.Magic)
        {
            add = 1f + magic - common;
            crit = player.magicCrit - commonCrit;
            speed = own.Speed;
            pen = own.ArmorPen;
        }
        else
        {
            add = 1f + minion - common;
            crit = own.Crit;
            speed = d.HasSnap ? 1f + d.SnapSummon : own.Speed;
            pen = own.ArmorPen;
        }
        return new Totals
        {
            Damage = new StatModifier(add, 1f, own.Damage.Flat, own.Damage.Base),
            Crit = crit,
            Speed = speed,
            ArmorPen = pen,
            Knockback = own.Knockback,
        };
    }

    static void Fold(Player player)
    {
        if (Players.Count == 0 || Players.Get(player.Pointer) is not PlayerData d) return;
        VanillaSet v = Vanilla;
        if (Delta(d, v.Generic, out float add, out float mult, out int crit, out float speed, out int pen))
        {
            if (add != 0f || mult != 1f)
            {
                player.meleeDamage = (player.meleeDamage + add) * mult;
                player.rangedDamage = (player.rangedDamage + add) * mult;
                player.magicDamage = (player.magicDamage + add) * mult;
                player.minionDamage = (player.minionDamage + add) * mult;
            }
            if (crit != 0)
            {
                player.meleeCrit += crit;
                player.rangedCrit += crit;
                player.magicCrit += crit;
            }
            if (speed != 0f) player.meleeSpeed += speed;
            if (pen != 0) player.armorPenetration += pen;
        }
        if (Delta(d, v.Melee, out add, out mult, out crit, out speed, out pen))
        {
            if (add != 0f || mult != 1f) player.meleeDamage = (player.meleeDamage + add) * mult;
            if (crit != 0) player.meleeCrit += crit;
            if (speed != 0f) player.meleeSpeed += speed;
            if (pen != 0) player.meleeArmorPenetration += pen;
        }
        if (Delta(d, v.Ranged, out add, out mult, out crit, out _, out _))
        {
            if (add != 0f || mult != 1f) player.rangedDamage = (player.rangedDamage + add) * mult;
            if (crit != 0) player.rangedCrit += crit;
        }
        if (Delta(d, v.Magic, out add, out mult, out crit, out _, out _))
        {
            if (add != 0f || mult != 1f) player.magicDamage = (player.magicDamage + add) * mult;
            if (crit != 0) player.magicCrit += crit;
        }
        if (Delta(d, v.Summon, out add, out mult, out _, out speed, out _))
        {
            if (add != 0f || mult != 1f) player.minionDamage = (player.minionDamage + add) * mult;
            if (speed != 0f) player.summonerWeaponSpeedBonus += speed;
        }
        d.HasSnap = true;
        d.SnapMelee = player.meleeSpeed;
        d.SnapSummon = player.summonerWeaponSpeedBonus;
    }

    static bool Delta(PlayerData d, DamageClass damageClass, out float add, out float mult, out int crit, out float speed, out int pen)
    {
        ref ClassStats cur = ref d.Stats[damageClass.Type];
        ref ClassStats app = ref d.Applied[damageClass.Type];
        add = cur.Damage.Additive - app.Damage.Additive;
        mult = cur.Damage.Multiplicative / app.Damage.Multiplicative;
        crit = RoundHalfUp(cur.Crit - app.Crit);
        speed = cur.Speed - app.Speed;
        pen = RoundHalfUp(cur.ArmorPen - app.ArmorPen);
        app.Damage = cur.Damage;
        app.Crit = cur.Crit;
        app.Speed = cur.Speed;
        app.ArmorPen = cur.ArmorPen;
        return add != 0f || mult != 1f || crit != 0 || speed != 0f || pen != 0;
    }

    internal static int RoundHalfUp(float x) => (int)Math.Floor(x + 0.5f);

    static int ExtraPen(Player player, DamageClass damageClass, bool melee)
    {
        float total = Total(player, damageClass).ArmorPen;
        return RoundHalfUp(total - player.armorPenetration - (melee ? player.meleeArmorPenetration : 0f));
    }

    public static float WeaponAttackSpeed(Player player, Item item)
    {
        float speed = Total(player, ItemClass(item)).Speed;
        return 1f + (speed - 1f) * ItemID.Sets.BonusMeleeSpeedMultiplier[item.type];
    }

    public static int WeaponArmorPenetration(Player player, Item item) =>
        (int)(item.armorPenetration + Total(player, ItemClass(item)).ArmorPen);

    static void Activate()
    {
        lock (Gate)
        {
            if (installed) return;
            installed = true;
        }
        DamageClassHooks.Install();
        ItemTooltipHooks.InstallLines();
    }

    internal static void ResetEffects(Player player)
    {
        if (Players.Count > 0 && Players.Get(player.Pointer) is PlayerData d) Reset(player, d);
    }

    internal static void CapAttackSpeeds(Player player) => Fold(player);

    internal static float WeaponDamageMultiplier(Player player, Item item, Func<float> vanilla)
    {
        DamageClass c = ItemClass(item);
        StatModifier t = Total(player, c).Damage;
        float mult = IsHooked(c) ? t.Additive * t.Multiplicative : vanilla();
        int damage = item.damage;
        if ((t.Base == 0f && t.Flat == 0f) || damage <= 0) return mult;
        return ((damage + t.Base) * mult + t.Flat) / damage;
    }

    internal static int WeaponCrit(Player player, Item item, int vanilla)
    {
        DamageClass c = ItemClass(item);
        if (!IsHooked(c)) return vanilla;
        return UsesCrit(c) ? RoundHalfUp(Total(player, c).Crit) + item.crit : 0;
    }

    internal static float WeaponKnockback(Player player, Item item, float vanilla)
    {
        StatModifier modifier = Total(player, ItemClass(item)).Knockback;
        return modifier == StatModifier.Default ? vanilla : modifier.ApplyTo(vanilla);
    }

    static bool MeleeLike(DamageClass c) => c == Vanilla.Melee || c == Vanilla.MeleeNoSpeed || c == Vanilla.SummonMeleeSpeed;

    internal static void ItemAnimation(Player player, Item item)
    {
        DamageClass c = ItemClass(item);
        bool hooked = IsHooked(c);
        if (!hooked && MeleeLike(c)) return;
        float speed = Total(player, c).Speed;
        if (!hooked && Math.Abs(speed - 1f) < 1e-6f) return;
        int frames = Math.Max(1, (int)((hooked ? item.useAnimation : player.itemAnimationMax) / Math.Min(Math.Max(speed, 0.01f), 3f)));
        player.itemAnimation = frames;
        player.itemAnimationMax = frames;
    }

    internal static void ItemTime(Player player, Item item)
    {
        DamageClass c = ItemClass(item);
        if (!IsHooked(c) && MeleeLike(c)) return;
        float speed = Total(player, c).Speed;
        if (Math.Abs(speed - 1f) < 1e-6f) return;
        int frames = Math.Max(1, (int)(item.useTime / Math.Min(Math.Max(speed, 0.01f), 3f)));
        player.itemTime = frames;
        player.itemTimeMax = frames;
    }

    internal static int HitPenetration(Player player, Item item) => HasData(player) ? ExtraPen(player, ItemClass(item), item.melee) : 0;

    internal static void ProjectileSpawned(Projectile projectile)
    {
        int who = projectile.owner;
        if (!projectile.active || who < 0 || who >= 255) return;
        Player player = Main.player[who];
        if (player.IsNull || !player.active || !HasData(player)) return;
        int extra = ExtraPen(player, ProjectileClass(projectile), projectile.melee);
        if (extra > 0) projectile.armorPenetration += extra;
    }

    internal static bool WantsFiring => hookedProjectiles;

    internal static void BeginFiring(Projectile projectile, out Projectile outer, out bool wasFiring)
    {
        outer = firing;
        wasFiring = isFiring;
        firing = projectile;
        isFiring = true;
    }

    internal static void EndFiring(Projectile outer, bool wasFiring)
    {
        firing = outer;
        isFiring = wasFiring;
    }

    internal static void TimerCrit(Player player, ref bool crit)
    {
        if (!isFiring) return;
        DamageClass c = ProjectileClass(firing);
        if (!IsHooked(c)) return;
        float chance = UsesCrit(c) ? Total(player, c).Crit + firing.bonusCritChance : -1f;
        Item held = player.HeldItem;
        if (chance >= 0f && !held.IsNull && ItemClass(held) == c) chance += held.crit;
        crit = chance >= 0f && Random.Shared.NextDouble() * 100.0 < chance;
    }

    internal static void MinionDamage(Projectile projectile)
    {
        if (!hookedProjectiles || !projectile.active || !(projectile.minion || projectile.sentry) || projectile.originalDamage <= 0) return;
        DamageClass c = ProjectileClass(projectile);
        if (!IsHooked(c)) return;
        int who = projectile.owner;
        if (who < 0 || who >= 255) return;
        Player owner = Main.player[who];
        if (owner.IsNull) return;
        projectile.damage = (int)(Total(owner, c).Damage.ApplyTo(projectile.originalDamage) + 5e-6f);
    }

    internal static StatModifier? KnockbackIfAny(Player player, DamageClass damageClass) =>
        HasData(player) ? Total(player, damageClass).Knockback : null;

    internal static bool WantsTooltip(Item item) =>
        Volatile.Read(ref installed) && !item.IsNull && item.damage > 0 && IsHooked(ItemClass(item));

    internal static string NameOf(DamageClass damageClass) =>
        Safe(damageClass, nameof(DamageClass.DisplayName), () => damageClass.DisplayName is { IsNull: false } text ? text.Value ?? "" : "", damageClass.PrettyPrintName());
}
