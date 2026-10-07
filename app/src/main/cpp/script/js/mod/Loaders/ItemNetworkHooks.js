// O pacote de dados precede a mensagem nativa no mesmo fluxo TCP. No servidor
// os dados entram ANTES do repasse; no cliente, antes de ProcessData retornar.
class ItemNetworkHooks {
    static #pending = new Map();
    static #receiving = null;
    static #dropping = null;
    static #dropSource = null;

    static Key(item) {
        const m = ItemLoader.Of(item);
        return m ? (m.Mod ? m.Mod.uuid : 'sem-mod') + '/' + m.constructor.name : '';
    }

    static Payload(item) {
        const m = ItemLoader.Of(item);
        if (!m) return null;

        if (Hooks.Overrides(m.constructor, ModItem, 'NetSend')) {
            const writer = new NetWriter();
            m.NetSend(writer);
            ItemDataLoader.Encode(writer.values);

            return { n: writer.values };
        }

        if (Hooks.Overrides(m.constructor, ModItem, 'SaveData') || item.__blItemData) return { d: ItemDataLoader.Save(item) };

        return null;
    }

    static Apply(item, envelope) {
        if (!item || item.type <= 0 || ItemNetworkHooks.Key(item) !== envelope.t) return false;

        const m = ItemLoader.Of(item);
        if (envelope.n !== undefined) {
            if (!m || !Hooks.Overrides(m.constructor, ModItem, 'NetReceive')) return false;
            m.NetReceive(new NetReader(envelope.n));
        } else ItemDataLoader.Load(item, envelope.d);

        return true;
    }

    // O item que `run` soltar no mundo (Item.NewItem do mesmo tipo) leva os dados de `item`.
    static DropFrom(item, run) {
        const previous = ItemNetworkHooks.#dropSource;
        ItemNetworkHooks.#dropSource = item;
        try {
            return run();
        }
        finally {
            ItemNetworkHooks.#dropSource = previous;
        }
    }

    static Slot(playerIndex, slot) {
        const player = Terraria.Main.player[playerIndex];
        if (!player || slot < 0 || slot >= Terraria.ID.PlayerItemSlotID.Count) return null;

        const reference = Terraria.ID.PlayerItemSlotID.SlotReference.new();
        reference['void .ctor(Player player, int slot)'](player, slot);

        return reference.Item;
    }

    static BeforeSend(packet, remote, ignore, index, slot) {
        const Main = Terraria.Main;
        if (Main.netMode === 0 || ![5, 21, 32, 90].includes(packet)) return;

        let item;
        if (packet === 5) item = ItemNetworkHooks.Slot(index, Math.trunc(slot));
        else if (packet === 32) {
            const chest = Main.chest[index];
            if (!chest || slot < 0 || slot >= chest.item.length) return;

            item = chest.item[Math.trunc(slot)].ExpandItem();
        } else {
            const world = Main.item[index];
            item = world && world.inner;
        }

        if (!item || item.type < FIRST_ITEM || item.stack <= 0) return;

        const incoming = ItemNetworkHooks.#receiving;
        if (incoming && incoming.envelope.p === packet && incoming.target === index &&
            ((packet !== 5 && packet !== 32) || incoming.envelope.s === Math.trunc(slot)) && !incoming.applied) {
            incoming.applied = ItemNetworkHooks.Apply(item, incoming.envelope);

            if (incoming.applied && packet === 32) {
                bl.items.__chestData(Main.chest[index], Math.trunc(slot), ItemDataLoader.Save(item));
                Terraria.InventoryStorage['void SyncChestItemUpdateToStorage(int chestIndex, int slot)'](index, Math.trunc(slot));
            }
        }

        const payload = ItemNetworkHooks.Payload(item);
        if (!payload) return;

        ModNet.Send({ k: 'item-data', p: packet, i: index, s: Math.trunc(slot), t: ItemNetworkHooks.Key(item), ...payload }, remote, ignore);
    }

