const CombatText = Terraria.CombatText['int NewText(Rectangle location, Color color, int amount, bool dramatic, bool dot)'];

// Um recurso novo (como a mana): o atual, o máximo e a regeneração. A arma
// ExampleCustomResourceWeapon gasta; o ExampleResourceAccessory aumenta o
// máximo e acelera a volta; a barra é a ExampleResourceBar.
export class ExampleResourcePlayer extends ModPlayer {
    static DefaultExampleResourceMax = 100;
    static HealExampleResourceColor = Color.new(187, 91, 201);

    exampleResourceCurrent = 0;       // o valor agora
    exampleResourceMax = 0;           // o máximo de base (volta a cada quadro no ResetEffects)
    exampleResourceMax2 = 0;          // o máximo deste quadro (os acessórios somam aqui)
    exampleResourceRegenRate = 1;     // quanto mais alto, mais rápido volta
    exampleResourceRegenTimer = 0;

    Initialize() {
        this.exampleResourceMax = ExampleResourcePlayer.DefaultExampleResourceMax;
    }

    ResetEffects(player) {
        this.ResetVariables();
    }

    UpdateDead(player) {
        this.ResetVariables();
    }

    ResetVariables() {
        this.exampleResourceRegenRate = 1;
        this.exampleResourceMax2 = this.exampleResourceMax;
    }

    // Depois dos acessórios (que mexem no máximo e na regeneração).
    PostUpdateMiscEffects(player) {
        this.UpdateResource();
    }

    PostUpdate(player) {
        this.CapResourceGodMode(player);
    }

    UpdateResource() {
        this.exampleResourceRegenTimer++;   // 60 por segundo
        if (this.exampleResourceRegenTimer > 60 / this.exampleResourceRegenRate) {
            this.exampleResourceCurrent += 1;
            this.exampleResourceRegenTimer = 0;
        }
        this.exampleResourceCurrent = Math.min(Math.max(this.exampleResourceCurrent, 0), this.exampleResourceMax2);
    }

    // No modo Jornada com a invencibilidade, o recurso fica cheio.
    CapResourceGodMode(player) {
        if (Terraria.Main.myPlayer === player.whoAmI && player.creativeGodMode) {
            this.exampleResourceCurrent = this.exampleResourceMax2;
        }
    }

    HealExampleResource(player, healAmount) {
        this.exampleResourceCurrent = Math.min(Math.max(this.exampleResourceCurrent + healAmount, 0), this.exampleResourceMax2);
        if (Terraria.Main.myPlayer === player.whoAmI) {
            CombatText(player.getRect(), ExampleResourcePlayer.HealExampleResourceColor, healAmount, false, false);
        }
    }
}
