// Os biomas de cada jogador, como no tModLoader: depois do Player.UpdateBiomes
// do jogo (a contagem de blocos já feita), cada ModBiome diz se o jogador está
// nele; a troca chama OnEnter/OnLeave e o OnInBiome roda a cada quadro dentro
// (também no quadro da entrada). Depois vem a escolha da cena.
//
// O jogo só chama o UpdateBiomes para o jogador local, também com ele morto
// (como no PC e no tModLoader). Teleporte e renascimento varrem e chamam o
// UpdateBiomes na hora (UpdateBiomesIfMovedEnoughForBlackFade), sem esperar a
// varredura seguinte.
//
// Na rede, como no tModLoader: cada aparelho calcula os biomas do próprio
// jogador e manda quando mudam. O servidor grava (o spawn de NPC roda lá e lê
// InModBiome do alvo), repassa aos outros e, a quem entra, manda os de todos
// (depois do SyncOnePlayer). Vai a chave uid/Classe de cada bioma ligado, não
// a posição. Quem recebe só grava a flag, sem OnEnter/OnLeave (o tModLoader
// também não chama). A vaga de quem sai ganha um Player novo do jogo
// (RemoteClient.Reset, msg 14), então flag velha não passa a quem entra.
class BiomeLoader {
    static List = [];
    static #byKey = null;   // 'uid/Classe' -> bioma

    static Add(inst) {
        inst.Type = BiomeLoader.List.length;
        BiomeLoader.List.push(inst);
        BiomeLoader.Install();
    }

    // Também para um mod só com ModSceneEffect: o hook lê as flags.
    static Install() {
        Hooks.Once('biome.update', () => {
            bl.defineField(Terraria.Player, 'ModBiomeFlags');
            bl.defineMethod(Terraria.Player, 'InModBiome', function (which) {
                return BiomeLoader.InBiome(this, which);
            });
            Terraria.Player['void UpdateBiomes()'].hook((original, self) => {
                original(self);
                BiomeLoader.UpdateBiomes(self);
                SceneEffectLoader.UpdateSceneEffect(self);
            });

            ModNet.Install();
            const Main = Terraria.Main;
            Terraria.NetMessage['void SyncOnePlayer(int plr, int toWho, int fromWho)'].hook((original, plr, toWho, fromWho) => {
                original(plr, toWho, fromWho);
                if (!(Main.netMode & 2)) return;

                const player = Main.player[plr];
                if (player.active) Safe.Run('rede: biomas de ' + plr, () => BiomeLoader.#Send(player, toWho, fromWho));
            });

            // Fora do mundo o UpdateBiomes não roda: sem isto o OnLeave de quem
            // estava dentro nunca vinha, e a flag ligada passava para o mundo
            // seguinte. O SaveAndQuit é a saída pelo menu de pausa e o fim da
            // sessão multijogador; o clearWorld, o começo de todo mundo
            // carregado (e a saída que não passou pelo SaveAndQuit).
            const WorldGen = Terraria.WorldGen;
            WorldGen['void SaveAndQuit()'].hook((original) => {
                BiomeLoader.LeaveWorld();
                original();
            });
            WorldGen['void clearWorld()'].hook((original) => {
                original();
                BiomeLoader.LeaveWorld();
            });
        });
    }

    // OnLeave de cada bioma ligado, flags zeradas e a cena vazia.
    static LeaveWorld() {
        const players = Terraria.Main.player;
        for (let i = 0; i < players.length; i++) {
            const player = players[i];
            const flags = player && player.ModBiomeFlags;
            if (!flags) continue;

            // OnLeave só do jogador daqui: o de outro não recebeu OnEnter.
            const local = i === Terraria.Main.myPlayer;
            for (const biome of BiomeLoader.List) {
                if (flags[biome.Type] !== 1) continue;
                flags[biome.Type] = 0;
                if (local) Safe.Run(biome.constructor.name + '.OnLeave', () => biome.OnLeave(player));
            }
            if (local) player.CurrentSceneEffect = SceneEffectLoader.Empty();
        }
    }

    static #Send(player, toClient, ignoreClient) {
        const keys = [];
        const flags = player.ModBiomeFlags;
        if (flags) {
            for (const biome of BiomeLoader.List) {
                if (flags[biome.Type] === 1) keys.push(SceneEffectLoader.KeyOf(biome));
            }
        }
        ModNet.Send({ k: 'biomes', v: 1, p: player.whoAmI, b: keys }, toClient, ignoreClient);
    }

    // `from`: no servidor, o índice do cliente; no cliente, 256. No servidor
    // cada cliente só fala por si; chave desconhecida é ignorada.
    static Receive(envelope, from) {
        const Main = Terraria.Main;
        const index = envelope.p;
        if (envelope.v !== 1 || !Array.isArray(envelope.b) || !Number.isInteger(index) || index < 0 || index >= 255) return;
        if (Main.netMode === 1 ? index === Main.myPlayer : index !== from) return;

        if (!BiomeLoader.#byKey) {
            BiomeLoader.#byKey = new Map(BiomeLoader.List.map((b) => [SceneEffectLoader.KeyOf(b), b]));
        }
        const player = Main.player[index];
        const flags = BiomeLoader.#FlagsOf(player);
        flags.fill(0);
        for (const key of envelope.b) {
            const biome = BiomeLoader.#byKey.get(key);
            if (biome) flags[biome.Type] = 1;
        }

        if (Main.netMode !== 1) ModNet.Send(envelope, -1, from);
    }

    // Um bioma pela classe, pela instância (ModContent.GetInstance) ou pelo Type.
    static Resolve(which) {
        if (which instanceof ModBiome) return which;
        if (typeof which === 'function') return Templates.Get(which);
        if (typeof which === 'number') return BiomeLoader.List[which];
        return undefined;
    }

    static InBiome(player, which) {
        const biome = BiomeLoader.Resolve(which);
        if (!biome || BiomeLoader.List[biome.Type] !== biome) {
            throw new TypeError('InModBiome: espera a classe de um ModBiome registrado, a instância ou o Type');
        }
        const flags = player.ModBiomeFlags;
        return !!(flags && flags[biome.Type]);
    }

    static #FlagsOf(player) {
        let flags = player.ModBiomeFlags;
        if (!flags || flags.length !== BiomeLoader.List.length) {
            const grown = new Uint8Array(BiomeLoader.List.length);
            if (flags) grown.set(flags.subarray(0, Math.min(flags.length, grown.length)));
            player.ModBiomeFlags = flags = grown;
        }
        return flags;
    }

    // Um IsBiomeActive que lança vale false nesta avaliação (o OnLeave roda se
    // estava dentro).
    static UpdateBiomes(player) {
        const flags = BiomeLoader.#FlagsOf(player);
        let changed = false;
        for (const biome of BiomeLoader.List) {
            const name = biome.constructor.name;
            const before = flags[biome.Type] === 1;
            const now = Safe.Run(name + '.IsBiomeActive', () => biome.IsBiomeActive(player)) === true;
            flags[biome.Type] = now ? 1 : 0;
            if (before !== now) changed = true;

            if (!before && now) Safe.Run(name + '.OnEnter', () => biome.OnEnter(player));
            else if (before && !now) Safe.Run(name + '.OnLeave', () => biome.OnLeave(player));
            if (now) Safe.Run(name + '.OnInBiome', () => biome.OnInBiome(player));
        }

        if (changed && Terraria.Main.netMode !== 0) Safe.Run('rede: biomas', () => BiomeLoader.#Send(player, -1, -1));
    }
}
