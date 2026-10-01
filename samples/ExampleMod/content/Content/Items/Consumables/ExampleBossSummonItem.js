const { ItemUseStyleID, SoundID } = Terraria.ID;
const PlaySound = Terraria.Audio.SoundEngine['void PlaySound(int type, Vector2 position, int style, float pitchOffset)'];

export class ExampleBossSummonItem extends ModItem {
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

    // true: o item conta como usado, e o jogo gasta um (consumable).
    UseItem(item, player) {
        if (player.whoAmI !== Terraria.Main.myPlayer) return true;
        PlaySound(SoundID.Roar, player.position, 0, 0);

        const type = ModNPC.getTypeByName('ExampleBoss');
        if (Terraria.Main.netMode !== 1) {
            Terraria.NPC.SpawnOnPlayer(player.whoAmI, type, 0, 0, 0, 0);
        } else {
            // No cliente, quem cria o chefe é o servidor: a mensagem 61 (a dos
            // itens de invocação do jogo) pede. Vale porque o ExampleBoss está
            // em NPCID.Sets.MPAllowedEnemies.
            Terraria.NetMessage['void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)'](
                61, -1, -1, null, player.whoAmI, type, 0, 0, 0, 0, 0);
        }
        return true;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModItem.getTypeByName('ExampleItem'), 10)
            .AddTile(Terraria.ID.TileID.DemonAltar)
            .Register();
    }
}
