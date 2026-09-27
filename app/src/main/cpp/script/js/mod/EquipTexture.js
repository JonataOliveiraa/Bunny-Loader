// Uma textura vestida registrada (o EquipTexture do tModLoader). Cada gancho
// repassa ao ModItem dono; um mod estende para mudar só a textura, como a
// cabeça do ExampleCostume, que é conjunto de vaidade sozinha.
class EquipTexture {
    Texture = '';    // o arquivo
    Name = '';
    Type = '';       // EquipType
    Slot = -1;
    Item = null;     // o ModItem dono (o molde), ou null

    FrameEffects(player, type) {
        if (this.Item) this.Item.EquipFrameEffects(player, type);
    }

    // head, body, legs: os SLOTS vestidos (player.head...), não itens.
    IsVanitySet(head, body, legs) {
        return this.Item ? this.Item.IsVanitySet(head, body, legs) : false;
    }

    PreUpdateVanitySet(player) {
        if (this.Item) this.Item.PreUpdateVanitySet(player);
    }

    UpdateVanitySet(player) {
        if (this.Item) this.Item.UpdateVanitySet(player);
    }

    ArmorSetShadows(player) {
        if (this.Item) this.Item.ArmorSetShadows(player);
    }

    // equipSlot e robes são Ref (.value).
    SetMatch(male, equipSlot, robes) {
        if (this.Item) this.Item.SetMatch(male, equipSlot, robes);
    }

    // Para asas sem item vestido (a textura posta pelo FrameEffects).
    VerticalWingSpeeds(player, ascentWhenFalling, ascentWhenRising, maxCanAscendMultiplier, maxAscentMultiplier, constantAscend) {
        if (this.Item) {
            this.Item.VerticalWingSpeeds(null, player, ascentWhenFalling, ascentWhenRising, maxCanAscendMultiplier,
                maxAscentMultiplier, constantAscend);
        }
    }

    HorizontalWingSpeeds(player, speed, acceleration) {
        if (this.Item) this.Item.HorizontalWingSpeeds(null, player, speed, acceleration);
    }

    // true: o mod anima as asas (o WingFrame do jogo não roda).
    WingUpdate(player, inUse) {
        return this.Item ? this.Item.WingUpdate(player, inUse) === true : false;
    }
}