    static Queue(envelope, from) {
        if (
            !envelope ||
            ![5, 21, 32, 90].includes(envelope.p) ||
            !Number.isInteger(envelope.i) ||
            envelope.i < 0 ||
            envelope.i >= 8000 ||
            !Number.isInteger(envelope.s) ||
            envelope.s < 0 ||
            envelope.s > 65535 ||
            typeof envelope.t !== 'string' ||
            !envelope.t ||
            (typeof envelope.d === 'string') === Array.isArray(envelope.n)
        )
            return;
        if ((envelope.p === 5 && envelope.i > 255) || ((envelope.p === 21 || envelope.p === 90) && envelope.i > 400)) return;
        if (Terraria.Main.netMode !== 1 && envelope.p === 5 && envelope.i !== from) return;

        const queue = ItemNetworkHooks.#pending.get(from) || [];
        const now = Date.now();

        while (queue.length && now - queue[0].time > 10000) queue.shift();
        queue.push({ envelope, time: now });
        if (queue.length > 64) queue.shift();

        ItemNetworkHooks.#pending.set(from, queue);
    }

    static Process(original, self, data, length, typeOut) {
        if (!data || length < 3 || length > data.length) return original(self, data, length, typeOut);

        const packet = data[0],
            Main = Terraria.Main;
        if (![5, 21, 32, 90].includes(packet)) return original(self, data, length, typeOut);
        if ((packet === 5 || packet === 32) && length < 4) return original(self, data, length, typeOut);

        const from = Main.netMode === 1 ? 256 : self.whoAmI;
        const index = packet === 5 ? data[1] : data[1] | (data[2] << 8);
        const slot = packet === 5 && length >= 4 ? data[2] | (data[3] << 8) : packet === 32 && length >= 4 ? data[3] : 0;
        const queue = ItemNetworkHooks.#pending.get(from) || [];
        const at = queue.findIndex(
            entry =>
                Date.now() - entry.time <= 10000 &&
                entry.envelope.p === packet &&
                (entry.envelope.i === index || (packet === 21 && index === 400 && Main.netMode !== 1)) &&
                ((packet !== 5 && packet !== 32) || entry.envelope.s === slot)
        );
        if (at < 0) return original(self, data, length, typeOut);

        const context = { envelope: queue.splice(at, 1)[0].envelope, target: index, applied: false };
        const previous = ItemNetworkHooks.#receiving;
        ItemNetworkHooks.#receiving = context;

        try {
            const result = original(self, data, length, typeOut);

            if (!context.applied)
                Safe.Run('ModItem: receber dados', () => {
                    if (packet === 32) {
                        const chest = Main.chest[index];
                        if (!chest || slot < 0 || slot >= chest.item.length) return;

                        const item = chest.item[slot].ExpandItem();
                        if (!ItemNetworkHooks.Apply(item, context.envelope)) return;

                        bl.items.__chestData(chest, slot, ItemDataLoader.Save(item));
                        Terraria.InventoryStorage['void SyncChestItemUpdateToStorage(int chestIndex, int slot)'](index, slot);
                    } else {
                        const item = packet === 5 ? ItemNetworkHooks.Slot(index, slot) : Main.item[context.target]?.inner;
                        ItemNetworkHooks.Apply(item, context.envelope);
                    }
                });

            return result;
        }
        finally {
            ItemNetworkHooks.#receiving = previous;
        }
    }

