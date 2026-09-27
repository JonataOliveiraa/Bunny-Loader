// Um manto: a textura do corpo vem pelo autoload, e a das pernas é desenhada
// no lugar das calças (o SetMatch com robes).
export class ExampleRobe extends ModItem {
    static AutoloadEquip = [EquipType.Body];

    Load() {
        EquipLoader.AddEquipTexture(this.Mod, this.Texture + '_Legs', EquipType.Legs, this);
    }

    SetStaticDefaults() {
        Terraria.ID.ArmorIDs.Body.Sets.HidesHands[this.Item.bodySlot] = false;
    }

    SetDefaults() {
        this.Item.width = 18;
        this.Item.height = 14;
        this.Item.rare = ItemRarityID.Blue;
        this.Item.vanity = true;
    }

    SetMatch(male, equipSlot, robes) {
        robes.value = true;
        equipSlot.value = EquipLoader.GetEquipSlot(this.Mod, this.constructor.name, EquipType.Legs);
    }
}
