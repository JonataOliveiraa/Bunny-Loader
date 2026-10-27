using BunnyLoader;
using Terraria;
using Terraria.ID;

namespace ExampleMod.Content.Items.Weapons.Melee;

// A espada do ExampleMod. Os numeros sao os do original.
public class ExampleMeleeWeapon : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 40;
        Item.height = 40;

        Item.useStyle = ItemUseStyleID.Swing;
        Item.useTime = 20;
        Item.useAnimation = 20;
        Item.autoReuse = true;

        Item.melee = true;
        // (dano, repulsao, critico)
        Item.SetWeaponValues(50, 6, 6);

        Item.value = Item.sellPrice(gold: 1);
        // Verde. O ItemRarityID nao existe no jogo do celular.
        Item.rare = 2;
        Item.UseSound = SoundID.Item1;
    }
}