    static Install() {
        Hooks.Once('item.NetworkData', () => {
            ModNet.InstallEntity(5, 21, 32, 90);

            Terraria.MessageBuffer['void ProcessData(byte[] messageData, int length, out int messageType)'].hook(ItemNetworkHooks.Process,
                ModNet.Received('net.recv.items', 5, 21, 32, 90));

            Terraria.MessageBuffer['void Reset(bool setupActive)'].hook((original, self, setup) => {
                ItemNetworkHooks.#pending.delete(Terraria.Main.netMode === 1 ? 256 : self.whoAmI);
                return original(self, setup);
            });

            Terraria.Item['bool IsNetStateDifferent(Item compareItem)'].hook(
                (original, self, compare) => {
                    if (original(self, compare)) return true;

                    return (
                        Safe.Run(
                            'ModItem: comparar dados de rede',
                            () => JSON.stringify(ItemNetworkHooks.Payload(self)) !== (compare.__blItemNetSnapshot ?? JSON.stringify(ItemNetworkHooks.Payload(compare)))
                        ) === true
                    );
                },
                { minType: FIRST_ITEM, on: -1 }
            );

            Terraria.Item['Item clientClone(Item cloneDestination)'].hook(
                (original, self, destination) => {
                    const copy = original(self, destination);

                    if (copy)
                        Safe.Run('ModItem: snapshot de rede', () => {
                            copy.__blItemNetSnapshot = JSON.stringify(ItemNetworkHooks.Payload(self));
                        });

                    return copy;
                },
                { minType: FIRST_ITEM, on: -1 }
            );

            Terraria.Player['void DropSelectedItem()'].hook((original, self) => {
                const previous = ItemNetworkHooks.#dropping;
                ItemNetworkHooks.#dropping = self;

                try {
                    return original(self);
                }
                finally {
                    ItemNetworkHooks.#dropping = previous;
                }
            });

            bl.classOf('', 'GUIPageIcons')['void DropUIItem(Player player, Item item, int additionalVelocity)'].hook((original, player, item, velocity) => {
                const previous = ItemNetworkHooks.#dropSource;
                ItemNetworkHooks.#dropSource = item;

                try {
                    return original(player, item, velocity);
                }
                finally {
                    ItemNetworkHooks.#dropSource = previous;
                }
            });

            Terraria.Item['void RequestNewItem(IEntitySource source, Vector2 center, int type, int stack, int prefix, NewItemOwnership ownership, Nullable<Vector2> velocity, Item.NewItemModifier modifier)'].hook(
                (original, source, center, type, stack, prefix, ownership, velocity, modifier) => {
                    const previous = ItemNetworkHooks.#dropSource;
                    const player = ItemNetworkHooks.#dropping;
                    const item = ItemNetworkHooks.#dropSource || (player && player.inventory[player.selectedItem]);
                    ItemNetworkHooks.#dropSource = item && item.type === type ? item : null;

                    try {
                        return original(source, center, type, stack, prefix, ownership, velocity, modifier);
                    }
                    finally {
                        ItemNetworkHooks.#dropSource = previous;
                    }
                }, { minType: FIRST_ITEM, arg: 2 });

            Terraria.Item['int NewItem(IEntitySource source, Vector2 center, int type, int stack, int prefix, NewItemOwnership ownership, Nullable<Vector2> velocity, Item.NewItemModifier modifier, bool noBroadcast)'].hook(
                (original, source, center, type, stack, prefix, ownership, velocity, modifier, noBroadcast) => {
                    const dropping = ItemNetworkHooks.#dropSource;
                    const incoming = ItemNetworkHooks.#receiving;
                    const defer = (dropping && dropping.type === type) || (incoming && incoming.envelope.p === 21 && !incoming.applied);
                    const index = original(source, center, type, stack, prefix, ownership, velocity, modifier, defer ? true : noBroadcast);
                    const world = Terraria.Main.item[index];

                    if (defer && world && world.inner && world.inner.type === type) {
                        Safe.Run('ModItem: transferir dados do drop', () => {
                            if (dropping) ItemDataLoader.Copy(dropping, world.inner);

                            if (incoming) {
                                incoming.target = index;
                                incoming.applied = ItemNetworkHooks.Apply(world.inner, incoming.envelope);
                            }
                        });

                        if (!noBroadcast && (Terraria.Main.netMode & 2)) world.SyncItem();
                    }

                    return index;
                }, { minType: FIRST_ITEM, arg: 2 });
        });
    }
}

Entities.Define(Terraria.Item, '__blItemNetSnapshot');
