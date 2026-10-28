using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System;
using BunnyLoader.Interop;
using BunnyLoader;
using Terraria.GameContent.Prefixes;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader.Core;
using Terraria.Utilities;

namespace Terraria.ModLoader;

public static unsafe class PrefixLoader
{
    static readonly object Gate = new();

    static ModPrefix?[] prefixes = Array.Empty<ModPrefix?>();

    static readonly Dictionary<PrefixCategory, List<ModPrefix>> ByCategory = new();

    static readonly Dictionary<int, PrefixCategory[]> CategoriesByType = new();

    static bool itemChoose, itemChance, itemAllow;

    static Hook? rollable, canRoll, roll, stats, apply, accessory, legacyNames;

    public static int VanillaCount => PrefixID.Count;

    public static int PrefixCount => Host.Native()->PrefixCount();

    public static ModPrefix? GetPrefix(int type)
    {
        ModPrefix?[] all = Volatile.Read(ref prefixes);
        uint index = (uint)(type - VanillaCount);
        return index < (uint)all.Length ? all[index] : null;
    }

    public static IReadOnlyList<ModPrefix> GetPrefixesInCategory(PrefixCategory category)
    {
        lock (Gate) return ByCategory.TryGetValue(category, out List<ModPrefix>? list) ? list.ToArray() : Array.Empty<ModPrefix>();
    }

    internal static void Register(ModPrefix prefix)
    {
        byte* mod = Host.Utf8(prefix.Mod.Uid), name = Host.Utf8(prefix.Name);
        int type;
        try { type = Host.Native()->RegisterPrefix(mod, name); }
        finally
        {
            Host.FreeUtf8(mod);
            Host.FreeUtf8(name);
        }
        if (type < 0) throw new InvalidOperationException($"{prefix.FullName}: the mod already has a prefix named '{prefix.Name}', or there is no room (up to 255)");
        prefix.Type = type;
        lock (Gate)
        {
            int index = type - VanillaCount;
            ModPrefix?[] all = prefixes;
            if (index >= all.Length)
            {
                var grown = new ModPrefix?[Math.Max(index + 1, all.Length * 2)];
                Array.Copy(all, grown, all.Length);
                all = grown;
            }
            all[index] = prefix;
            Volatile.Write(ref prefixes, all);
            if (!ByCategory.TryGetValue(prefix.Category, out List<ModPrefix>? list)) ByCategory[prefix.Category] = list = new List<ModPrefix>();
            list.Add(prefix);
        }
        bool Has(string method) => ItemHooks.Overrides(prefix.GetType(), typeof(ModPrefix), method);
        WantRolling();
        if (Has(nameof(ModPrefix.Apply))) WantApply();
        if (Has(nameof(ModPrefix.ApplyAccessoryEffects)))
            ItemHooks.Once("PrefixAccessory", () => accessory = ItemHooks.Raw("void GrantPrefixBenefits(Item item)",
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&GrantPrefixBenefits));
        ItemTooltipHooks.WantPrefixLines();
    }

    internal static void WantItemHooks(Func<string, bool> has)
    {
        bool choose = has(nameof(ModItem.ChoosePrefix)), chance = has(nameof(ModItem.PrefixChance)), allow = has(nameof(ModItem.AllowPrefix));
        if (!choose && !chance && !allow) return;
        itemChoose |= choose;
        itemChance |= chance;
        itemAllow |= allow;
        WantRolling();
        if (chance) WantApply();
    }

