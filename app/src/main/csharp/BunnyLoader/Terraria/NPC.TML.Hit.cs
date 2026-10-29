using System;
using Terraria.ModLoader;

namespace Terraria;

public readonly partial struct NPC
{
    public struct HitModifiers
    {
        public DamageClass DamageType { get; init; } = DamageClass.Default;

        public int HitDirection { get; init; } = default;

        public bool SuperArmor { get; init; } = false;

        public StatModifier SourceDamage = new();

        public AddableFloat FlatBonusDamage = new();

        public AddableFloat ScalingBonusDamage = new();

        public MultipliableFloat TargetDamageMultiplier = new();

        public StatModifier Defense = new();

        public AddableFloat ArmorPenetration = new();

        public AddableFloat ScalingArmorPenetration = new();

        public MultipliableFloat DefenseEffectiveness = MultipliableFloat.One * 0.5f;

        public StatModifier CritDamage = new(2f, 1f);

        public StatModifier NonCritDamage = new();

        public StatModifier FinalDamage = new();

        public MultipliableFloat DamageVariationScale = new();

        private int _damageLimit = int.MaxValue;
        public void SetMaxDamage(int limit) => _damageLimit = Math.Min(_damageLimit, Math.Max(limit, 1));

        private bool? _critOverride = default;

        public void DisableCrit() => _critOverride = false;

        public void SetCrit() => _critOverride ??= true;

        private bool _knockbackDisabled = false;

        public void DisableKnockback() => _knockbackDisabled = true;

        public StatModifier Knockback = new();

        public int? HitDirectionOverride { private get; set; } = default;

        private bool _instantKill = default;
        public void SetInstantKill() => _instantKill = true;

        private bool _combatTextHidden = default;
        public void HideCombatText() => _combatTextHidden = true;

        public delegate void HitInfoModifier(ref HitInfo info);
        public event HitInfoModifier? ModifyHitInfo = null;

        public HitModifiers() { }

        public readonly int GetDamage(float baseDamage, bool crit, bool damageVariation = false, float luck = 0f)
        {
            crit = _critOverride ?? crit;
            if (SuperArmor)
            {
                float dmg = 1;
                if (crit)
                    dmg *= CritDamage.Additive * CritDamage.Multiplicative;

                return Math.Clamp((int)dmg, 1, Math.Min(_damageLimit, 4));
            }

            float damage = SourceDamage.ApplyTo(baseDamage);
            damage += FlatBonusDamage.Value + ScalingBonusDamage.Value * damage;
            damage *= TargetDamageMultiplier.Value;

            int variationPercent = Math.Clamp((int)Math.Round(15 * DamageVariationScale.Value), 0, 100);
            if (damageVariation && variationPercent > 0)
                damage = variationPercent == 15 ? Main.DamageVar(damage, luck) : damage * (1 + Random.Shared.Next(-variationPercent, variationPercent + 1) / 100f);

            float defense = Math.Max(Defense.ApplyTo(0), 0);
            float armorPenetration = defense * Math.Clamp(ScalingArmorPenetration.Value, 0, 1) + ArmorPenetration.Value;
            defense = Math.Max(defense - armorPenetration, 0);

            float damageReduction = defense * DefenseEffectiveness.Value;
            damage = Math.Max(damage - damageReduction, 1);

            damage = (crit ? CritDamage : NonCritDamage).ApplyTo(damage);

            return Math.Clamp((int)FinalDamage.ApplyTo(damage), 1, _damageLimit);
        }

        public readonly float GetKnockback(float baseKnockback) => _knockbackDisabled ? 0 : Math.Max(Knockback.ApplyTo(baseKnockback), 0);

        public HitInfo ToHitInfo(float baseDamage, bool crit, float baseKnockback, bool damageVariation = false, float luck = 0f)
        {
            var hitInfo = new HitInfo()
            {
                DamageType = DamageType ?? DamageClass.Default,
                SourceDamage = Math.Max((int)SourceDamage.ApplyTo(baseDamage), 1),
                Damage = _instantKill ? 1 : GetDamage(baseDamage, crit, damageVariation, luck),
                Crit = _critOverride ?? crit,
                Knockback = GetKnockback(baseKnockback),
                HitDirection = HitDirectionOverride ?? HitDirection,
                InstantKill = _instantKill,
                HideCombatText = _combatTextHidden
            };

            ModifyHitInfo?.Invoke(ref hitInfo);
            ModifyHitInfo = null;
            return hitInfo;
        }
    }

    public struct HitInfo
    {
        public DamageClass DamageType = DamageClass.Default;

        private int _sourceDamage = 1;
        public int SourceDamage
        {
            readonly get => _sourceDamage;
            set => _sourceDamage = Math.Max(value, 1);
        }

        private int _damage = 1;
        public int Damage
        {
            readonly get => _damage;
            set => _damage = Math.Max(value, 1);
        }

        public bool Crit = false;

        public int HitDirection = 0;

        public float Knockback = 0;

        public bool InstantKill = false;

        public bool HideCombatText = false;

        public HitInfo() { }
    }
}
