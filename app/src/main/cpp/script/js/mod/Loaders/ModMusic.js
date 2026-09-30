// Música de mod pelo Android (bl.music, MediaPlayer). Nos menus, a do tema
// (ModMenu). No mundo, os candidatos: o ModNPC com
// Music mais prioritário perto da tela e a cena do jogador local (ModBiome,
// ModSceneEffect), que só troca o do NPC com prioridade maior, como no
// tModLoader. O candidato ganha da escolha do jogo se a prioridade dele chegar
// ao degrau dela (VanillaMusic). Caixa de música ganha de tudo. A troca é a do
// jogo: a nova sobe 0,005 por quadro e a que tocava só desce quando a nova
// passa de 0,25.
class ModMusic {
    static FADE = 0.005;
    static AUDIBLE = 0.25;
    static Tracks = [];               // slot - Base() -> { id, file, fade, sent }
    static Slots = new Map();         // arquivo -> slot

    static #npcs = new Set();         // NPCs de mod vivos (o Music pode mudar na IA)
    static #wanted = -1;
    static #hooked = false;
    static #savedSilenceFade = null;  // o musicFade[0] do jogo enquanto a nossa manda
    static #base = 0;

    // O primeiro slot de mod: MusicID.Count. Lido na primeira vez: o topo deste
    // arquivo roda antes de o jogo estar pronto.
    static Base() {
        if (!ModMusic.#base) ModMusic.#base = Safe.Run('MusicID.Count', () => Terraria.ID.MusicID.Count) || 105;
        return ModMusic.#base;
    }

    static Track(slot) {
        return ModMusic.Tracks[slot - ModMusic.Base()];
    }

    // Sem música de mod nenhuma, o conjunto não guarda NPC nenhum.
    static TrackNpc(npc, m) {
        if ((m.Music | 0) >= 0) ModMusic.Install();
        else if (!ModMusic.#hooked) return;

        ModMusic.#npcs.add(npc);
    }

    // Todo quadro, até na tela de carregamento; nenhum espera o motor JS
    // ocupado: sem a escolha vale a anterior, sem o fade o volume fica.
    static Install() {
        Hooks.Once('music', () => {
            ModMusic.#hooked = true;
            const Main = Terraria.Main;
            const decide = (tow) => (original, self) => {
                original(self);
                ModMusic.#Decide(tow);
            };

            Main['void UpdateAudio_DecideOnNewMusic()'].hook(decide(false), { ifBusy: 'skip' });
            Main['void UpdateAudio_DecideOnTOWMusic()'].hook(decide(true), { ifBusy: 'skip' });
            Main['void UpdateAudio()'].hook((original, self) => {
                original(self);
                ModMusic.#Step();
            }, { ifBusy: 'original' });
        });
    }

    // O slot que a música de mod pede, ou -1 (fica a do jogo).
    static #Choose(tow) {
        const Main = Terraria.Main;
        if (Main.gameMenu) return MenuLoader.MenuMusic();

        const box = ModMusic.#MusicBox();
        if (box > 0) return box;
        // A caixa do jogo entra depois desta escolha (no UpdateAudio) e ganha.
        if (Main.SceneMetrics.ActiveMusicBox >= 0) return -1;

        let { music, priority } = ModMusic.#FromNpcs();
        const scene = SceneEffectLoader.List.length ? SceneEffectLoader.Of(Main.LocalPlayer).music : null;
        if (scene && scene.value > -1 && scene.priority > priority) {
            music = scene.value;
            priority = scene.priority;
        }
        if (music < 0 || priority < SceneEffectPriority.BiomeLow) return -1;

        const tier = Safe.Run('ModMusic: degrau do jogo', () => VanillaMusic.Tier(tow));
        return tier === undefined || priority >= tier ? music : -1;
    }

    // Dos NPCs de mod com Music perto da tela (5000 px de folga), o de maior
    // SceneEffectPriority.
    static #FromNpcs() {
        const Main = Terraria.Main;
        const none = { music: -1, priority: SceneEffectPriority.None };
        if (!ModMusic.#npcs.size) return none;

        const margin = 5000;
        const sp = Main.screenPosition;
        const x0 = sp.X - margin, x1 = sp.X + Main.screenWidth + margin;
        const y0 = sp.Y - margin, y1 = sp.Y + Main.screenHeight + margin;

        let best = -1, bestPriority = SceneEffectPriority.None;
        for (const npc of ModMusic.#npcs) {
            // Só o que está no mundo: a amostra do ContentSamples também passa pelo SetDefaults.
            const live = npc.active && Main.npc[npc.whoAmI] === npc;
            const m = live ? NPCLoader.Of(npc) : undefined;
            if (!m) {
                ModMusic.#npcs.delete(npc);
                continue;
            }

            const music = m.Music | 0;
            if (music < 0) continue;

            const c = npc.Center;
            if (c.X < x0 || c.X > x1 || c.Y < y0 || c.Y > y1) continue;

            const priority = m.SceneEffectPriority | 0;
            if (best < 0 || priority > bestPriority) {
                best = music;
                bestPriority = priority;
            }
        }
        return { music: best, priority: bestPriority };
    }

    // Caixa de música de mod ligada na tela, ou equipada (acessório ou
    // visual): ganha de tudo, como a do jogo.
    static #MusicBox() {
        if (!TileLoader.MusicBoxes.size) return 0;

        const Main = Terraria.Main;
        const sp = Main.screenPosition;
        const found = bl.tiles.find('tile.musicbox', Math.floor(sp.X / 16), Math.floor(sp.Y / 16),
            Math.ceil((sp.X + Main.screenWidth) / 16), Math.ceil((sp.Y + Main.screenHeight) / 16));

        for (let k = 0; k < found.length; k += 2) {
            const tile = TileLoader.Tile(found[k], found[k + 1]);
            if (tile.frameX < 36) continue;

            const boxes = TileLoader.MusicBoxes.get(tile.type);
            const box = boxes && boxes.get(tile.frameY - tile.frameY % 36);
            if (box) return box.slot;
        }

        const armor = Main.player[Main.myPlayer].armor;
        for (let k = 3; k < armor.length; k++) {
            if (k >= 10 && k < 13) continue;   // a armadura visual
            const slot = TileLoader.MusicBoxItems.get(armor[k].type);
            if (slot) return slot;
        }
        return 0;
    }