    internal static void WantRollable()
    {
        ItemHooks.Once("PrefixRollable", () =>
        {
            rollable = ItemHooks.Raw("Terraria", "Item", "int[] GetRollablePrefixes()",
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr>)&GetRollablePrefixes);
            canRoll = ItemHooks.Raw("Terraria", "Item", "bool CanRollPrefix(int prefix)",
                (IntPtr)(delegate* unmanaged<IntPtr, int, IntPtr, byte>)&CanRollPrefix);
        });
    }

    static void WantRolling()
    {
        WantRollable();
        ItemHooks.Once("PrefixRolling", () =>
        {
            roll = ItemHooks.Raw("Terraria", "Item", "bool RollAPrefix(UnifiedRandom random, ref int rolledPrefix)",
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, int*, IntPtr, byte>)&RollAPrefix);
            stats = ItemHooks.Raw("Terraria", "Item",
                "bool TryGetPrefixStatMultipliersForItem(int rolledPrefix, out float dmg, out float kb, out float spd, out float size, out float shtspd, out float mcst, out int crt, out int tagdmg, out int arpen, out float value)",
                (IntPtr)(delegate* unmanaged<IntPtr, int, float*, float*, float*, float*, float*, float*, int*, int*, int*, float*, IntPtr, byte>)&TryGetPrefixStatMultipliersForItem);
        });
    }

    static void WantApply() => ItemHooks.Once("PrefixApply", () => apply = ItemHooks.Raw("Terraria", "Item",
        "bool Prefix(int prefixWeWant, out bool rolledPrefixIsTopTier)",
        (IntPtr)(delegate* unmanaged<IntPtr, int, byte*, IntPtr, byte>)&Prefix));

    internal static void ContentReady()
    {
        ModPrefix[] all;
        lock (Gate) all = prefixes.Where(p => p != null).Select(p => p!).ToArray();
        if (all.Length == 0) return;
        Tables(all);
        foreach (ModPrefix p in all)
        {
            try { p.SetupContent(); }
            catch (Exception e) { ItemHooks.Report($"{p.FullName}.SetStaticDefaults", e); }
        }
        ItemHooks.Once("PrefixNames", () => legacyNames = ItemHooks.Raw("Terraria", "Lang", "void InitializeLegacyLocalization()",
            (IntPtr)(delegate* unmanaged<IntPtr, void>)&InitializeLegacyLocalization));
    }

    static void Tables(ModPrefix[] all)
    {
        int total = PrefixCount;
        var names = Lang.prefix;
        if (names.Length < total)
        {
            var grown = GameArray<LocalizedText>.New(new GameClass("Terraria.Localization", "LocalizedText"), total);
            for (int i = 0; i < names.Length; i++) grown[i] = names[i];
            Lang.prefix = grown;
            names = grown;
        }
        var reduced = PrefixID.Sets.ReducedNaturalChance;
        if (reduced.Length < total)
        {
            var grown = GameArray<bool>.New(new GameClass("System", "Boolean"), total);
            for (int i = 0; i < reduced.Length; i++) grown[i] = reduced[i];
            PrefixID.Sets.ReducedNaturalChance = grown;
        }
        foreach (ModPrefix p in all)
        {
            try { names[p.Type] = p.DisplayName; }
            catch (Exception e) { ItemHooks.Report($"{p.FullName}.DisplayName", e); }
        }
    }

    [UnmanagedCallersOnly]
    static void InitializeLegacyLocalization(IntPtr method)
    {
        Hook? hook = legacyNames;
        if (hook == null) return;
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(method);
        ItemHooks.Original(ref call, hook, "Lang.InitializeLegacyLocalization");
        try
        {
            ModPrefix[] all;
            lock (Gate) all = prefixes.Where(p => p != null).Select(p => p!).ToArray();
            Tables(all);
        }
        catch (Exception e) { ItemHooks.Report("prefix names", e); }
    }

    static bool At(GameArray<bool> set, int type) => !set.IsNull && (uint)type < (uint)set.Length && set[type];

    static bool CanBeModWeapon(Item item) => item.damage > 0 && item.ammo == 0 && !item.consumable && At(ItemID.Sets.CanGetPrefixes, item.type);

    static PrefixCategory[] Categories(Item item)
    {
        int type = item.type;
        lock (Gate)
        {
            if (CategoriesByType.TryGetValue(type, out PrefixCategory[]? cached)) return cached;
        }
        ModItem? m = ItemHooks.ModItemOf(item);
        bool weapon = m != null && CanBeModWeapon(item);
        bool Ask(Func<ModItem, bool> question)
        {
            if (!weapon) return false;
            try { return question(m!); }
            catch (Exception e)
            {
                ItemHooks.Report($"{m!.FullName} (prefix category)", e);
                return false;
            }
        }
        var list = new List<PrefixCategory>();
        if (At(PrefixLegacy.ItemSets.SwordsHammersAxesPicks, type) || Ask(x => x.MeleePrefix())) list.Add(PrefixCategory.Melee);
        if (At(PrefixLegacy.ItemSets.GunsBows, type) || Ask(x => x.RangedPrefix())) list.Add(PrefixCategory.Ranged);
        if (At(PrefixLegacy.ItemSets.Magic, type) || Ask(x => x.MagicPrefix())) list.Add(PrefixCategory.Magic);
        if (At(PrefixLegacy.ItemSets.Summon, type) || Ask(x => x.SummonPrefix())) list.Add(PrefixCategory.Summon);
        if (At(PrefixLegacy.ItemSets.SpearsMacesChainsawsDrillsPunchCannon, type) || At(PrefixLegacy.ItemSets.BoomerangsChakrams, type) ||
            At(PrefixLegacy.ItemSets.ItemsThatCanHaveLegendary2, type) || Ask(x => x.WeaponPrefix()) || list.Count > 0)
            list.Add(PrefixCategory.AnyWeapon);
        if (item.IsAPrefixableAccessory()) list.Add(PrefixCategory.Accessory);
        PrefixCategory[] result = list.ToArray();
        if (type > 0)
            lock (Gate) CategoriesByType[type] = result;
        return result;
    }

    static GameArray<int> VanillaPrefixes(PrefixCategory category) => category switch
    {
        PrefixCategory.Melee => PrefixLegacy.Prefixes.PrefixesForSwords,
        PrefixCategory.Ranged => PrefixLegacy.Prefixes.PrefixesForGunsBows,
        PrefixCategory.Magic => PrefixLegacy.Prefixes.PrefixesForMagic,
        PrefixCategory.Summon => PrefixLegacy.Prefixes.PrefixesForSummons,
        PrefixCategory.AnyWeapon => PrefixLegacy.Prefixes.PrefixesForSpears,
        PrefixCategory.Accessory => PrefixLegacy.Prefixes.PrefixesForAccessories,
        _ => default,
    };

    internal static bool AllowPrefix(Item item, int pre)
    {
        bool allowed = true;
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { allowed &= g.AllowPrefix(item, pre); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.AllowPrefix", e); }
        }
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { allowed &= m.AllowPrefix(pre); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.AllowPrefix", e); }
        }
        return allowed;
    }

    static bool CanRoll(ModPrefix p, Item item)
    {
        try { return p.CanRoll(item); }
        catch (Exception e)
        {
            ItemHooks.Report($"{p.FullName}.CanRoll", e);
            return false;
        }
    }

    [UnmanagedCallersOnly]
    static IntPtr GetRollablePrefixes(IntPtr self, IntPtr method)
    {
        Hook? hook = rollable;
        if (hook == null) return IntPtr.Zero;
        IntPtr original = (IntPtr)ItemHooks.Call(hook, self, method, "Item.GetRollablePrefixes");
        try { return Rollable(new Item(self), new GameArray<int>(original)); }
        catch (Exception e)
        {
            ItemHooks.Report("GetRollablePrefixes", e);
            return original;
        }
    }

    static IntPtr Rollable(Item item, GameArray<int> vanilla)
    {
        PrefixCategory[] categories = Categories(item);
        bool modItem = ItemLoader.Get(item.type) != null;
        var extra = new List<int>();
        foreach (PrefixCategory c in categories)
            foreach (ModPrefix p in GetPrefixesInCategory(c))
                if (CanRoll(p, item)) extra.Add(p.Type);
        if (!modItem && extra.Count == 0 && !itemAllow) return vanilla.Pointer;
        var result = new List<int>();
        var seen = new HashSet<int>();
        void Add(int id)
        {
            if (seen.Add(id) && (!itemAllow || AllowPrefix(item, id))) result.Add(id);
        }
        void AddAll(GameArray<int> ids)
        {
            if (ids.IsNull) return;
            for (int i = 0; i < ids.Length; i++) Add(ids[i]);
        }
        AddAll(vanilla);
        if (modItem)
            foreach (PrefixCategory c in categories) AddAll(VanillaPrefixes(c));
        foreach (int id in extra) Add(id);
        if (result.Count == 0) return IntPtr.Zero;
        var array = GameArray<int>.New(new GameClass("System", "Int32"), result.Count);
        for (int i = 0; i < result.Count; i++) array[i] = result[i];
        return array.Pointer;
    }

    [UnmanagedCallersOnly]
    static byte CanRollPrefix(IntPtr self, int prefix, IntPtr method)
    {
        Hook? hook = canRoll;
        if (hook == null) return 0;
        try
        {
            if (GetPrefix(prefix) is ModPrefix p && p.Category == PrefixCategory.Custom)
            {
                var item = new Item(self);
                return AllowPrefix(item, prefix) && CanRoll(p, item) ? (byte)1 : (byte)0;
            }
        }
        catch (Exception e) { ItemHooks.Report("CanRoll", e); }
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(prefix);
        call.Arg(method);
        return (byte)(ItemHooks.Original(ref call, hook, "Item.CanRollPrefix") & 0xFF);
    }

    static int ChoosePrefix(Item item, UnifiedRandom rand)
    {
        if (!itemChoose) return -1;
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try
            {
                int pre = g.ChoosePrefix(item, rand);
                if (pre > 0) return pre;
            }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.ChoosePrefix", e); }
        }
        if (ItemHooks.ModItemOf(item) is not ModItem m) return -1;
        try { return m.ChoosePrefix(rand); }
        catch (Exception e)
        {
            ItemHooks.Report($"{m.FullName}.ChoosePrefix", e);
            return -1;
        }
    }

    static bool? PrefixChance(Item item, int pre, UnifiedRandom rand)
    {
        bool? result = null;
        void Take(bool? r)
        {
            if (r.HasValue) result = r.Value && (result ?? true);
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { Take(g.PrefixChance(item, pre, rand)); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.PrefixChance", e); }
        }
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { Take(m.PrefixChance(pre, rand)); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.PrefixChance", e); }
        }
        return result;
    }

    [UnmanagedCallersOnly]
    static byte RollAPrefix(IntPtr self, IntPtr random, int* rolled, IntPtr method)
    {
        Hook? hook = roll;
        if (hook == null) return 0;
        try
        {
            var item = new Item(self);
            var rand = new UnifiedRandom(random);
            int forced = ChoosePrefix(item, rand);
            if (forced > 0 && item.CanRollPrefix(forced))
            {
                *rolled = forced;
                return 1;
            }
            var list = item.GetRollablePrefixes();
            if (list.IsNull || list.Length == 0) return 0;
            var ids = new List<int>();
            var weights = new List<double>();
            double total = 0;
            bool anyMod = false;
            for (int i = 0; i < list.Length; i++)
            {
                int id = list[i];
                double w = 1;
                if (GetPrefix(id) is ModPrefix p)
                {
                    anyMod = true;
                    try { w = CanRoll(p, item) ? p.RollChance(item) : 0; }
                    catch (Exception e)
                    {
                        ItemHooks.Report($"{p.FullName}.RollChance", e);
                        w = 0;
                    }
                }
                if (!(w > 0)) continue;
                ids.Add(id);
                weights.Add(w);
                total += w;
            }
            if (anyMod)
            {
                if (ids.Count == 0) return 0;
                double r = rand.NextDouble() * total;
                int k = 0;
                while (k < ids.Count - 1 && r >= weights[k]) r -= weights[k++];
                *rolled = ids[k];
                return 1;
            }
        }
        catch (Exception e) { ItemHooks.Report("RollAPrefix", e); }
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(random);
        call.Arg((IntPtr)rolled);
        call.Arg(method);
        return (byte)(ItemHooks.Original(ref call, hook, "Item.RollAPrefix") & 0xFF);
    }

    [UnmanagedCallersOnly]
    static byte TryGetPrefixStatMultipliersForItem(IntPtr self, int prefix, float* dmg, float* kb, float* spd, float* size, float* shtspd,
                                                   float* mcst, int* crt, int* tagdmg, int* arpen, float* value, IntPtr method)
    {
        Hook? hook = stats;
        if (hook == null) return 0;
        if (GetPrefix(prefix) is ModPrefix p)
        {
            try
            {
                *dmg = *kb = *spd = *size = *shtspd = *mcst = *value = 1f;
                *crt = *tagdmg = *arpen = 0;
                return StatsOf(p, new Item(self), dmg, kb, spd, size, shtspd, mcst, crt, value) ? (byte)1 : (byte)0;
            }
            catch (Exception e)
            {
                ItemHooks.Report($"{p.FullName}.SetStats", e);
                return 0;
            }
        }
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(prefix);
        call.Arg((IntPtr)dmg);
        call.Arg((IntPtr)kb);
        call.Arg((IntPtr)spd);
        call.Arg((IntPtr)size);
        call.Arg((IntPtr)shtspd);
        call.Arg((IntPtr)mcst);
        call.Arg((IntPtr)crt);
        call.Arg((IntPtr)tagdmg);
        call.Arg((IntPtr)arpen);
        call.Arg((IntPtr)value);
        call.Arg(method);
        return (byte)(ItemHooks.Original(ref call, hook, "Item.TryGetPrefixStatMultipliersForItem") & 0xFF);
    }

    static bool StatsOf(ModPrefix p, Item item, float* dmg, float* kb, float* spd, float* size, float* shtspd, float* mcst, int* crt, float* value)
    {
        if (!p.AllStatChangesHaveEffectOn(item)) return false;
        float d = 1f, k = 1f, s = 1f, z = 1f, sh = 1f, m = 1f;
        int c = 0;
        p.SetStats(ref d, ref k, ref s, ref z, ref sh, ref m, ref c);
        float valueMult = d * (2f - s) * (2f - m) * z * k * sh * (1f + c * 0.02f);
        p.ModifyValue(ref valueMult);
        *dmg = d;
        *kb = k;
        *spd = s;
        *size = z;
        *shtspd = sh;
        *mcst = m;
        *crt = c;
        *value = valueMult;
        return (d == 1f || Math.Round(item.damage * d) != item.damage) &&
               (s == 1f || Math.Round(item.useAnimation * s) != item.useAnimation) &&
               (m == 1f || Math.Round(item.mana * m) != item.mana) &&
               (k == 1f || item.knockBack != 0f);
    }

    [UnmanagedCallersOnly]
    static byte Prefix(IntPtr self, int want, byte* topTier, IntPtr method)
    {
        Hook? hook = apply;
        if (hook == null) return 0;
        var item = new Item(self);
        int arg = want;
        try
        {
            if (want < 0 && want > -3 && itemChance)
            {
                UnifiedRandom rand = WorldGen.isGeneratingOrLoadingWorld ? WorldGen.genRand : Main.rand;
                bool? chance = PrefixChance(item, want, rand);
                if (chance == false) return 0;
                if (chance == true && want == -1) arg = -2;
            }
        }
        catch (Exception e) { ItemHooks.Report("PrefixChance", e); }
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(arg);
        call.Arg((IntPtr)topTier);
        call.Arg(method);
        byte ok = (byte)(ItemHooks.Original(ref call, hook, "Item.Prefix") & 0xFF);
        if (ok == 0 || want == -3) return ok;
        try
        {
            if (GetPrefix(item.prefix) is ModPrefix p)
            {
                try { p.Apply(item); }
                catch (Exception e) { ItemHooks.Report($"{p.FullName}.Apply", e); }
            }
        }
        catch (Exception e) { ItemHooks.Report("ModPrefix.Apply", e); }
        return ok;
    }

    [UnmanagedCallersOnly]
    static void GrantPrefixBenefits(IntPtr self, IntPtr itemPointer, IntPtr method)
    {
        Hook? hook = accessory;
        if (hook == null) return;
        ItemHooks.Call(hook, self, itemPointer, method, "Player.GrantPrefixBenefits");
        try
        {
            var item = new Item(itemPointer);
            if (!item.IsNull && GetPrefix(item.prefix) is ModPrefix p) p.ApplyAccessoryEffects(new Player(self));
        }
        catch (Exception e) { ItemHooks.Report("ModPrefix.ApplyAccessoryEffects", e); }
    }

    internal static List<TooltipLine>? TooltipLines(Item item)
    {
        if (GetPrefix(item.prefix) is not ModPrefix p || !ItemHooks.Overrides(p.GetType(), typeof(ModPrefix), nameof(ModPrefix.GetTooltipLines))) return null;
        try
        {
            List<TooltipLine>? lines = p.GetTooltipLines(item)?.Where(l => l != null).ToList();
            return lines is { Count: > 0 } ? lines : null;
        }
        catch (Exception e)
        {
            ItemHooks.Report($"{p.FullName}.GetTooltipLines", e);
            return null;
        }
    }
}
