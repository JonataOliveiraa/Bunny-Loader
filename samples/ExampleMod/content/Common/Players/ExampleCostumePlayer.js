// O jogador com o ExampleCostume: perto de moradores ganha o buff Blocky
// (pulo alto), e com o visual ligado veste as texturas do fantasia no
// FrameEffects (as da água, molhado).
export class ExampleCostumePlayer extends ModPlayer {
    BlockyAccessoryPrevious = false;
    BlockyAccessory = false;     // o fantasia equipado (os efeitos)
    BlockyHideVanity = false;    // num slot com o visual escondido
    BlockyForceVanity = false;   // no slot de vaidade: só o visual
    BlockyPower = false;         // o buff valendo

    get BlockyVanityEffects() {
        return this.BlockyForceVanity || (this.BlockyPower && !this.BlockyHideVanity);
    }

    ResetEffects(player) {
        this.BlockyAccessoryPrevious = this.BlockyAccessory;
        this.BlockyAccessory = this.BlockyHideVanity = this.BlockyForceVanity = this.BlockyPower = false;
    }

    UpdateEquips(player) {
        if (player.townNPCs >= 1 && this.BlockyAccessory) player.AddBuff(ModContent.BuffType('Blocky'), 60, true);
    }

    FrameEffects(player) {
        if (!this.BlockyVanityEffects) return;

        const name = player.wet ? 'BlockyAlt' : 'ExampleCostume';
        player.head = EquipLoader.GetEquipSlot(name, EquipType.Head);
        player.body = EquipLoader.GetEquipSlot(name, EquipType.Body);
        player.legs = EquipLoader.GetEquipSlot(name, EquipType.Legs);
    }
}
