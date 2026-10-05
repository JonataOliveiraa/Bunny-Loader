class PlayerNetworkHooks {
    static #copying = new Set();
    static #baseline = null;
    static #source = null;
    static #connected = new Map();

    static Install(cls) {
        const want = PlayerLoader.Wants;
        want(cls, ['CopyClientState', 'SendClientChanges'], 'player.ClientState', () => {
            Terraria.Player['object clientClone(Player clonePlayer)'].hook((original, player, clone) => {
                const key = bl.addressOf(player);
                if (PlayerNetworkHooks.#copying.has(key)) return original(player, clone);
                PlayerNetworkHooks.#copying.add(key);
                try {
                    const result = original(player, clone);
                    PlayerNetworkHooks.#Copy(player, clone);
                    return result;
                } finally { PlayerNetworkHooks.#copying.delete(key); }
            });
            Terraria.Player['Player clientClone()'].hook((original, player) => {
                const key = bl.addressOf(player);
                if (PlayerNetworkHooks.#copying.has(key)) return original(player);
                PlayerNetworkHooks.#copying.add(key);
                try {
                    const clone = original(player);
                    PlayerNetworkHooks.#Copy(player, clone);
                    return clone;
                } finally { PlayerNetworkHooks.#copying.delete(key); }
            });
            Terraria.Main['void UpdateClient()'].hook((original) => {
                const Main = Terraria.Main, player = Main.player[Main.myPlayer];
                if (!player || Main.gameMenu || Main.netMode !== 1) {
                    PlayerNetworkHooks.#baseline = null;
                    PlayerNetworkHooks.#source = null;
                    return original();
                }
                const key = bl.addressOf(player);
                if (PlayerNetworkHooks.#source === key && PlayerNetworkHooks.#baseline) {
                    const previous = PlayerLoader.Of(PlayerNetworkHooks.#baseline);
                    PlayerLoader.Each(player, 'SendClientChanges', (inst) => inst.SendClientChanges(player, previous.get(inst.constructor)));
                }
                original();
                if (PlayerNetworkHooks.#source !== key || !PlayerNetworkHooks.#baseline) {
                    PlayerNetworkHooks.#baseline = player['Player clientClone()']();
                } else player['object clientClone(Player clonePlayer)'](PlayerNetworkHooks.#baseline);
                PlayerNetworkHooks.#source = key;
            });
        });
        want(cls, ['SyncPlayer', 'PlayerConnect', 'PlayerDisconnect'], 'player.NetworkLifecycle', () => {
            Terraria.NetMessage['void SyncOnePlayer(int plr, int toWho, int fromWho)'].hook((original, index, toWho, fromWho) => {
                original(index, toWho, fromWho);
                const player = Terraria.Main.player[index];
                if (player) {
                    const previous = PlayerNetworkHooks.#connected.get(index);
                    if (previous && bl.addressOf(previous) !== bl.addressOf(player)) PlayerNetworkHooks.#Disconnect(index);
                    PlayerLoader.Call(player, 'SyncPlayer', toWho, fromWho, !PlayerNetworkHooks.#connected.get(index));
                    if (player.active) PlayerNetworkHooks.#connected.set(index, player);
                }
            });
            Terraria.Player.Hooks['void PlayerConnect(int playerIndex)'].hook((original, index) => {
                original(index);
                const player = Terraria.Main.player[index];
                if (player) {
                    const previous = PlayerNetworkHooks.#connected.get(index);
                    if (previous && bl.addressOf(previous) !== bl.addressOf(player)) PlayerNetworkHooks.#Disconnect(index);
                    PlayerNetworkHooks.#connected.set(index, player);
                    PlayerLoader.Call(player, 'PlayerConnect');
                }
            });
            Terraria.Player.Hooks['void PlayerDisconnect(int playerIndex)'].hook((original, index) => {
                PlayerNetworkHooks.#Disconnect(index);
                return original(index);
            });
            Terraria.NetMessage['void SyncDisconnectedPlayer(int plr)'].hook((original, index) => {
                PlayerNetworkHooks.#Disconnect(index);
                return original(index);
            });
            Terraria.Player['void Update(int i)'].hook((original, self, index) => {
                const result = original(self, index), Main = Terraria.Main;
                if (Main.gameMenu || Main.netMode === 0 || index !== Main.myPlayer) return result;
                for (const [index, player] of PlayerNetworkHooks.#connected) {
                    if (!player) continue;
                    const current = Main.player[index];
                    if (!current || !current.active || bl.addressOf(current) !== bl.addressOf(player)) PlayerNetworkHooks.#Disconnect(index);
                }
                return result;
            });
        });
    }

    static #Disconnect(index) {
        const previous = PlayerNetworkHooks.#connected.get(index);
        if (previous === null) return;
        PlayerNetworkHooks.#connected.set(index, null);
        const player = previous || Terraria.Main.player[index];
        if (player) PlayerLoader.Call(player, 'PlayerDisconnect');
    }

    static #Copy(player, clone) {
        if (!clone || bl.addressOf(player) === bl.addressOf(clone)) return;
        if (clone.ModPlayers === player.ModPlayers) clone.ModPlayers = undefined;
        const copies = PlayerLoader.Of(clone);
        PlayerLoader.Each(player, 'CopyClientState', (inst) => inst.CopyClientState(player, copies.get(inst.constructor)));
    }
}