    // Com o musicFade[0] em 1, o curMusic 0 tira as faixas do jogo com fade em
    // vez de cortar num quadro.
    static #SilenceGame(on) {
        const fade = Terraria.Main.musicFade;
        if (on) {
            if (ModMusic.#savedSilenceFade === null) ModMusic.#savedSilenceFade = fade[0];
            fade[0] = 1;
            Terraria.Main.newMusic = 0;
        } else if (ModMusic.#savedSilenceFade !== null) {
            fade[0] = ModMusic.#savedSilenceFade;
            ModMusic.#savedSilenceFade = null;
        }
    }

    // A nossa só cala o jogo quando já se ouve; até lá segura o que tocava.
    static #Decide(tow) {
        const Main = Terraria.Main;
        const wanted = ModMusic.#wanted = ModMusic.#Choose(tow);
        const track = wanted >= ModMusic.Base() ? ModMusic.Track(wanted) : null;
        if (track && track.fade > ModMusic.AUDIBLE) return ModMusic.#SilenceGame(true);

        ModMusic.#SilenceGame(false);
        if (track) Main.newMusic = Main.curMusic;
        else if (wanted >= 0) Main.newMusic = wanted;   // MusicID do jogo (0 = silêncio) pedido por NPC de mod ou pela cena
    }

    static #Step() {
        const Main = Terraria.Main;
        const target = Main.gameMenu ? MenuLoader.MenuMusic() : ModMusic.#wanted;
        const main = target >= ModMusic.Base() ? ModMusic.Track(target) : null;
        const current = Main.curMusic;
        const mainAudible = main
            ? main.fade > ModMusic.AUDIBLE
            : current <= 0 || Main.musicFade[current] > ModMusic.AUDIBLE;
        const volume = Main.musicVolume;

        for (const track of ModMusic.Tracks) {
            if (track === main) track.fade = Math.min(1, track.fade + ModMusic.FADE);
            else if (mainAudible) track.fade = Math.max(0, track.fade - ModMusic.FADE);

            const v = track.fade * volume;
            if (v !== track.sent) {
                bl.music.setVolume(track.id, v);
                track.sent = v;
            }
        }
    }
}
