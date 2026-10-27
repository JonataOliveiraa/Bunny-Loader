using BunnyLoader;
using BunnyLoader.Loaders;

namespace Terraria.ModLoader;

// O jogo do celular nao tem DamageClass: o loader (Loaders/DamageClassLoader)
// liga as do jogo as flags e aos campos do jogador, e as outras aos hooks.

/// <summary>
/// Uma classe de dano, como a do tModLoader: de quais outras ela herda bonus,
/// como quais conta para os efeitos do jogo e de quais aceita os prefixos.
/// As 10 do jogo sao estaticas (DamageClass.Melee...); as de mod entram
/// sozinhas e saem de ModContent.GetInstance&lt;T&gt;().
/// </summary>
public abstract class DamageClass : ModType
{
    public static DamageClass Default => DamageClassLoader.Vanilla.Default;
    public static DamageClass Generic => DamageClassLoader.Vanilla.Generic;
    public static DamageClass Melee => DamageClassLoader.Vanilla.Melee;
    public static DamageClass MeleeNoSpeed => DamageClassLoader.Vanilla.MeleeNoSpeed;
    public static DamageClass Ranged => DamageClassLoader.Vanilla.Ranged;
    public static DamageClass Magic => DamageClassLoader.Vanilla.Magic;
    public static DamageClass Summon => DamageClassLoader.Vanilla.Summon;
    public static DamageClass SummonMeleeSpeed => DamageClassLoader.Vanilla.SummonMeleeSpeed;
    public static DamageClass MagicSummonHybrid => DamageClassLoader.Vanilla.MagicSummonHybrid;
    public static DamageClass Throwing => DamageClassLoader.Vanilla.Throwing;

    /// <summary>A posicao no loader: 0 a 9 sao as do jogo, na ordem do tModLoader.</summary>
    public int Type { get; internal set; }

    /// <summary>
    /// O nome no tooltip ("15 dano de exemplo"). null = as traducoes do mod
    /// (secao DamageClasses, chave &lt;Name&gt;.DisplayName); sem elas, o nome
    /// separado. Provisorio: vira LocalizedText com a localizacao da fase 0.
    /// </summary>
    public virtual string? DisplayName => null;

    /// <summary>Quanto dos bonus de damageClass esta classe herda. Padrao: tudo do Generic.</summary>
    public virtual StatInheritanceData GetModifierInheritance(DamageClass damageClass) =>
        damageClass == Generic ? StatInheritanceData.Full : StatInheritanceData.None;

    /// <summary>Se esta classe conta como damageClass para os efeitos do jogo.</summary>
    public virtual bool GetEffectInheritance(DamageClass damageClass) => false;

    /// <summary>Se o item desta classe aceita os prefixos de damageClass. Padrao: os das que ela conta como.</summary>
    public virtual bool GetPrefixInheritance(DamageClass damageClass) => GetEffectInheritance(damageClass);

    /// <summary>Os bonus de base de cada jogador, postos de novo a cada quadro (depois do ResetEffects).</summary>
    public virtual void SetDefaultStats(Player player) { }

    /// <summary>false: sem critico (como as de lacaio).</summary>
    public virtual bool UseStandardCritCalcs => true;

    /// <summary>lineName: "Damage", "CritChance", "Speed" ou "Knockback".</summary>
    public virtual bool ShowStatTooltipLine(Player player, string lineName) => true;

    public bool CountsAsClass(DamageClass damageClass) => DamageClassLoader.CountsAs(this, damageClass);

    public bool CountsAsClass<T>() where T : DamageClass => CountsAsClass(ModContent.GetInstance<T>());

    public bool GetsPrefixesFor(DamageClass damageClass) =>
        this == damageClass || DamageClassLoader.Safe(this, nameof(GetPrefixInheritance), () => GetPrefixInheritance(damageClass));

    internal sealed override void Register() => DamageClassLoader.Register(this);

    // O texto do jogo (Lang.tip) das classes do jogo; -1 nas de mod.
    internal virtual int VanillaTip => -1;

    internal bool IsVanilla => VanillaTip >= 0;
}
