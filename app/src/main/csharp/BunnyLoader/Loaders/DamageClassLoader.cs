using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BunnyLoader.Loaders;

// As classes de dano (o DamageClassLoader do tModLoader) e o que as liga ao
// jogo do celular, que e compilado e nao tem DamageClass. O mesmo desenho do
// loader JS (script/js/mod/Loaders/DamageClassLoader.js):
//
// As do jogo continuam sendo as flags do item e do projetil (melee, ranged,
// magic, summon) e os campos do jogador (meleeDamage, meleeCrit, meleeSpeed,
// minionDamage...). O que um mod soma nelas e dobrado nesses campos antes do
// CapAttackSpeeds, e todo codigo do jogo que os le ja ve o bonus.
//
// As outras, "de gancho" (Generic, Throwing e MagicSummonHybrid usadas num
// item, e as de mod), nao cabem nas flags: o dano, o critico, a repulsao, a
// velocidade, a penetracao e o dano de lacaio delas passam pelos hooks abaixo.
// O item delas ganha as flags das classes que ela conta como
// (GetEffectInheritance) ou cujos prefixos aceita.
//
// Tudo roda na thread do jogo (hooks, tooltip, SetDefaults); sem travas.
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
    }

    // Os bonus de uma classe para um jogador (o que GetDamage, GetCritChance...
    // devolvem por ref).
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

    internal sealed class PlayerData
    {
        public ClassStats[] Stats = Array.Empty<ClassStats>();
        public ClassStats[] Applied = Array.Empty<ClassStats>();
        public bool HasSnap;
        public float SnapMelee, SnapSummon;
    }

    // Os totais de uma classe (GetTotalDamage e afins).
    internal struct Totals
    {
        public StatModifier Damage;
        public float Crit;
        public float Speed;
        public float ArmorPen;
        public StatModifier Knockback;
    }

    static readonly List<DamageClass> Classes = new();
    static readonly Dictionary<DamageClass, Flags> Native = new();

    // [i][j]: a heranca de j em i (null = nada) e se i conta como j. Refeitas
    // quando entra classe nova.
    static StatInheritanceData?[]?[] inherit = Array.Empty<StatInheritanceData?[]?>();
    static bool[]?[] countsComputed = Array.Empty<bool[]?>();
    static bool[]?[] counts = Array.Empty<bool[]?>();

    // A classe por tipo (gravada no SetDefaults do tipo, vale para o Clone,
    // o NewItem e a rede) e a de cada objeto mudado fora dele (vale enquanto
    // o objeto tiver o mesmo tipo).
    static readonly Dictionary<int, DamageClass> ItemTypeClass = new();
    static readonly Dictionary<int, DamageClass> ProjectileTypeClass = new();
    static readonly Dictionary<nint, (int Type, DamageClass Class)> ItemInstanceClass = new();
    static readonly Dictionary<nint, (int Type, DamageClass Class)> ProjectileInstanceClass = new();

    static readonly Dictionary<nint, PlayerData> Players = new();

    static nint defaulting;        // o item/projetil no SetDefaults agora
    static bool hookedProjectiles; // algum projetil tem classe de gancho
    static Projectile firing;      // o projetil no Projectile.Damage agora
    static bool isFiring;
    static int staticDefaultsDone; // classes com SetStaticDefaults ja chamado
    static bool installed;

    internal static readonly VanillaSet Vanilla = BuildVanilla();

    public static int DamageClassCount => Classes.Count;

    public static DamageClass? GetDamageClass(int type) => (uint)type < (uint)Classes.Count ? Classes[type] : null;

    // ---- registro ----

    static VanillaSet BuildVanilla()
    {
        var v = new VanillaSet();
        // A ordem de Type do tModLoader.
        foreach (DamageClass cls in new[] { v.Default, v.Generic, v.Melee, v.MeleeNoSpeed, v.Ranged, v.Magic,
                                            v.Summon, v.SummonMeleeSpeed, v.MagicSummonHybrid, v.Throwing })
        {
            cls.Type = Classes.Count;
            Classes.Add(cls);
            ModContent.Add(cls);
        }
        Native[v.Default] = new Flags();
        Native[v.Melee] = new Flags { Melee = true };
        Native[v.MeleeNoSpeed] = new Flags { Melee = true, NoSpeed = true };
        Native[v.Ranged] = new Flags { Ranged = true };
        Native[v.Magic] = new Flags { Magic = true };
        Native[v.Summon] = new Flags { Summon = true };
        Native[v.SummonMeleeSpeed] = new Flags { Summon = true, Whip = true };
        return v;
    }

    public static void Register(DamageClass cls)
    {
        cls.Type = Classes.Count;
        Classes.Add(cls);
        inherit = Array.Empty<StatInheritanceData?[]?>();
        countsComputed = Array.Empty<bool[]?>();
        counts = Array.Empty<bool[]?>();
        ModContent.Add(cls);
        LocalizeName(cls);
        Activate();
    }

    // Os SetStaticDefaults pendentes, na thread do jogo (o primeiro quadro de
    // jogador depois da carga).
    static void RunStaticDefaults()
    {
        while (staticDefaultsDone < Classes.Count)
        {
            DamageClass cls = Classes[staticDefaultsDone++];
            if (cls.IsVanilla) continue;
            try
            {
                cls.SetStaticDefaults();
            }
            catch (Exception e)
            {
                Log.Error($"damage class {cls.FullName}: SetStaticDefaults threw: {e}");
            }
        }
    }

    public static bool IsHooked(DamageClass cls) => !Native.ContainsKey(cls);

    public static bool UsesCrit(DamageClass cls) => Safe(cls, nameof(DamageClass.UseStandardCritCalcs), () => cls.UseStandardCritCalcs);

    public static bool CountsAs(DamageClass cls, DamageClass other)
    {
        if (cls == other) return true;
        int i = cls.Type, j = other.Type;
        if (countsComputed.Length != Classes.Count)
        {
            countsComputed = new bool[]?[Classes.Count];
            counts = new bool[]?[Classes.Count];
        }
        bool[] done = countsComputed[i] ??= new bool[Classes.Count];
        bool[] row = counts[i] ??= new bool[Classes.Count];
        if (!done[j])
        {
            row[j] = Safe(cls, nameof(DamageClass.GetEffectInheritance), () => cls.GetEffectInheritance(other));
            done[j] = true;
        }
        return row[j];
    }

    static StatInheritanceData? Inheritance(DamageClass cls, DamageClass other)
    {
        if (inherit.Length != Classes.Count) inherit = new StatInheritanceData?[]?[Classes.Count];
        StatInheritanceData?[] row = inherit[cls.Type] ??= Fill(Classes.Count, cls);
        return row[other.Type];

        static StatInheritanceData?[] Fill(int count, DamageClass cls)
        {
            var row = new StatInheritanceData?[count];
            for (int j = 0; j < count; j++)
            {
                DamageClass other = Classes[j];
                if (other == cls) continue;
                StatInheritanceData data = Safe(cls, nameof(DamageClass.GetModifierInheritance), () => cls.GetModifierInheritance(other));
                row[j] = data.IsNone ? null : data;
            }
            return row;
        }
    }

    // Um erro do mod vai ao log e vale o fallback (false / None por padrao).
    internal static T Safe<T>(DamageClass cls, string member, Func<T> body, T fallback = default!)
    {
        try
        {
            return body();
        }
        catch (Exception e)
        {
            Log.Error($"damage class {cls.FullName}: {member} threw: {e}");
            return fallback;
        }
    }

    // ---- item e projetil ----

    // Em volta do SetDefaults de um item de mod: o DamageType escrito nele vale
    // para o tipo inteiro.
    public static void Defaulting(nint entity, Action body)
    {
        nint outer = defaulting;
        defaulting = entity;
        try
        {
            body();
        }
        finally
        {
            defaulting = outer;
        }
    }

    public static DamageClass ItemClass(Item item)
    {
        int type = item.type;
        if (ItemInstanceClass.Count > 0 && ItemInstanceClass.TryGetValue(item.Pointer, out var own) && own.Type == type) return own.Class;
        if (ItemTypeClass.TryGetValue(type, out DamageClass? cls)) return cls;
        return ItemFromFlags(item);
    }

    public static void SetItemClass(Item item, DamageClass cls)
    {
        if (cls == null) throw new ArgumentNullException(nameof(cls), "DamageType: use DamageClass.Default for none");
        Flags flags = FlagsOf(cls);
        item.melee = flags.Melee;
        item.ranged = flags.Ranged;
        item.magic = flags.Magic;
        item.summon = flags.Summon;

        int type = item.type;
        if (defaulting == item.Pointer)
        {
            ItemTypeClass[type] = cls;
            if (flags.NoSpeed) ItemID.Sets.NoMeleeSpeedBonus[type] = true;
            if (flags.Whip) ItemID.Sets.SummonerWeaponThatScalesWithAttackSpeed[type] = true;
        }
        else
        {
            ItemInstanceClass[item.Pointer] = (type, cls);
        }
        if (IsHooked(cls)) Activate();
    }

    public static DamageClass ProjectileClass(Projectile proj)
    {
        int type = proj.type;
        if (ProjectileInstanceClass.Count > 0 && ProjectileInstanceClass.TryGetValue(proj.Pointer, out var own) && own.Type == type) return own.Class;
        if (ProjectileTypeClass.TryGetValue(type, out DamageClass? cls)) return cls;
        return ProjectileFromFlags(proj);
    }

    public static void SetProjectileClass(Projectile proj, DamageClass cls)
    {
        if (cls == null) throw new ArgumentNullException(nameof(cls), "DamageType: use DamageClass.Default for none");
        Flags flags = FlagsOf(cls);
        proj.melee = flags.Melee;
        proj.ranged = flags.Ranged;
        proj.magic = flags.Magic;

        if (defaulting == proj.Pointer) ProjectileTypeClass[proj.type] = cls;
        else ProjectileInstanceClass[proj.Pointer] = (proj.type, cls);
        if (IsHooked(cls))
        {
            hookedProjectiles = true;
            Activate();
        }
    }

    // As flags do jogo de uma classe: as dela, se e do jogo; senao as das
    // classes que ela conta como ou cujos prefixos aceita.
    static Flags FlagsOf(DamageClass cls)
    {
        if (Native.TryGetValue(cls, out Flags own)) return own;
        VanillaSet v = Vanilla;
        bool Takes(DamageClass b) => CountsAs(cls, b) || cls.GetsPrefixesFor(b);
        return new Flags
        {
            Melee = Takes(v.Melee) || Takes(v.MeleeNoSpeed),
            Ranged = Takes(v.Ranged),
            Magic = Takes(v.Magic),
            Summon = Takes(v.Summon) || Takes(v.SummonMeleeSpeed),
        };
    }

    static DamageClass ItemFromFlags(Item item)
    {
        VanillaSet v = Vanilla;
        int type = item.type;
        if (item.melee) return ItemID.Sets.NoMeleeSpeedBonus[type] ? v.MeleeNoSpeed : v.Melee;
        if (item.ranged) return v.Ranged;
        if (item.magic) return v.Magic;
        if (item.summon) return ItemID.Sets.SummonerWeaponThatScalesWithAttackSpeed[type] ? v.SummonMeleeSpeed : v.Summon;
        return v.Default;
    }

    static DamageClass ProjectileFromFlags(Projectile proj)
    {
        VanillaSet v = Vanilla;
        int type = proj.type;
        if (proj.melee) return v.Melee;
        if (proj.ranged) return v.Ranged;
        if (proj.magic) return v.Magic;
        if (ProjectileID.Sets.IsAWhip[type]) return v.SummonMeleeSpeed;
        if (proj.minion || proj.sentry || ProjectileID.Sets.MinionShot[type] || ProjectileID.Sets.SentryShot[type]) return v.Summon;
        return v.Default;
    }

    // ---- jogador ----

    static bool HasData(Player player) => Players.ContainsKey(player.Pointer);

    static PlayerData Data(Player player)
    {
        if (!Players.TryGetValue(player.Pointer, out PlayerData? d))
        {
            Activate();
            d = new PlayerData();
            Players[player.Pointer] = d;
            Reset(player, d);
        }
        else if (d.Stats.Length < Classes.Count)
        {
            Reset(player, d);
        }
        return d;
    }

    public static ref ClassStats Stats(Player player, DamageClass cls)
    {
        if (cls == null) throw new ArgumentNullException(nameof(cls));
        return ref Data(player).Stats[cls.Type];
    }

    static void Reset(Player player, PlayerData d)
    {
        int count = Classes.Count;
        if (d.Stats.Length != count)
        {
            d.Stats = new ClassStats[count];
            d.Applied = new ClassStats[count];
        }
        for (int i = 0; i < count; i++)
        {
            d.Stats[i] = ClassStats.Fresh();
            d.Applied[i] = ClassStats.Fresh();
        }
        for (int i = 0; i < count; i++)
        {
            DamageClass cls = Classes[i];
            if (cls.IsVanilla) continue;
            try
            {
                cls.SetDefaultStats(player);
            }
            catch (Exception e)
            {
                Log.Error($"damage class {cls.FullName}: SetDefaultStats threw: {e}");
            }
        }
    }

    // O total de uma classe: o dela mais o de cada classe herdada, como o
    // GetTotalDamage do tModLoader. As do jogo sao lidas dos campos do jogador.
    public static Totals Total(Player player, DamageClass cls)
    {
        if (cls == null) throw new ArgumentNullException(nameof(cls));
        PlayerData d = Data(player);
        Totals t = Effective(player, d, cls);
        for (int j = 0; j < Classes.Count; j++)
        {
            DamageClass other = Classes[j];
            if (other == cls) continue;
            StatInheritanceData? found = Inheritance(cls, other);
            if (found is not StatInheritanceData inh) continue;

            Totals s = Effective(player, d, other);
            t.Damage = t.Damage.CombineWith(s.Damage.Scale(inh.damageInheritance));
            t.Crit += s.Crit * inh.critChanceInheritance;
            t.Speed += (s.Speed - 1f) * inh.attackSpeedInheritance;
            t.ArmorPen += s.ArmorPen * inh.armorPenInheritance;
            t.Knockback = t.Knockback.CombineWith(s.Knockback.Scale(inh.knockbackInheritance));
        }
        return t;
    }

    // Os numeros de uma classe sozinha. As do jogo vem dos campos do jogador,
    // que ja tem o equipamento do jogo e o que foi dobrado; o Generic e a parte
    // comum aos quatro (o jogo soma "todo dano" em cada um).
    static Totals Effective(Player player, PlayerData d, DamageClass cls)
    {
        VanillaSet v = Vanilla;
        ref ClassStats own = ref d.Stats[cls.Type];
        bool folded = cls == v.Generic || cls == v.Melee || cls == v.Ranged || cls == v.Magic || cls == v.Summon;
        if (!folded)
        {
            return new Totals { Damage = own.Damage, Crit = own.Crit, Speed = own.Speed, ArmorPen = own.ArmorPen, Knockback = own.Knockback };
        }

        float melee = player.meleeDamage, ranged = player.rangedDamage, magic = player.magicDamage, minion = player.minionDamage;
        float common = Math.Min(Math.Min(melee, ranged), Math.Min(magic, minion));
        float commonCrit = Math.Min(Math.Min((float)player.meleeCrit, player.rangedCrit), player.magicCrit);
        float genericSpeed = d.Stats[v.Generic.Type].Speed;
        float add, crit, speed, pen;
        if (cls == v.Generic)
        {
            add = common;
            crit = commonCrit;
            speed = genericSpeed;
            pen = player.armorPenetration;
        }
        else if (cls == v.Melee)
        {
            add = 1f + melee - common;
            crit = player.meleeCrit - commonCrit;
            speed = d.HasSnap ? d.SnapMelee - genericSpeed + 1f : own.Speed;
            pen = player.meleeArmorPenetration;
        }
        else if (cls == v.Ranged)
        {
            add = 1f + ranged - common;
            crit = player.rangedCrit - commonCrit;
            speed = own.Speed;
            pen = own.ArmorPen;
        }
        else if (cls == v.Magic)
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

    // O que o mod somou nas classes do jogo vai para os campos do jogador.
    // Antes do CapAttackSpeeds: ele troca meleeSpeed (velocidade) por
    // 1/velocidade (multiplicador do tempo de uso).
    static void Fold(Player player)
    {
        if (!Players.TryGetValue(player.Pointer, out PlayerData? d)) return;
        VanillaSet v = Vanilla;

        // Generic: os quatro danos, os tres criticos, meleeSpeed, armorPenetration.
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

    // O que mudou numa classe desde a ultima dobra; true se algo mudou.
    static bool Delta(PlayerData d, DamageClass cls, out float add, out float mult, out int crit, out float speed, out int pen)
    {
        ref ClassStats cur = ref d.Stats[cls.Type];
        ref ClassStats app = ref d.Applied[cls.Type];
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

    // O Math.round do JS (meio sobe), para os numeros baterem com o loader JS.
    internal static int RoundHalfUp(float x) => (int)Math.Floor(x + 0.5f);

    // A penetracao da classe menos o que o jogo ja soma no acerto
    // (armorPenetration, e meleeArmorPenetration para o corpo a corpo).
    static int ExtraPen(Player player, DamageClass cls, bool melee)
    {
        float total = Total(player, cls).ArmorPen;
        return RoundHalfUp(total - player.armorPenetration - (melee ? player.meleeArmorPenetration : 0f));
    }

    // ---- nomes ----

    // O nome do tooltip: o DisplayName da classe, senao o da cultura do jogo
    // nas traducoes do mod, senao o nome separado.
    static readonly Dictionary<DamageClass, List<(string Culture, string Text)>> Names = new();

    static void LocalizeName(DamageClass cls)
    {
        if (cls.DisplayName != null) return;
        var texts = LocalizationLoader.Texts(cls.Mod, "DamageClasses", cls.Name + ".DisplayName");
        if (texts.Count > 0) Names[cls] = texts;
    }

    public static string NameOf(DamageClass cls)
    {
        if (cls.IsVanilla) return Terraria.Lang.tip[cls.VanillaTip].Value ?? "";
        string? own = Safe(cls, nameof(DamageClass.DisplayName), () => cls.DisplayName);
        if (own != null) return own;
        if (Names.TryGetValue(cls, out var texts)) return Pick(texts);
        return cls.PrettyPrintName();
    }

    static string Pick(List<(string Culture, string Text)> texts)
    {
        string culture = "";
        try
        {
            culture = Terraria.Localization.Language.ActiveCulture.Name ?? "";
        }
        catch (Exception)
        {
            // Sem a cultura (menu cedo demais): fica a primeira.
        }
        string language = culture.Split('-')[0];
        foreach (var (c, text) in texts) if (string.Equals(c, culture, StringComparison.OrdinalIgnoreCase)) return text;
        foreach (var (c, text) in texts) if (c.Split('-')[0].Equals(language, StringComparison.OrdinalIgnoreCase)) return text;
        foreach (var (c, text) in texts) if (c == "en-US") return text;
        return texts[0].Text;
    }

    // ---- hooks ----

    static void Activate()
    {
        if (installed) return;
        installed = true;
        Install();
    }

    static void Install()
    {
        VanillaSet v = Vanilla;
        bool MeleeLike(DamageClass cls) => cls == v.Melee || cls == v.MeleeNoSpeed || cls == v.SummonMeleeSpeed;

        On_Player.ResetEffects += (orig, self) =>
        {
            orig(self);
            RunStaticDefaults();
            if (Players.TryGetValue(self.Pointer, out PlayerData? d)) Reset(self, d);
        };

        On_Player.CapAttackSpeeds += (orig, self) =>
        {
            Fold(self);
            orig(self);
        };

        // O multiplicador de dano, que o GetWeaponDamage, a municao (PickAmmo) e
        // o tooltip do jogo usam. Base e Flat entram nele pelo dano do item:
        // assim o GetWeaponDamage do jogo segue sendo o caminho.
        On_Player.GetWeaponDamageMultiplier += (orig, self, item) =>
        {
            DamageClass cls = ItemClass(item);
            StatModifier t = Total(self, cls).Damage;
            float mult = IsHooked(cls) ? t.Additive * t.Multiplicative : orig(self, item);
            int damage = item.damage;
            if ((t.Base == 0f && t.Flat == 0f) || damage <= 0) return mult;
            return ((damage + t.Base) * mult + t.Flat) / damage;
        };

        On_Player.GetWeaponCrit += (orig, self, item) =>
        {
            int vanilla = orig(self, item);
            DamageClass cls = ItemClass(item);
            if (!IsHooked(cls)) return vanilla;
            return UsesCrit(cls) ? RoundHalfUp(Total(self, cls).Crit) + item.crit : 0;
        };

        On_Player.GetWeaponKnockback += (orig, self, item, knockBack) =>
        {
            float vanilla = orig(self, item, knockBack);
            StatModifier modifier = Total(self, ItemClass(item)).Knockback;
            return modifier == StatModifier.Default ? vanilla : modifier.ApplyTo(vanilla);
        };

        On_Player.ApplyItemAnimation += (orig, self, item) =>
        {
            orig(self, item);
            DamageClass cls = ItemClass(item);
            bool hooked = IsHooked(cls);
            if (!hooked && MeleeLike(cls)) return;
            float speed = Total(self, cls).Speed;
            if (!hooked && Math.Abs(speed - 1f) < 1e-6f) return;
            int baseFrames = hooked ? item.useAnimation : self.itemAnimationMax;
            int frames = Math.Max(1, (int)(baseFrames / Math.Min(Math.Max(speed, 0.01f), 3f)));
            self.itemAnimation = frames;
            self.itemAnimationMax = frames;
        };

        On_Player.ApplyItemTime_Item += (orig, self, item) =>
        {
            orig(self, item);
            DamageClass cls = ItemClass(item);
            if (!IsHooked(cls) && MeleeLike(cls)) return;
            float speed = Total(self, cls).Speed;
            if (Math.Abs(speed - 1f) < 1e-6f) return;
            int frames = Math.Max(1, (int)(item.useTime / Math.Min(Math.Max(speed, 0.01f), 3f)));
            self.itemTime = frames;
            self.itemTimeMax = frames;
        };

        // Golpe de item: a penetracao da classe que o jogo nao soma sozinho.
        On_Player.ProcessHitAgainstNPC += (orig, self, item, itemRectangle, originalDamage, knockBack, npcIndex) =>
        {
            int extra = ExtraPen(self, ItemClass(item), item.melee);
            if (extra <= 0)
            {
                orig(self, item, itemRectangle, originalDamage, knockBack, npcIndex);
                return;
            }
            self.armorPenetration += extra;
            try
            {
                orig(self, item, itemRectangle, originalDamage, knockBack, npcIndex);
            }
            finally
            {
                self.armorPenetration -= extra;
            }
        };

        // Projetil: a penetracao da classe vai nele ao nascer.
        On_Projectile.NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float_NewProjectileModifier +=
            (orig, spawnSource, x, y, speedX, speedY, type, damage, knockBack, owner, ai0, ai1, ai2, modifier) =>
            {
                int i = orig(spawnSource, x, y, speedX, speedY, type, damage, knockBack, owner, ai0, ai1, ai2, modifier);
                if (i < 0 || i >= 1000) return i;
                Projectile p = Main.projectile[i];
                int who = p.owner;
                if (!p.active || who < 0 || who >= 255) return i;
                Player player = Main.player[who];
                if (player.Pointer == 0 || !player.active || !HasData(player)) return i;
                int extra = ExtraPen(player, ProjectileClass(p), p.melee);
                if (extra > 0) p.armorPenetration += extra;
                return i;
            };

        // Projetil de gancho: o critico sorteado de novo pela classe, logo
        // depois do sorteio do jogo (que foi pelas flags).
        On_Projectile.Damage += (orig, self) =>
        {
            if (!hookedProjectiles)
            {
                orig(self);
                return;
            }
            Projectile outer = firing;
            bool wasFiring = isFiring;
            firing = self;
            isFiring = true;
            try
            {
                orig(self);
            }
            finally
            {
                firing = outer;
                isFiring = wasFiring;
            }
        };

        On_Player.TryConsumingTimerCrit += (orig, self, hitbox, ref crit) =>
        {
            if (isFiring)
            {
                DamageClass cls = ProjectileClass(firing);
                if (IsHooked(cls))
                {
                    float chance = UsesCrit(cls) ? Total(self, cls).Crit + firing.bonusCritChance : -1f;
                    Item held = self.HeldItem;
                    if (chance >= 0f && held.Pointer != 0 && ItemClass(held) == cls) chance += held.crit;
                    crit = chance >= 0f && Random.Shared.NextDouble() * 100.0 < chance;
                }
            }
            orig(self, hitbox, ref crit);
        };

        // Lacaio e sentinela: o jogo refaz o dano com o minionDamage a cada quadro.
        On_Projectile.Update += (orig, self, i) =>
        {
            orig(self, i);
            if (!hookedProjectiles || !self.active || !(self.minion || self.sentry) || self.originalDamage <= 0) return;
            DamageClass cls = ProjectileClass(self);
            if (!IsHooked(cls)) return;
            int who = self.owner;
            if (who < 0 || who >= 255) return;
            Player owner = Main.player[who];
            if (owner.Pointer == 0) return;
            self.damage = (int)(Total(owner, cls).Damage.ApplyTo(self.originalDamage) + 5e-6f);
        };

        DamageClassTooltips.Install();
    }

    // ---- o que os mods veem no jogador ----

    public static float WeaponAttackSpeed(Player player, Item item)
    {
        float speed = Total(player, ItemClass(item)).Speed;
        return 1f + (speed - 1f) * ItemID.Sets.BonusMeleeSpeedMultiplier[item.type];
    }

    public static int WeaponArmorPenetration(Player player, Item item) =>
        (int)(item.armorPenetration + Total(player, ItemClass(item)).ArmorPen);

    internal static StatModifier? KnockbackIfAny(Player player, DamageClass cls) =>
        HasData(player) ? Total(player, cls).Knockback : null;
}
