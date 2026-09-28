const NewText = Terraria.Main['void NewText(string newText, byte R, byte G, byte B, bool onlyCurrentPlayer)'];

export class ExamplePlayer extends ModPlayer {
    ExampleDefenseDebuff = false;
    DefenseDebuffMultiplier = 1;

    OnEnterWorld(player) {
        if (player.whoAmI !== Terraria.Main.myPlayer) return;
        const welcome = ModLocalization.Translate('CustomText.WelcomeMessage');
        NewText(welcome.replace('{WorldName}', Terraria.Main.worldName), 255, 200, 0, false);
    }

    ResetEffects(player) {
        this.ExampleDefenseDebuff = false;
    }

    UpdateEquips(player) {
        if (this.ExampleDefenseDebuff) {
            player.statDefense = Math.floor(player.statDefense * this.DefenseDebuffMultiplier);
        }
    }
}
