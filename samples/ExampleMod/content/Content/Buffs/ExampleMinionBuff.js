const { BuffID } = Terraria.ID;

export class ExampleMinionBuff extends ModBuff {
    SetStaticDefaults() {
        Terraria.Main.buffNoSave[this.Type] = true;
        Terraria.Main.buffNoTimeDisplay[this.Type] = true;
        const counter = Terraria.DataStructures.CachedProjectileCounterBuffTextHandler.new(ModProjectile.getTypeByName('ExampleMinion'));
        BuffID.Sets.BuffTextHandlers.Add(this.Type, counter);
    }

    UpdatePlayer(player, buffIndex) {
        if (player.ownedProjectileCounts[ModProjectile.getTypeByName('ExampleMinion')] > 0) {
            player.buffTime[buffIndex] = 18000;
        } else {
            player.DelBuff(buffIndex);
        }
    }
}
