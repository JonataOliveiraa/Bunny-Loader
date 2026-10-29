using System.Collections.Generic;
using System.Threading;
using System;
using BunnyLoader;
using Terraria.ID;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static class DamageClassLoader
{
    [Flags]
    internal enum GameFlags
    {
        None = 0,
        Melee = 1,
        Ranged = 2,
        Magic = 4,
        Summon = 8,
        NoSpeed = 16,
        Whip = 32,
    }

    internal readonly struct Totals
    {
        public readonly StatModifier Damage;
        public readonly float Crit, Speed, Pen;
        public readonly StatModifier Knockback;

        public Totals(StatModifier damage, float crit, float speed, float pen, StatModifier knockback)
        {
            Damage = damage;
            Crit = crit;
            Speed = speed;
            Pen = pen;
            Knockback = knockback;
        }
    }

    internal sealed class PlayerStats
    {
        public DamageClassData[] Stats = Array.Empty<DamageClassData>();
        public DamageClassData[] Applied = Array.Empty<DamageClassData>();
        public bool Snapped;
        public float SnapMelee, SnapSummon;
    }

    sealed class OwnClass
    {
        public int Type;
        public DamageClass Class = null!;
    }

    static readonly object Gate = new();

    static readonly List<DamageClass> DamageClasses = new()
    {
        DamageClass.Default,
        DamageClass.Generic,
        DamageClass.Melee,
        DamageClass.MeleeNoSpeed,
        DamageClass.Ranged,
        DamageClass.Magic,
        DamageClass.Summon,
        DamageClass.SummonMeleeSpeed,
        DamageClass.MagicSummonHybrid,
        DamageClass.Throwing,
    };

    static readonly int VanillaClassCount = DamageClasses.Count;

    static readonly Dictionary<DamageClass, GameFlags> Native = new()
    {
        [DamageClass.Default] = GameFlags.None,
        [DamageClass.Melee] = GameFlags.Melee,
        [DamageClass.MeleeNoSpeed] = GameFlags.Melee | GameFlags.NoSpeed,
        [DamageClass.Ranged] = GameFlags.Ranged,
        [DamageClass.Magic] = GameFlags.Magic,
        [DamageClass.Summon] = GameFlags.Summon,
        [DamageClass.SummonMeleeSpeed] = GameFlags.Summon | GameFlags.Whip,
    };

    static readonly DamageClass[] Folded = { DamageClass.Generic, DamageClass.Melee, DamageClass.Ranged, DamageClass.Magic, DamageClass.Summon };

    static bool[,]? effectCache;
    static StatInheritanceData?[,]? inheritCache;

    static DamageClass?[] itemTypes = Array.Empty<DamageClass?>();
    static DamageClass?[] projectileTypes = Array.Empty<DamageClass?>();
    static readonly EntityInstances<OwnClass> OwnItems = new(), OwnProjectiles = new();
    static int ownItems, ownProjectiles;

    internal static readonly EntityInstances<PlayerStats> Players = new();

    internal static IntPtr Defaulting;

    internal static volatile bool Active;

    static DamageClassLoader()
    {
        foreach (DamageClass cls in DamageClasses) ModContent.Add(cls);
    }

    public static int DamageClassCount
    {
        get { lock (Gate) return DamageClasses.Count; }
    }

    public static DamageClass? GetDamageClass(int type)
    {
        lock (Gate) return (uint)type < (uint)DamageClasses.Count ? DamageClasses[type] : null;
    }

    internal static int Add(DamageClass damageClass)
    {
        int type;
        lock (Gate)
        {
            DamageClasses.Add(damageClass);
            type = DamageClasses.Count - 1;
            effectCache = null;
            inheritCache = null;
        }
        Activate();
        return type;
    }

    internal static bool IsHooked(DamageClass cls) => !Native.ContainsKey(cls);

    internal static void Activate()
    {
        if (Active) return;
        Active = true;
        DamageClassHooks.Install();
    }

    static DamageClass[] All()
    {
        lock (Gate) return DamageClasses.ToArray();
    }

    internal static bool CountsAs(DamageClass cls, DamageClass other)
    {
        bool[,] cache = Volatile.Read(ref effectCache) ?? BuildEffects();
        if (cls.Type >= cache.GetLength(0) || other.Type >= cache.GetLength(1)) cache = BuildEffects();
        return cache[cls.Type, other.Type];
    }

    static bool[,] BuildEffects()
    {
        DamageClass[] all = All();
        var cache = new bool[all.Length, all.Length];
        for (int i = 0; i < all.Length; i++)
            for (int j = 0; j < all.Length; j++)
            {
                bool counts = i == j;
                if (!counts)
                {
                    try { counts = all[i].GetEffectInheritance(all[j]); }
                    catch (Exception e) { ItemHooks.Report($"{all[i].FullName}.GetEffectInheritance", e); }
                }
                cache[i, j] = counts;
            }
        Volatile.Write(ref effectCache, cache);
        return cache;
    }

    static StatInheritanceData? Inheritance(DamageClass cls, DamageClass other)
    {
        StatInheritanceData?[,] cache = Volatile.Read(ref inheritCache) ?? BuildInheritance();
        if (cls.Type >= cache.GetLength(0) || other.Type >= cache.GetLength(1)) cache = BuildInheritance();
        return cache[cls.Type, other.Type];
    }

    static StatInheritanceData?[,] BuildInheritance()
    {
        DamageClass[] all = All();
        var cache = new StatInheritanceData?[all.Length, all.Length];
        for (int i = 0; i < all.Length; i++)
            for (int j = 0; j < all.Length; j++)
            {
                if (i == j) continue;
                try
                {
                    StatInheritanceData data = all[i].GetModifierInheritance(all[j]);
                    if (!data.IsNone) cache[i, j] = data;
                }
                catch (Exception e) { ItemHooks.Report($"{all[i].FullName}.GetModifierInheritance", e); }
            }
        Volatile.Write(ref inheritCache, cache);
        return cache;
    }

    internal static bool UsesCrit(DamageClass cls)
    {
        try { return cls.UseStandardCritCalcs; }
        catch (Exception e)
        {
            ItemHooks.Report($"{cls.FullName}.UseStandardCritCalcs", e);
            return false;
        }
    }

    internal static DamageClass ItemClass(Item item)
    {
        int type = item.type;
        if (Volatile.Read(ref ownItems) > 0 && OwnItems.Get(item.Pointer) is OwnClass own && own.Type == type) return own.Class;
        DamageClass?[] types = Volatile.Read(ref itemTypes);
        if ((uint)type < (uint)types.Length && types[type] is DamageClass cls) return cls;
        return ItemFromFlags(item);
    }

    internal static void SetItemClass(Item item, DamageClass cls)
    {
        ArgumentNullException.ThrowIfNull(cls);
        GameFlags flags = FlagsOf(cls);
        item.melee = (flags & GameFlags.Melee) != 0;
        item.ranged = (flags & GameFlags.Ranged) != 0;
        item.magic = (flags & GameFlags.Magic) != 0;
        item.summon = (flags & GameFlags.Summon) != 0;
        int type = item.type;
        if (Defaulting == item.Pointer)
        {
            lock (Gate) itemTypes = Store(itemTypes, type, cls);
            var noSpeed = ItemID.Sets.NoMeleeSpeedBonus;
            if ((flags & GameFlags.NoSpeed) != 0 && (uint)type < (uint)noSpeed.Length) noSpeed[type] = true;
            var whip = ItemID.Sets.SummonerWeaponThatScalesWithAttackSpeed;
            if ((flags & GameFlags.Whip) != 0 && (uint)type < (uint)whip.Length) whip[type] = true;
        }
        else
        {
            OwnItems.Set(item.Pointer, new OwnClass { Type = type, Class = cls });
            Interlocked.Increment(ref ownItems);
        }
        if (IsHooked(cls)) Activate();
    }

    internal static DamageClass ProjectileClass(Projectile proj)
    {
        int type = proj.type;
        if (Volatile.Read(ref ownProjectiles) > 0 && OwnProjectiles.Get(proj.Pointer) is OwnClass own && own.Type == type) return own.Class;
        DamageClass?[] types = Volatile.Read(ref projectileTypes);
        if ((uint)type < (uint)types.Length && types[type] is DamageClass cls) return cls;
        return ProjectileFromFlags(proj);
    }

    internal static void SetProjectileClass(Projectile proj, DamageClass cls)
    {
        ArgumentNullException.ThrowIfNull(cls);
        GameFlags flags = FlagsOf(cls);
        proj.melee = (flags & GameFlags.Melee) != 0;
        proj.ranged = (flags & GameFlags.Ranged) != 0;
        proj.magic = (flags & GameFlags.Magic) != 0;
        int type = proj.type;
        if (Defaulting == proj.Pointer)
        {
            lock (Gate) projectileTypes = Store(projectileTypes, type, cls);
        }
        else
        {
            OwnProjectiles.Set(proj.Pointer, new OwnClass { Type = type, Class = cls });
            Interlocked.Increment(ref ownProjectiles);
        }
        if (IsHooked(cls))
        {
            Activate();
            DamageClassHooks.WantProjectile(type);
        }
    }

    static DamageClass?[] Store(DamageClass?[] table, int type, DamageClass cls)
    {
        if (type < 0) return table;
        if (type >= table.Length)
        {
            var grown = new DamageClass?[Math.Max(type + 1, table.Length * 2)];
            Array.Copy(table, grown, table.Length);
            table = grown;
        }
        table[type] = cls;
        return table;
    }

    static GameFlags FlagsOf(DamageClass cls)
    {
        if (Native.TryGetValue(cls, out GameFlags own)) return own;
        bool Takes(DamageClass b)
        {
            if (CountsAs(cls, b)) return true;
            try { return cls.GetsPrefixesFor(b); }
            catch (Exception e)
            {
                ItemHooks.Report($"{cls.FullName}.GetPrefixInheritance", e);
                return false;
            }
        }
        GameFlags flags = GameFlags.None;
        if (Takes(DamageClass.Melee) || Takes(DamageClass.MeleeNoSpeed)) flags |= GameFlags.Melee;
        if (Takes(DamageClass.Ranged)) flags |= GameFlags.Ranged;
        if (Takes(DamageClass.Magic)) flags |= GameFlags.Magic;
        if (Takes(DamageClass.Summon) || Takes(DamageClass.SummonMeleeSpeed)) flags |= GameFlags.Summon;
        return flags;
    }

    static DamageClass ItemFromFlags(Item item)
    {
        int type = item.type;
        if (item.melee) return Flag(ItemID.Sets.NoMeleeSpeedBonus, type) ? DamageClass.MeleeNoSpeed : DamageClass.Melee;
        if (item.ranged) return DamageClass.Ranged;
        if (item.magic) return DamageClass.Magic;
        if (item.summon) return Flag(ItemID.Sets.SummonerWeaponThatScalesWithAttackSpeed, type) ? DamageClass.SummonMeleeSpeed : DamageClass.Summon;
        return DamageClass.Default;
    }

    static DamageClass ProjectileFromFlags(Projectile proj)
    {
        int type = proj.type;
        if (proj.melee) return DamageClass.Melee;
        if (proj.ranged) return DamageClass.Ranged;
        if (proj.magic) return DamageClass.Magic;
        if (Flag(ProjectileID.Sets.IsAWhip, type)) return DamageClass.SummonMeleeSpeed;
        if (proj.minion || proj.sentry || Flag(ProjectileID.Sets.MinionShot, type) || Flag(ProjectileID.Sets.SentryShot, type)) return DamageClass.Summon;
        return DamageClass.Default;
    }

    static bool Flag(BunnyLoader.Interop.GameArray<bool> set, int type) => !set.IsNull && (uint)type < (uint)set.Length && set[type];

    internal static PlayerStats Data(Player player)
    {
        PlayerStats? d = Players.Get(player.Pointer);
        if (d != null) return d;
        Activate();
        d = new PlayerStats();
        Players.Set(player.Pointer, d);
        Reset(player, d);
        return d;
    }

    internal static DamageClassData Stats(Player player, DamageClass cls)
    {
        ArgumentNullException.ThrowIfNull(cls);
        PlayerStats d = Data(player);
        if (cls.Type >= d.Stats.Length) Reset(player, d);
        return d.Stats[cls.Type];
    }

    internal static void Reset(Player player, PlayerStats d)
    {
        DamageClass[] all = All();
        if (d.Stats.Length != all.Length)
        {
            d.Stats = Fresh(all.Length);
            d.Applied = Fresh(all.Length);
        }
        else
        {
            foreach (DamageClassData data in d.Stats) data.Reset();
            foreach (DamageClassData data in d.Applied) data.Reset();
        }
        d.Snapped = false;
        for (int i = VanillaClassCount; i < all.Length; i++)
        {
            try { all[i].SetDefaultStats(player); }
            catch (Exception e) { ItemHooks.Report($"{all[i].FullName}.SetDefaultStats", e); }
        }
    }

    static DamageClassData[] Fresh(int count)
    {
        var data = new DamageClassData[count];
        for (int i = 0; i < count; i++) data[i] = new DamageClassData();
        return data;
    }

    internal static Totals Total(Player player, DamageClass cls)
    {
        ArgumentNullException.ThrowIfNull(cls);
        PlayerStats d = Data(player);
        if (cls.Type >= d.Stats.Length) Reset(player, d);
        Totals self = Effective(player, d, cls);
        StatModifier damage = self.Damage, knockback = self.Knockback;
        float crit = self.Crit, speed = self.Speed, pen = self.Pen;
        foreach (DamageClass other in All())
        {
            if (other == cls || other.Type >= d.Stats.Length) continue;
            if (Inheritance(cls, other) is not StatInheritanceData inh) continue;
            Totals s = Effective(player, d, other);
            damage = damage.CombineWith(s.Damage.Scale(inh.damageInheritance));
            crit += s.Crit * inh.critChanceInheritance;
            speed += (s.Speed - 1) * inh.attackSpeedInheritance;
            pen += s.Pen * inh.armorPenInheritance;
            knockback = knockback.CombineWith(s.Knockback.Scale(inh.knockbackInheritance));
        }
        return new Totals(damage, crit, speed, pen, knockback);
    }

    static Totals Effective(Player player, PlayerStats d, DamageClass cls)
    {
        DamageClassData own = d.Stats[cls.Type];
        bool folded = cls == DamageClass.Generic || cls == DamageClass.Melee || cls == DamageClass.Ranged || cls == DamageClass.Magic || cls == DamageClass.Summon;
        if (!folded) return new Totals(own.damage, own.critChance, own.attackSpeed, own.armorPen, own.knockback);

        float melee = player.meleeDamage, ranged = player.rangedDamage, magic = player.magicDamage, minion = player.minionDamage;
        float common = Math.Min(Math.Min(melee, ranged), Math.Min(magic, minion));
        int meleeCrit = player.meleeCrit, rangedCrit = player.rangedCrit, magicCrit = player.magicCrit;
        int commonCrit = Math.Min(meleeCrit, Math.Min(rangedCrit, magicCrit));
        float genericSpeed = d.Stats[DamageClass.Generic.Type].attackSpeed;
        float add, crit, speed, pen;
        if (cls == DamageClass.Generic)
        {
            add = common;
            crit = commonCrit;
            speed = genericSpeed;
            pen = player.armorPenetration;
        }
        else if (cls == DamageClass.Melee)
        {
            add = 1 + melee - common;
            crit = meleeCrit - commonCrit;
            speed = d.Snapped ? d.SnapMelee - genericSpeed + 1 : own.attackSpeed;
            pen = player.meleeArmorPenetration;
        }
        else if (cls == DamageClass.Ranged)
        {
            add = 1 + ranged - common;
            crit = rangedCrit - commonCrit;
            speed = own.attackSpeed;
            pen = own.armorPen;
        }
        else if (cls == DamageClass.Magic)
        {
            add = 1 + magic - common;
            crit = magicCrit - commonCrit;
            speed = own.attackSpeed;
            pen = own.armorPen;
        }
        else
        {
            add = 1 + minion - common;
            crit = own.critChance;
            speed = d.Snapped ? 1 + d.SnapSummon : own.attackSpeed;
            pen = own.armorPen;
        }
        return new Totals(new StatModifier(add, 1, own.damage.Flat, own.damage.Base), crit, speed, pen, own.knockback);
    }

    internal static void Fold(Player player)
    {
        PlayerStats? d = Players.Get(player.Pointer);
        if (d == null || d.Stats.Length < VanillaClassCount) return;
        foreach (DamageClass cls in Folded)
        {
            DamageClassData cur = d.Stats[cls.Type], app = d.Applied[cls.Type];
            float add = cur.damage.Additive - app.damage.Additive;
            float mult = cur.damage.Multiplicative / app.damage.Multiplicative;
            if (add != 0 || mult != 1) FoldDamage(player, cls, add, mult);
            int crit = Round(cur.critChance - app.critChance);
            if (crit != 0) FoldCrit(player, cls, crit);
            float speed = cur.attackSpeed - app.attackSpeed;
            if (speed != 0)
            {
                if (cls == DamageClass.Generic || cls == DamageClass.Melee) player.meleeSpeed += speed;
                else if (cls == DamageClass.Summon) player.summonerWeaponSpeedBonus += speed;
            }
            int pen = Round(cur.armorPen - app.armorPen);
            if (pen != 0)
            {
                if (cls == DamageClass.Generic) player.armorPenetration += pen;
                else if (cls == DamageClass.Melee) player.meleeArmorPenetration += pen;
            }
            app.CopyFrom(cur);
        }
        d.Snapped = true;
        d.SnapMelee = player.meleeSpeed;
        d.SnapSummon = player.summonerWeaponSpeedBonus;
    }

    static void FoldDamage(Player player, DamageClass cls, float add, float mult)
    {
        bool all = cls == DamageClass.Generic;
        if (all || cls == DamageClass.Melee) player.meleeDamage = (player.meleeDamage + add) * mult;
        if (all || cls == DamageClass.Ranged) player.rangedDamage = (player.rangedDamage + add) * mult;
        if (all || cls == DamageClass.Magic) player.magicDamage = (player.magicDamage + add) * mult;
        if (all || cls == DamageClass.Summon) player.minionDamage = (player.minionDamage + add) * mult;
    }

    static void FoldCrit(Player player, DamageClass cls, int crit)
    {
        bool all = cls == DamageClass.Generic;
        if (all || cls == DamageClass.Melee) player.meleeCrit += crit;
        if (all || cls == DamageClass.Ranged) player.rangedCrit += crit;
        if (all || cls == DamageClass.Magic) player.magicCrit += crit;
    }

    internal static int Round(float value) => (int)MathF.Floor(value + 0.5f);

    internal static int ExtraPen(Player player, DamageClass cls, bool melee)
    {
        float total = Total(player, cls).Pen;
        return Round(total - player.armorPenetration - (melee ? player.meleeArmorPenetration : 0));
    }
}
