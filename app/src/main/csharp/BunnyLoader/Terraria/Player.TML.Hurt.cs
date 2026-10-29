using System;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Terraria;

public readonly partial struct Player
{
    public struct HurtModifiers
    {
        public PlayerDeathReason DamageSource { get; init; } = default;

        public bool PvP { get; init; } = default;

        public int CooldownCounter { get; init; } = -1;

        public bool Dodgeable { get; init; } = true;

        public int HitDirection { get; init; } = default;

        public StatModifier SourceDamage = new();

        public MultipliableFloat IncomingDamageMultiplier = new();

        public StatModifier FinalDamage = new();

        public AddableFloat ArmorPenetration = new();

        public AddableFloat ScalingArmorPenetration = new();

        private int _damageLimit = int.MaxValue;

        public void SetMaxDamage(int limit) => _damageLimit = Math.Min(_damageLimit, Math.Max(limit, 1));

        public StatModifier Knockback = new();

        public int? HitDirectionOverride { private get; set; } = default;

        public MultipliableFloat KnockbackImmunityEffectiveness = new();

        private bool _cancelled = default;

        public void Cancel() => _cancelled = true;

        private bool _dustDisabled = default;

        public void DisableDust() => _dustDisabled = true;

        private bool _soundDisabled = default;

        public void DisableSound() => _soundDisabled = true;

        public delegate void HurtInfoModifier(ref HurtInfo info);

        public event HurtInfoModifier? ModifyHurtInfo = null;

        public HurtModifiers() { }

        internal readonly bool Cancelled => _cancelled;

        internal readonly bool ChangesAfterDefense =>
            ArmorPenetration.Value != 0 || ScalingArmorPenetration.Value != 0 || _damageLimit != int.MaxValue || FinalDamage != StatModifier.Default;

        public float GetDamage(float baseDamage, float defense, float defenseEffectiveness)
        {
            float damage = SourceDamage.ApplyTo(baseDamage) * IncomingDamageMultiplier.Value;
            float armorPenetration = defense * Math.Clamp(ScalingArmorPenetration.Value, 0, 1) + ArmorPenetration.Value;
            defense = Math.Max(defense - armorPenetration, 0);
            float damageReduction = defense * defenseEffectiveness;
            damage = Math.Max(damage - damageReduction, 1);
            return Math.Clamp((int)FinalDamage.ApplyTo(damage), 1, _damageLimit);
        }

        public float GetKnockback(float baseKnockback, bool knockbackImmune)
        {
            float knockback = Math.Max(Knockback.ApplyTo(baseKnockback), 0f);
            if (knockbackImmune)
                knockback *= 1 - Math.Clamp(KnockbackImmunityEffectiveness.Value, 0, 1);
            return knockback;
        }

        public HurtInfo ToHurtInfo(int damage, int defense, float defenseEffectiveness, float knockback, bool knockbackImmune)
        {
            var hurtInfo = new HurtInfo
            {
                DamageSource = DamageSource,
                PvP = PvP,
                CooldownCounter = CooldownCounter,
                Dodgeable = Dodgeable,
                HitDirection = HitDirectionOverride ?? HitDirection,
                SourceDamage = (int)SourceDamage.ApplyTo(damage),
                Damage = (int)GetDamage(damage, defense, defenseEffectiveness),
                Knockback = GetKnockback(knockback, knockbackImmune),
                Cancelled = _cancelled,
                DustDisabled = _dustDisabled,
                SoundDisabled = _soundDisabled,
            };
            ModifyHurtInfo?.Invoke(ref hurtInfo);
            ModifyHurtInfo = null;
            return hurtInfo;
        }
    }

    public struct HurtInfo
    {
        public PlayerDeathReason DamageSource = default;

        public bool PvP = false;

        public int CooldownCounter = -1;

        public bool Dodgeable = true;

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

        public int HitDirection = 0;

        public float Knockback = 0;

        public bool Cancelled = false;

        public bool DustDisabled = false;

        public bool SoundDisabled = false;

        public HurtInfo() { }
    }
}
