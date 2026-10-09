let mod;
const Main = Terraria.Main;

export default class ServerSyncProbe extends Mod {
    Load() {
        mod = this;
        bl.log('serversync-probe LOAD version=' + this.version + ' uid=' + this.uuid);
    }
}

export class ProbeWorld extends ModSystem {
    OnWorldLoad() {
        bl.log('serversync-probe WORLD version=' + mod.version + ' mode=' + Main.netMode);
    }
}

export class ProbePlayer extends ModPlayer {
    PlayerConnect(player) {
        bl.log('serversync-probe CONNECT player=' + player.whoAmI + ' local=' + Main.myPlayer + ' mode=' + Main.netMode);
    }
}
