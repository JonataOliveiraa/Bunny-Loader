// O mobile envia só tipo/prefixo em PlayerDeathReason. Este complemento
// precede o Hurt no mesmo TCP e conserva NetSend/NetReceive da arma.
class PvpItemNetwork {
    static #pending = new Map();
    static #receiving = null;

    static Receive(envelope, from) {
        const Main = Terraria.Main;
        if (!envelope || !Number.isInteger(envelope.a) || envelope.a < 0 || envelope.a >= 255
            || !Number.isInteger(envelope.i) || envelope.i < 0 || envelope.i >= 255
            || !Number.isInteger(envelope.type) || envelope.type <= 0
            || envelope.type >= FIRST_ITEM && !ItemLoader.ByType.has(envelope.type)
            || !Number.isInteger(envelope.prefix) || envelope.prefix < 0 || envelope.prefix > 255
            || (Main.netMode !== 1 && from !== envelope.a)) return;
        const queue = PvpItemNetwork.#pending.get(from) || [];
        const now = Date.now();
        while (queue.length && now - queue[0].at > 5000) queue.shift();
        if (queue.length >= 16) queue.shift();
        queue.push({ ...envelope, at: now });
        PvpItemNetwork.#pending.set(from, queue);
    }

    static Source(source, target) {
        const envelope = PvpItemNetwork.#receiving;
        if (!envelope || envelope.i !== target || envelope.a !== source._sourcePlayerIndex
            || envelope.type !== source._sourceItemType || envelope.prefix !== source._sourceItemPrefix) return null;
        const item = Terraria.Item.new(); item['void .ctor()']();
        item['void SetDefaults(int Type, ItemVariant variant)'](envelope.type, null);
        if (envelope.prefix) item['bool Prefix(int prefixWeWant)'](envelope.prefix);
        if (envelope.t) Safe.Run('PvP: dados da arma', () => ItemNetworkHooks.Apply(item, envelope));
        return { player: Terraria.Main.player[envelope.a], item };
    }

    static Install() {
        Hooks.Once('combat.PvpNetwork', () => {
            ModNet.Install();
            Terraria.MessageBuffer['void ProcessData(byte[] messageData, int length, out int messageType)'].hook(
                (original, self, data, length, type) => {
                    const from = Terraria.Main.netMode === 1 ? 256 : self.whoAmI;
                    const queue = PvpItemNetwork.#pending.get(from);
                    const outer = PvpItemNetwork.#receiving;
                    let envelope = null;
                    if (queue) {
                        const now = Date.now();
                        while (queue.length && now - queue[0].at > 5000) queue.shift();
                        if (queue.length && queue[0].i === data[1]) envelope = queue.shift();
                        if (!queue.length) PvpItemNetwork.#pending.delete(from);
                    }
                    PvpItemNetwork.#receiving = envelope;
                    try { return original(self, data, length, type); }
                    finally { PvpItemNetwork.#receiving = outer; }
                }, ModNet.Received('combat.PvpReceived', 117));
            Terraria.NetMessage['void SendPlayerHurt(int playerTargetIndex, PlayerDeathReason reason, int damage, int direction, bool critical, bool pvp, int hitContext, int remoteClient, int ignoreClient)'].hook(
                (original, target, source, damage, direction, crit, pvp, context, remote, ignore) => {
                    if (pvp) Safe.Run('PvP: enviar arma', () => {
                        const received = PvpItemNetwork.#receiving;
                        if (received && received.i === target && received.a === source._sourcePlayerIndex)
                            ModNet.Send(received, remote, ignore);
                        else {
                            const attack = CombatLoader.PvpSource(source, target);
                            if (attack) ModNet.Send({ k: 'pvp-item', a: attack.player.whoAmI, i: target,
                                type: attack.item.type, prefix: attack.item.prefix,
                                t: ItemNetworkHooks.Key(attack.item), ...ItemNetworkHooks.Payload(attack.item) }, remote, ignore);
                        }
                    });
                    return original(target, source, damage, direction, crit, pvp, context, remote, ignore);
                });
            Terraria.GameContent.BannerSystem['void Clear()'].hook(original => {
                PvpItemNetwork.#pending.clear(); PvpItemNetwork.#receiving = null;
                return original();
            });
        });
    }
}
