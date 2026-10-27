using BunnyLoader;
using ExampleMod.Content.DamageClasses;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons;

// Uma espada com a classe de dano de exemplo. O tooltip mostra "dano de
// exemplo", o critico da classe (4% + o do item) e esconde a velocidade.
// A receita chega com o loader de receitas.
public class ExampleCustomDamageWeapon : ModItem
{
    // A textura da espada de exemplo.
    public override string Texture => "Content/Items/Weapons/Melee/ExampleMeleeWeapon";

    public override void SetDefaults()
    {
        Item.DamageType = ModContent.GetInstance<ExampleDamageClass>();
        Item.width = 40;
        Item.height = 40;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.useTime = 30;
        Item.useAnimation = 30;
        Item.autoReuse = true;
        Item.damage = 70;
        Item.knockBack = 4;
        Item.crit = 6;
        Item.value = Item.buyPrice(gold: 1);
        // Verde. O ItemRarityID nao existe no jogo do celular.
        Item.rare = 2;
        Item.UseSound = SoundID.Item1;
    }
}
