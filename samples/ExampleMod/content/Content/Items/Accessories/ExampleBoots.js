export class ExampleBoots extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Accessories/' + this.constructor.name;
        this.MoveSpeedBonus = 8;
        this.LavaImmunityTime = 2;
    }

    SetDefaults() {
        this.Item.width = 28;
        this.Item.height = 24;
        this.Item.accessory = true;
        this.Item.rare = ItemRarityID.Red;
        this.Item.value = Terraria.Item.sellPrice(0, 2, 0, 0);
    }

    ModifyTooltipLines() {
        for (let i = 0; i < this.TooltipLines.length; i++) {
            this.TooltipLines[i] = this.TooltipLines[i].replace('{0}', this.LavaImmunityTime);
        }
    }

    UpdateAccessory(item, player, vanity, hideVisual) {
        if (!vanity) {
            player.moveSpeed += this.MoveSpeedBonus / 100;
            player.accRunSpeed = 6.75;
            player.rocketBoots = 2;
            player.vanityRocketBoots = 2;
            player.waterWalk2 = true;
            player.waterWalk = true;
            player.iceSkate = true;
            player.desertBoots = true;
            player.fireWalk = true;
            player.noFallDmg = true;
            player.lavaRose = true;
            player.lavaMax += this.LavaImmunityTime * 60;
        }

        if (vanity || !hideVisual) {
            player['void CancelAllBootRunVisualEffects()']();
            player.hellfireTreads = true;
            const grounded = player.velocity.Y === 0 && Math.abs(player.velocity.X) > 1;
            if (grounded && (!player.mount.Active || player.mount.Type !== Terraria.ID.MountID.WallOfFleshGoat)) {
                const x = Math.floor(player.Center.X / 16);
                const y = Math.floor((player.position.Y + player.height) / 16);
                player['bool DoBootsEffect_PlaceFlamesOnTile(int X, int Y)'](x, y);
            }
        }
    }
}
