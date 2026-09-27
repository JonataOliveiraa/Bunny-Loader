// Golpes que dois tipos de Global ouvem: item/projétil e o NPC acertado.
class HitLoader {
    static ItemHitsNPC() {
        Hooks.Once('global.ItemHitsNPC', () => {
            Terraria.Player['void ApplyNPCOnHitEffects(Item sItem, Rectangle itemRectangle, int damage, float knockBack, int npcIndex, int dmgRandomized, int dmgDone)'].hook(
                (original, self, item, rect, damage, knockBack, npcIndex, dmgRandomized, dmgDone) => {
                    original(self, item, rect, damage, knockBack, npcIndex, dmgRandomized, dmgDone);

                    const npc = Terraria.Main.npc[npcIndex];
                    const crit = dmgDone >= dmgRandomized * 2;
                    globalItems.Each(item, 'OnHitNPC', (g) => g.OnHitNPC(item, self, npc, dmgDone, knockBack, crit));
                    globalNPCs.Each(npc, 'OnHitByItem', (g) => g.OnHitByItem(npc, self, item, dmgDone, knockBack, crit));
                });
        });
    }

    static ProjectileHitsNPC() {
        Hooks.Once('global.ProjectileHitsNPC', () => {
            Terraria.Projectile['void StatusNPC(int i)'].hook((original, p, i) => {
                original(p, i);

                const npc = Terraria.Main.npc[i];
                globalProjectiles.Each(p, 'OnHitNPC', (g) => g.OnHitNPC(p, npc));
                globalNPCs.Each(npc, 'OnHitByProjectile', (g) => g.OnHitByProjectile(npc, p));
            });
        });
    }
}
