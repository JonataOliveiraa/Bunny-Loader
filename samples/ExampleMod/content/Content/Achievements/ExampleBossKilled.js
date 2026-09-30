// A conquista de derrotar o Chefe de Exemplo (o MinionBossKilled do
// tModLoader): uma condição de NPC morto, que o jogo mesmo acompanha.
export class ExampleBossKilled extends ModAchievement {
    SetStaticDefaults() {
        this.AddNPCKilledCondition(ModContent.NPCType('ExampleBoss'));
    }
}
