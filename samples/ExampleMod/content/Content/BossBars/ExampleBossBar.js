// A barra de vida do Olho de ??? (ModBossBar), como a do MinionBoss do
// tModLoader: o ícone da cabeça certa, a vida e um escudo. Aqui o escudo é o
// que falta para a 2ª fase (a metade de cima da vida). O NPC a pede no
// SetDefaults: `this.BossBar = ModContent.GetInstance(ExampleBossBar)`.
//
// Sem ModBossBar, todo chefe com cabeça de chefe já ganha a barra comum do
// jogo; ela só é preciso para mudar o que a barra mostra.
export class ExampleBossBar extends ModBossBar {
    bossHeadIndex = -1;

    // O ícone: a cabeça que o chefe usa agora (a da 2ª fase vem do BossHeadSlot).
    GetIconTexture(iconFrame) {
        return this.bossHeadIndex !== -1 ? Terraria.GameContent.TextureAssets.NpcHeadBoss[this.bossHeadIndex] : null;
    }

    // Aqui o jogo pergunta se desenha a barra: false sempre que não valer
    // (sem isso, ela ficaria na tela fora de hora).
    ModifyInfo(info, life, lifeMax, shield, shieldMax) {
        const npc = Terraria.Main.npc[info.npcIndexToAimAt];
        if (!npc.active) return false;

        // Guardado para o GetIconTexture.
        this.bossHeadIndex = npc.GetBossHeadTextureIndex();

        life.value = npc.life;
        lifeMax.value = npc.lifeMax;
        const half = npc.lifeMax / 2;
        shield.value = Math.max(0, npc.life - half);
        shieldMax.value = half;
        return true;
    }
}
