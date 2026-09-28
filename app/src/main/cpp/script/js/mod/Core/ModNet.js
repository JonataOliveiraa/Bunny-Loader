// Dados de mod na rede: NetSend/NetReceive do ModSystem e dos Globais,
// SendExtraAI/ReceiveExtraAI do ModNPC e do ModProjectile, e os ModPacket.
//
// Tudo vai pelos NetModules do jogo (a mensagem 82), com um id de módulo que o
// jogo não registrou; o hook no NetManager.Read o pega antes. O conteúdo é
// JSON. Os dados de uma entidade seguem logo depois da mensagem do jogo que
// eles acompanham (7 mundo, 23 NPC, 27 projétil).
class ModNet {
    static ID = 0xB17E;
    static MAX_BYTES = 65000;

    static Send(envelope, toClient, ignoreClient) {
        const mode = Terraria.Main.netMode;
        if (mode === 0) return;

        ModNet.Install();
        const json = JSON.stringify(envelope);
        const size = ModNet.#Utf8Length(json) + 5;   // o texto e o prefixo de tamanho dele
        if (size > ModNet.MAX_BYTES) {
            throw new RangeError('rede: pacote de mod com ' + size + ' bytes; o limite e ' + ModNet.MAX_BYTES);
        }

        const packet = Terraria.Net.NetPacket.new();
        packet['void .ctor(ushort id, int size)'](ModNet.ID, size);
        packet.Writer['void Write(string value)'](json);
        packet['void ShrinkToFit()']();

        const net = Terraria.Net.NetManager.Instance;
        if (mode === 1) net['void SendToServer(NetPacket packet)'](packet);
        else if (toClient >= 0) net['void SendToClient(NetPacket packet, int clientId)'](packet, toClient);
        else net['void Broadcast(NetPacket packet, int ignoreClient)'](packet, ignoreClient);
    }

    // Os dois lados rodam os mesmos mods: instala nos dois assim que algum usa a rede.
    static Install() {
        Hooks.Once('net.Read', () => {
            Terraria.Net.NetManager['void Read(BinaryReader reader, int userId, int readLength)'].hook(
                (original, self, reader, userId, length) => {
                    const stream = reader.BaseStream;
                    const start = stream.Position;
                    if (length < 2 || reader.ReadUInt16() !== ModNet.ID) {
                        stream.Position = start;
                        return original(self, reader, userId, length);
                    }

                    const text = reader.ReadString();
                    let envelope;
                    try {
                        envelope = JSON.parse(text);
                    } catch (e) {
                        bl.log('rede: pacote de mod ilegivel (' + e + ')');
                        return undefined;
                    }

                    ModNet.#Receive(envelope, Terraria.Main.netMode === 1 ? 256 : userId);
                    return undefined;
                });
        });
    }

    // 7 (mundo, só do servidor), 23 (NPC, só do servidor), 27 (projétil, de quem manda).
    static InstallEntity() {
        ModNet.Install();

        Hooks.Once('net.SendData', () => {
            Terraria.NetMessage['void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)'].hook(
                (original, msgType, remote, ignore, text, number, n2, n3, n4, n5, n6, n7) => {
                    original(msgType, remote, ignore, text, number, n2, n3, n4, n5, n6, n7);
                    if (msgType !== 7 && msgType !== 23 && msgType !== 27) return;
                    if (Terraria.Main.netMode === 0) return;

                    if (msgType === 7) ModNet.#AfterWorld(remote, ignore);
                    else if (msgType === 23) ModNet.#AfterNPC(number, remote, ignore);
                    else ModNet.#AfterProjectile(number, remote, ignore);
                });
        });
    }

    static #AfterWorld(remote, ignore) {
        if (Terraria.Main.netMode === 1) return;

        Safe.Run('rede: dados do mundo', () => ModNet.#SendWorld(remote, ignore));
    }

    static #AfterNPC(index, remote, ignore) {
        const Main = Terraria.Main;
        if (Main.netMode === 1 || index < 0 || index >= 200) return;

        const npc = Main.npc[index];
        if (!npc.active) return;

