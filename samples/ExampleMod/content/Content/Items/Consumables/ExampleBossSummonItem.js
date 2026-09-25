const { ItemUseStyleID, SoundID } = Terraria.ID;
const PlaySound = Terraria.Audio.SoundEngine['void PlaySound(int type, Vector2 position, int style, float pitchOffset)'];

export class ExampleBossSummonItem extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Consumables/' + this.constructor.name;
    }

    SetDefaults() {
        this.Item.width = 30;
        this.Item.height = 20;
        this.Item.maxStack = ModItem.CommonMaxStack;
        this.Item.value = Terraria.Item.sellPrice(0, 0, 1, 0);
        this.Item.rare = ItemRarityID.Blue;
        this.Item.useAnimation = 30;
        this.Item.useTime = 30;
        this.Item.useStyle = ItemUseStyleID.HoldUp;
        this.Item.consumable = true;
    }

    CanUseItem(item, player) {
        return !Terraria.Main.dayTime && !Terraria.NPC.AnyNPCs(ModNPC.getTypeByName('ExampleBoss'));
    }

    UseItem(item, player) {
        if (player.whoAmI !== Terraria.Main.myPlayer) return;
        PlaySound(SoundID.Roar, player.position, 0, 0);
        Terraria.NPC.SpawnOnPlayer(player.whoAmI, ModNPC.getTypeByName('ExampleBoss'), 0, 0, 0, 0);
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModItem.getTypeByName('ExampleItem'), 10)
            .AddTile(Terraria.ID.TileID.DemonAltar)
            .Register();
    }
}
