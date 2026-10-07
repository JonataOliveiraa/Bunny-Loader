class ServerSyncLoader {
    static #SEND = 'void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)';
    static #PROCESS = 'void ProcessData(byte[] messageData, int length, out int messageType)';
    static #REQUEST_WORLD = 6;
    static #CLIENT_UUID = 68;
    static #PREFIX = 'bunnyloader-sync:';

    static Install() {
        Hooks.Once('server.sync', () => Safe.Run('ServerSyncLoader.Install', () => ServerSyncLoader.#Hook()));
    }

    static #Hook() {
        ModNet.Install();
        Terraria.NetMessage[ServerSyncLoader.#SEND].hook(
            (original, msgType, remote, ignore, text, number, n2, n3, n4, n5, n6, n7) => {
                if (msgType === ServerSyncLoader.#REQUEST_WORLD && Terraria.Main.netMode === 1) {
                    Safe.Run('ServerSync: apresentar', () => ServerSyncLoader.#Hello(original));
                }
                return original(msgType, remote, ignore, text, number, n2, n3, n4, n5, n6, n7);
            }, ModNet.Sent('net.send.sync', ServerSyncLoader.#REQUEST_WORLD));
        // Só quem hospeda lê as apresentações: o gancho em toda mensagem
        // recebida fica de fora de quem só entra em servidores.
        bl.classOf('', 'GUIMultiplayerHost')['void HostServer()'].hook((original, self) => {
            Hooks.Once('server.sync.listen', () => Safe.Run('ServerSync: ouvir', () => ServerSyncLoader.#Listen()));
            return original(self);
        });
    }

    static #Listen() {
        Terraria.MessageBuffer[ServerSyncLoader.#PROCESS].hook((original, self, data, length, messageType) => {
            if (data && length > 2 && data[0] === ServerSyncLoader.#CLIENT_UUID && Terraria.Main.netMode !== 1) {
                const hello = ServerSyncLoader.#Read(data, length);
                if (hello) {
                    messageType.value = ServerSyncLoader.#CLIENT_UUID;
                    Safe.Run('ServerSync: conferir', () => ServerSyncLoader.#Judge(hello, self.whoAmI));
                    return undefined;
                }
            }
            return original(self, data, length, messageType);
        }, ModNet.Received('net.recv.sync', ServerSyncLoader.#CLIENT_UUID));
    }

    static #Read(data, length) {
        let size = 0, shift = 0, i = 1;
        for (; i < length && shift < 35; i++, shift += 7) {
            const b = data[i];
            size |= (b & 0x7F) << shift;
            if (!(b & 0x80)) { i++; break; }
        }
        const prefix = ServerSyncLoader.#PREFIX;
        if (size < prefix.length || i + size > length) return null;
        for (let k = 0; k < prefix.length; k++) {
            if (data[i + k] !== prefix.charCodeAt(k)) return null;
        }
        let text = '';
        for (let k = i + prefix.length; k < i + size; k++) text += String.fromCharCode(data[k]);
        try {
            return JSON.parse(text);
        } catch (e) {
            bl.log('servidor: apresentação ilegível (' + e + ')');
            return null;
        }
    }

    static #Text(key, n) {
        const pt = /^pt/.test(ModLocalization.ActiveCultureName || '');
        switch (key) {
            case 'textures': return pt
                ? 'Este servidor não permite pacotes de textura. Desligue os seus (' + n + ') para entrar.'
                : 'This server does not allow texture packs. Turn yours off (' + n + ') to join.';
            case 'sync': return pt
                ? 'Este servidor usa outros mods. Sincronize para entrar.'
                : 'This server uses different mods. Sync to join.';
        }
        return key;
    }

    static #Mods() {
        return ModLoader.Mods.map((m) => ({ uid: m.uuid, id: m.id || '', name: m.name || '', version: m.version || '' }));
    }

    static #Hello(send) {
        const Main = Terraria.Main;
        const json = JSON.stringify({ mods: ServerSyncLoader.#Mods(), textures: bl.__texturePacks().length })
            .replace(/[\u007f-\uffff]/g, (c) => '\\u' + c.charCodeAt(0).toString(16).padStart(4, '0'));
        const uuid = Main.clientUUID;
        Main.clientUUID = ServerSyncLoader.#PREFIX + json;
        try {
            send(ServerSyncLoader.#CLIENT_UUID, -1, -1, null, 0, 0, 0, 0, 0, 0, 0);
        } finally {
            Main.clientUUID = uuid;
        }
    }

    static Receive(envelope, from) {
        if (envelope.k === 'sync-plan' && Terraria.Main.netMode === 1) ServerSyncLoader.#Offer(envelope);
    }

    static #Same(a, b) {
        return Array.isArray(b) && a.length === b.length
            && a.every((m, i) => b[i] && m.uid === b[i].uid && m.version === b[i].version);
    }

    static #Judge(hello, from) {
        if (from < 0 || from >= 255 || from === Terraria.Main.myPlayer) return;
        if (!HostSettingsMenu.AllowTextures && hello.textures > 0) {
            bl.log('servidor: jogador ' + from + ' recusado (' + hello.textures + ' pacote(s) de textura)');
            ServerSyncLoader.#Kick(from, ServerSyncLoader.#Text('textures', hello.textures));
            return;
        }
        const mods = ServerSyncLoader.#Mods();
        if (!HostSettingsMenu.SyncMods || ServerSyncLoader.#Same(mods, hello.mods)) return;
        bl.log('servidor: jogador ' + from + ' com outros mods; mandando os ' + mods.length + ' deste servidor');
        ModNet.Send({ k: 'sync-plan', mods }, from);
        ServerSyncLoader.#Kick(from, ServerSyncLoader.#Text('sync'));
    }

    static #Kick(player, reason) {
        const text = Terraria.Localization.NetworkText['NetworkText FromLiteral(string text)'](reason);
        Terraria.NetMessage['void BootPlayer(int plr, NetworkText msg)'](player, text);
    }

    static #Offer(envelope) {
        const net = Terraria.Netplay;
        const data = Terraria.Main.ActivePlayerFileData;
        const path = data ? String(data.Path || '') : '';
        const offer = {
            mods: envelope.mods || [],
            address: String(net.ServerIPText || ''),
            port: net.ListenPort,
            password: String(net.ServerPassword || ''),
            player: path.split(/[\\/]/).pop(),
            textures: bl.__texturePacks(),
        };
        bl.log('servidor: mods diferentes; oferecendo a sincronização com ' + offer.address + ':' + offer.port);
        if (bl.__offerServerSync(JSON.stringify(offer))) ServerSyncMenu.Open();
    }
}
