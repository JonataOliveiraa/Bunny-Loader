// Os penteados de mod, como o HairLoader do tModLoader. O tipo sai no
// registro, depois dos 228 do jogo (Main.maxHairStyles, constante no
// celular); as texturas (TextureAssets.PlayerHair/PlayerHairAlt, as únicas
// tabelas do tamanho dos cabelos) com o jogo pronto. A criação de personagem
// e o Cabeleireiro mostram a lista do HairstyleUnlocksHelper, que o jogo monta
// um por um: os de mod entram depois (RebuildList), e a lista é refeita
// quando um deles muda de liberado para não (ListWarrantsRemake).
//
// Save: o arquivo do jogo guarda o número do cabelo, e sem o mod ele ficaria
// além da tabela (o IL2CPP daqui não confere o limite). Então vai o 0 no
// arquivo do jogo e o de mod, pelo nome, no <personagem>.plr.bl.json.
class HairLoader {
    static ByType = new Map();
    static VanillaCount = 228;
    static #pending = [];
    static #installed = false;
    static #waiting = [];   // [player, inst]: carregados antes das texturas
    static #saving = null;  // o cabelo de mod durante o save (o do jogador vira 0)
    static SAVE_KEY = 'bunny:hair';

    static Add(inst) {
        inst.Type = HairLoader.VanillaCount + HairLoader.ByType.size;
        HairLoader.ByType.set(inst.Type, inst);

        // Os arquivos agora, com o mod na pilha.
        const main = ModFiles.Texture(inst.Texture);
        const alt = ModFiles.Texture(inst.AltTexture);
        inst.__files = {
            main: bl.file.exists(main) ? bl.mod.path + '/' + main : null,
            alt: bl.file.exists(alt) ? bl.mod.path + '/' + alt : null,
        };
        HairLoader.#pending.push(inst);
        if (HairLoader.#pending.length === 1) Ready.Add(() => HairLoader.#Install(), 'setup');
        Hooks.Once('hair', () => HairLoader.#Hook());
    }

    static #KeyOf(inst) {
        return (inst.Mod ? inst.Mod.uuid : 'sem-mod') + '/' + inst.constructor.name;
    }

    static #Install() {
        const pending = HairLoader.#pending.splice(0);
        if (!pending.length) return;

        const T = Terraria.GameContent.TextureAssets;
        const total = HairLoader.VanillaCount + HairLoader.ByType.size;
        if (T.PlayerHair.length < total) T.PlayerHair = T.PlayerHair.cloneResized(total);
        if (T.PlayerHairAlt.length < total) T.PlayerHairAlt = T.PlayerHairAlt.cloneResized(total);

        for (const inst of pending) {
            const name = inst.constructor.name;
            Safe.Run(name + ' (textura)', () => {
                const main = inst.__files.main ? bl.loadTextureAsset(inst.__files.main) : null;
                if (!main) { bl.log(name + ': sem a textura ' + inst.Texture); return; }
                T.PlayerHair[inst.Type] = main;
                T.PlayerHairAlt[inst.Type] = inst.__files.alt ? bl.loadTextureAsset(inst.__files.alt) : main;
            });
            Safe.Run(name + '.SetStaticDefaults', () => inst.SetStaticDefaults());
        }
        HairLoader.#installed = true;
        bl.log('cabelos de mod: ' + pending.length + ' (tipos ' + pending[0].Type + '..' + (total - 1) + ')');

        for (const [player, inst] of HairLoader.#waiting.splice(0)) player.hair = inst.Type;
    }

    static #Hook() {
        const Helper = Terraria.GameContent.HairstyleUnlocksHelper;
        const unlocked = new Map();   // tipo -> liberado na última conta

        // Refaz a lista se algum de mod mudou (o Cabeleireiro de dia, por exemplo).
        Helper['bool ListWarrantsRemake()'].hook((original, self) => {
            let remake = original(self);
            const creation = self._isAtCharacterCreation, stylist = self._isAtStylist;
            for (const [type, inst] of HairLoader.ByType) {
                const now = !!Safe.Run(inst.constructor.name + '.IsUnlocked', () => inst.IsUnlocked(creation, stylist));
                if (unlocked.get(type) !== now) remake = true;
                unlocked.set(type, now);
            }
            return remake;
        });

        Helper['void RebuildList()'].hook((original, self) => {
            original(self);
            if (!HairLoader.#installed) return;
            const list = self.AvailableHairstyles;
            const creation = self._isAtCharacterCreation, stylist = self._isAtStylist;
            for (const [type, inst] of HairLoader.ByType) {
                const ok = unlocked.has(type) ? unlocked.get(type)
                    : !!Safe.Run(inst.constructor.name + '.IsUnlocked', () => inst.IsUnlocked(creation, stylist));
                if (ok && !list.Contains(type)) list.Add(type);
            }
        });

        // O arquivo do jogo leva o 0; o de mod vai no .plr.bl.json.
        Terraria.Player['void InternalSavePlayerFile(PlayerFileData playerFile)'].hook((original, fileData) => {
            const player = fileData && fileData.Player;
            const inst = player && HairLoader.ByType.get(player.hair);
            if (!inst) return original(fileData);

            HairLoader.#saving = inst;
            player.hair = 0;
            try {
                original(fileData);
            } finally {
                player.hair = inst.Type;
                HairLoader.#saving = null;
            }
        });

        PlayerLoader.AddSaveExtra(HairLoader.SAVE_KEY,
            (player) => {
                const inst = HairLoader.#saving || HairLoader.ByType.get(player.hair);
                return inst ? HairLoader.#KeyOf(inst) : undefined;
            },
            (player, key) => {
                for (const inst of HairLoader.ByType.values()) {
                    if (HairLoader.#KeyOf(inst) !== key) continue;
                    if (HairLoader.#installed) player.hair = inst.Type;
                    else HairLoader.#waiting.push([player, inst]);
                    return;
                }
            });

        HairLoader.#HookNet();
    }

    // Quem lê a mensagem 4 (a aparência do jogador) troca cabelo acima de 227
    // pelo 0: o de mod vai logo atrás dela pelo ModNet, na mesma ordem.
    static #HookNet() {
        ModNet.Install();
        Terraria.NetMessage['void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)'].hook(
            (original, msgType, remote, ignore, text, number, n2, n3, n4, n5, n6, n7) => {
                original(msgType, remote, ignore, text, number, n2, n3, n4, n5, n6, n7);
                if (msgType !== 4 || Terraria.Main.netMode === 0 || number < 0 || number > 255) return;
                const hair = Terraria.Main.player[number].hair;
                if (HairLoader.ByType.has(hair)) ModNet.Send({ k: 'hair', i: number, h: hair }, remote, ignore);
            }, ModNet.Sent('net.send.hair', 4));
    }

    // `from`: no servidor, o cliente (que só fala do próprio jogador); no cliente, 256.
    static Receive(envelope, from) {
        const Main = Terraria.Main;
        const i = Main.netMode === 2 ? from : envelope.i;
        if (i < 0 || i > 255 || !HairLoader.ByType.has(envelope.h)) return;
        Main.player[i].hair = envelope.h;
        if (Main.netMode === 2) ModNet.Send({ k: 'hair', i, h: envelope.h }, -1, from);
    }
}
