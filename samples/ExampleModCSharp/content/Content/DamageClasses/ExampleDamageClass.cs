using BunnyLoader;
using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Content.DamageClasses;

// A classe de dano do ExampleMod: herda os bonus de "todo dano" (Generic),
// conta como corpo a corpo e magica para os efeitos do jogo, e tem 4% de
// critico e 10 de penetracao de armadura a mais. O nome do tooltip ("dano de
// exemplo") vem de Localization/<idioma>.json, secao DamageClasses.
public class ExampleDamageClass : DamageClass
{
    // Quanto dos bonus de cada outra classe esta herda. Aqui: tudo do Generic
    // e nada das outras (o padrao, escrito por extenso como no original).
    public override StatInheritanceData GetModifierInheritance(DamageClass damageClass)
    {
        if (damageClass == DamageClass.Generic)
            return StatInheritanceData.Full;

        return new StatInheritanceData(
            damageInheritance: 0f,
            critChanceInheritance: 0f,
            attackSpeedInheritance: 0f,
            armorPenInheritance: 0f,
            knockbackInheritance: 0f
        );
    }

    // Para os efeitos do jogo (Pedra de Magma, Spectre...) e para os prefixos,
    // esta classe conta como corpo a corpo e magica.
    public override bool GetEffectInheritance(DamageClass damageClass)
    {
        if (damageClass == DamageClass.Melee)
            return true;
        if (damageClass == DamageClass.Magic)
            return true;

        return false;
    }

    // Os bonus de base de todo jogador, a cada quadro.
    public override void SetDefaultStats(Player player)
    {
        player.GetCritChance<ExampleDamageClass>() += 4;
        player.GetArmorPenetration<ExampleDamageClass>() += 10;
    }

    // Esta classe tem critico.
    public override bool UseStandardCritCalcs => true;

    // A linha de velocidade some do tooltip das armas desta classe.
    public override bool ShowStatTooltipLine(Player player, string lineName)
    {
        if (lineName == "Speed")
            return false;

        return true;
    }
}