        const data = ModNet.#EntityData(npc, NPCLoader.Of(npc), ModNPC, globalNPCs);
        if (data) ModNet.Send({ k: 'npc', i: index, t: npc.type, x: data.x, g: data.g }, remote, ignore);
    }

    static #AfterProjectile(index, remote, ignore) {
        const Main = Terraria.Main;
        if (index < 0 || index >= 1000) return;

        const p = Main.projectile[index];
        if (!p.active) return;

        // No servidor, o projétil de um cliente segue pelo repasse do pacote dele.
        if (Main.netMode !== 1 && p.owner !== Main.myPlayer && p.owner !== 255) return;

        const data = ModNet.#EntityData(p, ProjectileLoader.Of(p), ModProjectile, globalProjectiles);
        if (data) ModNet.Send({ k: 'proj', o: p.owner, id: ModNet.#KeyOf(p), t: p.type, x: data.x, g: data.g }, remote, ignore);
    }

    // `from`: no servidor, o índice do cliente; no cliente, 256 (o servidor).
    static #Receive(envelope, from) {
        const Main = Terraria.Main;

        switch (envelope.k) {
            case 'packet': {
                const mod = ModRegistry.ByUuid(envelope.m);
                if (!mod) return;

                Safe.Run(ModRegistry.ClassName(mod) + '.HandlePacket', () => mod.HandlePacket(new NetReader(envelope.d), from));
                return;
            }
            case 'world':
                for (const s of SystemLoader.List) {
                    if (!Hooks.Overrides(s.constructor, ModSystem, 'NetReceive')) continue;

                    const values = envelope.d[SystemLoader.KeyOf(s)];
                    Safe.Run(s.constructor.name + '.NetReceive', () => s.NetReceive(new NetReader(values)));
                }
                return;
            case 'biomes':
                BiomeLoader.Receive(envelope, from);
                return;
            case 'npc': {
                const npc = Main.npc[envelope.i];
                if (!npc || !npc.active || npc.type !== envelope.t) return;

                ModNet.#Apply(npc, envelope, NPCLoader.Of(npc), ModNPC, globalNPCs);
                return;
            }
            case 'proj': {
                const p = ModNet.#FindProjectile(envelope.o, envelope.id, envelope.t);
                if (p) ModNet.#Apply(p, envelope, ProjectileLoader.Of(p), ModProjectile, globalProjectiles);

                // O servidor repassa o projétil de um cliente aos outros: os dados vão atrás.
                if (Main.netMode !== 1 && from < 256) ModNet.Send(envelope, -1, from);
                return;
            }
        }
    }

    // Na rede o projétil é a `key` dele (quem criou, o número que deu e a
    // geração): o índice em Main.projectile muda de um aparelho a outro.
    static #KeyOf(p) {
        const key = p.key;
        return key.Spawner + ':' + key.Index + ':' + key.Generation;
    }

    static #FindProjectile(owner, key, type) {
        const all = Terraria.Main.projectile;
        for (let i = 0; i < 1000; i++) {
            const p = all[i];
            if (p.active && p.owner === owner && p.type === type && ModNet.#KeyOf(p) === key) return p;
        }
        return null;
    }

    static #Apply(entity, envelope, own, Base, registry) {
        if (own && envelope.x !== undefined && Hooks.Overrides(own.constructor, Base, 'ReceiveExtraAI')) {
            Safe.Run(own.constructor.name + '.ReceiveExtraAI', () => own.ReceiveExtraAI(new NetReader(envelope.x)));
        }

        const data = envelope.g || {};
        for (const g of registry.Of(entity)) {
            if (!Hooks.Overrides(g.constructor, registry.Base, 'NetReceive')) continue;

            const values = data[g.constructor.name];
            if (values !== undefined) Safe.Run(g.constructor.name + '.NetReceive', () => g.NetReceive(entity, new NetReader(values)));
        }
    }

    // O que a entidade manda junto com a mensagem do jogo; null se ninguém escreve.
    static #EntityData(entity, own, Base, registry) {
        let x, g;
        let any = false;

        if (own && Hooks.Overrides(own.constructor, Base, 'SendExtraAI')) {
            const writer = new NetWriter();
            Safe.Run(own.constructor.name + '.SendExtraAI', () => own.SendExtraAI(writer));
            x = writer.values;
            any = true;
        }

        for (const global of registry.Of(entity)) {
            if (!Hooks.Overrides(global.constructor, registry.Base, 'NetSend')) continue;

            const writer = new NetWriter();
            Safe.Run(global.constructor.name + '.NetSend', () => global.NetSend(entity, writer));
            (g = g || {})[global.constructor.name] = writer.values;
            any = true;
        }

        return any ? { x, g } : null;
    }

    static #SendWorld(remote, ignore) {
        const data = {};
        let any = false;

        for (const s of SystemLoader.List) {
            if (!Hooks.Overrides(s.constructor, ModSystem, 'NetSend')) continue;

            const writer = new NetWriter();
            Safe.Run(s.constructor.name + '.NetSend', () => s.NetSend(writer));
            data[SystemLoader.KeyOf(s)] = writer.values;
            any = true;
        }

        if (any) ModNet.Send({ k: 'world', d: data }, remote, ignore);
    }

    static #Utf8Length(s) {
        let bytes = 0;
        for (let i = 0; i < s.length; i++) {
            const c = s.charCodeAt(i);
            if (c < 0x80) bytes += 1;
            else if (c < 0x800) bytes += 2;
            else if (c >= 0xD800 && c <= 0xDBFF) {
                bytes += 4;
                i++;
            } else {
                bytes += 3;
            }
        }
        return bytes;
    }
}
