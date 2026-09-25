const NewText = Terraria.Main['void NewText(string newText, byte R, byte G, byte B)'];

export class ExamplePlayer extends ModPlayer {
    ExampleDefenseDebuff = false;
    DefenseDebuffMultiplier = 1;

    OnEnterWorld(player) {
        if (player.whoAmI !== Terraria.Main.myPlayer) return;
        const key = ModLocalization.Translate('CustomText.WelcomeMessage');
        const welcome = Terraria.Localization.Language['string GetTextValue(string key)'](key);
        NewText(welcome.replace('{WorldName}', Terraria.Main.worldName), 255, 200, 0);
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
