const { BuffID } = Terraria.ID;
const Main = Terraria.Main;
const LocalizedDuration = Terraria.Lang['string LocalizedDuration(TimeSpan time, bool abbreviated, bool showAllAvailableUnits)'];
const FromSeconds = System.TimeSpan['TimeSpan FromSeconds(double value)'];
const NewText = Main['void NewText(string newText, byte R, byte G, byte B, bool onlyCurrentPlayer)'];

// Código para todos os buffs, os do jogo também (o ExampleGlobalBuff do tModLoader).
export class ExampleGlobalBuff extends GlobalBuff {
    static #randomText = '';
    static #randomType = 0;

    // Com mais de 5 buffs, o Congelado (Chilled) dura no máximo 3 segundos.
    UpdatePlayer(type, player, buffIndex) {
        if (type !== BuffID.Chilled || buffIndex.value < 5) return;

        const limit = 3 * 60;
        if (player.buffTime[buffIndex.value] > limit) player.buffTime[buffIndex.value] = limit;
    }

    PreDraw(spriteBatch, type, buffIndex, drawParams) {
        // A Fogueira muda de cor e treme.
        if (type === BuffID.Campfire) {
            drawParams.DrawColor = Color.Multiply(Color.new(Main.DiscoR, Main.DiscoG, Main.DiscoB), Main.buffAlpha[buffIndex]);

            const shakeX = Rand.Next(-2, 3), shakeY = Rand.Next(-2, 3);
            drawParams.Position.X += shakeX;
            drawParams.Position.Y += shakeY;
            drawParams.TextPosition.X += shakeX;
            drawParams.TextPosition.Y += shakeY;
        }

        // O texto do tempo sobe e desce 6 pixels na Regeneração, na Pele de
        // Ferro e na Rapidez, cada ícone 4 quadros atrás do anterior.
        if (Main.EquipPage !== 2 && (type === BuffID.Regeneration || type === BuffID.Ironskin || type === BuffID.Swiftness)) {
            const interval = 60;
            const time = ((Main.GameUpdateCount + 4 * buffIndex) % interval) / interval;
            drawParams.TextPosition.Y += Math.trunc(6 * time);
        }

        return true;
    }

    ModifyBuffText(type, buffName, tip, rare) {
        const player = Main.player[Main.myPlayer];
        const index = player.FindBuffIndex(type);
        if (index < 0 || index >= player.buffTime.length) return;

        // O tempo que falta, em todo buff que tem tempo. A Defesa de Exemplo já
        // mostra o dela (ModifyBuffText do próprio ModBuff).
        const time = new Ref(0);
        if (type !== ModContent.BuffType('ExampleDefenseBuff') && Main.TryGetBuffTime(index, time) && time.value > 2) {
            const text = LocalizedDuration(FromSeconds(Math.floor(time.value / 60)), false, true);
            tip.value += '\n' + ModLocalization.Translate('CustomText.RemainingTime').replace('{0}', text);
        }

        // Com Sugar Rush (a fatia de bolo), o nome dos buffs embaralha.
        if (player.FindBuffIndex(BuffID.SugarRush) >= 0 && buffName.value.length > 2) {
            if (Main.GameUpdateCount % 10 === 0 || ExampleGlobalBuff.#randomType !== type) {
                if (ExampleGlobalBuff.#randomType !== type) {
                    ExampleGlobalBuff.#randomText = buffName.value;
                    ExampleGlobalBuff.#randomType = type;
                }
                const characters = [...ExampleGlobalBuff.#randomText];
                const n = characters.length;
                const swaps = Rand.Next(1, Math.floor(n / 2));
                for (let swap = 0; swap < swaps; swap++) {
                    const a = Rand.Next(n - 1);
                    const b = Rand.Next(a + 1, n);
                    [characters[a], characters[b]] = [characters[b], characters[a]];
                }
                ExampleGlobalBuff.#randomText = characters.join('');
            }
            buffName.value = ExampleGlobalBuff.#randomText;
        }
    }

    // Parado, o jogador não cancela a Defesa de Exemplo tocando no ícone.
    RightClick(type, buffIndex) {
        const player = Main.player[Main.myPlayer];
        if (type === ModContent.BuffType('ExampleDefenseBuff') && player.velocity.X === 0 && player.velocity.Y === 0) {
            NewText(ModLocalization.Translate('CustomText.CannotCancel'), 255, 255, 255, false);
            return false;
        }

        return true;
    }
}
