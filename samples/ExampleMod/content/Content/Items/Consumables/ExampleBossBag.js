// A bolsa de tesouro do Chefe de Exemplo (o MinionBossBag do tModLoader): o
// chefe a solta no Expert e no Mestre, e ela traz o que ele daria no Clássico.
// O ItemID.Sets.PreHardmodeLikeBossBag do tModLoader não existe no celular.
const { ItemID } = Terraria.ID;
const { ItemDropRule } = Terraria.GameContent.ItemDropRules;

export class ExampleBossBag extends ModItem {
    SetStaticDefaults() {
        // Brilha quando está no chão, como as bolsas do jogo.
        ItemID.Sets.BossBag[this.Type] = true;
    }

    SetDefaults() {
        this.Item.maxStack = ModItem.CommonMaxStack;
        this.Item.consumable = true;
        this.Item.width = 24;
        this.Item.height = 24;
        this.Item.rare = ItemRarityID.Purple;
        this.Item.expert = true;   // "Expert" no tooltip e a cor do nome
    }

    CanRightClick(item) {
        return true;
    }

    // O mesmo do modo Clássico do chefe (ExampleBoss.ModifyNPCLoot), e as moedas dele.
    ModifyItemLoot(itemLoot) {
        itemLoot.Add(ItemDropRule.NotScalingWithLuck(ModContent.ItemType('ExampleBossMask'), 7, 1, 1));
        itemLoot.Add(ItemDropRule.Common(ModContent.ItemType('ExampleItem'), 1, 15, 30));
        itemLoot.Add(ItemDropRule.CoinsBasedOnNPCValue(ModContent.NPCType('ExampleBoss')));
    }
}
