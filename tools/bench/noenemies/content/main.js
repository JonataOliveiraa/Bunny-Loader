// Some com os inimigos que causam dano, todo quadro. O mundo de teste tem
// monstros perto do spawn que matavam o personagem no meio dos testes (morto,
// o jogo pula o UpdateEquips, o uso de item...). Os NPCs que os testes criam
// vêm com dano 0 e ficam.
const Main = Terraria.Main;

Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu) return;
    for (let n = 0; n < 200; n++) {
        const npc = Main.npc[n];
        if (npc.active && npc.damage > 0 && !npc.friendly && !npc.townNPC) npc.active = false;
    }
});

export default class BenchNoEnemies extends Mod {}
