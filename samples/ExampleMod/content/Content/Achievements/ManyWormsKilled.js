const { NPCID } = Terraria.ID;

// Cinco Vermes Gigantes (o ManyExampleWormsKilled do tModLoader, com o verme
// do jogo): uma condição de número que completa sozinha ao chegar a 5, e
// escondida ("???") até o primeiro. A condição de NPC morto do jogo só conta
// uma morte, por isso a conta é feita aqui.
export class ManyWormsKilled extends ModAchievement {
    condition = null;

    get Category() { return Terraria.Achievements.AchievementCategory.Collector; }
    get Hidden() { return !this.condition || this.condition.Value === 0; }

    SetStaticDefaults() {
        this.condition = this.AddIntCondition(5);
    }

    OnNPCKilled(player, npcId) {
        if (player.whoAmI !== Terraria.Main.myPlayer) return;
        if (npcId === NPCID.GiantWormHead) this.condition.Value++;
    }
}
