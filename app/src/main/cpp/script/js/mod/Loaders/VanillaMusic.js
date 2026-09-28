// Em que degrau está a música que o jogo escolheu, na escala do
// SceneEffectPriority: a de mod ganha se a prioridade dela chegar lá. É onde o
// tModLoader encaixa a música de mod entre os ramos do UpdateAudio_DecideOnNewMusic
// (e do DecideOnTOWMusic, o Otherworld):
//   créditos .............................. 9 (nada ganha)
//   desafio da tocha, Senhor da Lua ....... 8 (BossHigh)
//   grupos de chefe 17, 9, 10 e 6 ......... 7 (BossMedium)
//   os outros chefes ...................... 6 (BossLow)
//   eventos (luas, piratas, goblins, OOA,
//   pedra arco-íris) ...................... 5 (Event)
//   eclipse, chuva de slime, brilho,
//   cidade, tempestade, submundo, espaço .. 4 (Environment)
//   templo, masmorra, cogumelo, corrupção . 3 (BiomeHigh)
//   meteoro, cemitério, deserto, selva, neve 2 (BiomeMedium)
//   o resto (superfície, subterrâneo) ..... 1 (BiomeLow)
// Pelas condições, não pelo número da música: os números se repetem entre
// degraus (e o Otherworld repete quase todos). Tabela e ordem do C# do PC
// (1.4.5.7); a ordem das constantes no disasm do celular bate, e lá as luas
// de abóbora e de gelo já estão no degrau dos eventos.
class VanillaMusic {
    // tipo -> [grupo na escolha normal, grupo no Otherworld]
    static #GROUPS = (() => {
        const map = new Map();
        const add = (types, normal, tow) => { for (const t of types) map.set(t, [normal, tow]); };
        add([13, 14, 15], 23, 1);
        add([26, 27, 28, 29, 111, 471, 472], 11, 11);
        add([35, 36], 24, 0);
        add([50], 19, 0);
        add([113, 114], 2, 3);
        add([125, 126], 21, 2);
        add([127], 22, 2);
        add([128, 129, 130, 131], 22, 0);
        add([134], 18, 2);
        add([135, 136], 18, 0);
        add([143, 144, 145], 3, 11);
        add([266], 3, 1);
        add([212, 213, 214, 215, 216, 252, 491, 662], 8, 8);
        add([222], 5, 1);
        add([245], 4, 2);
        add([262, 263, 264], 6, 6);
        add([370], 15, 15);
        add([381, 382, 383, 384, 385, 386, 387, 388, 389, 390, 391, 392, 395, 520], 9, 9);
        add([398], 7, 7);
        add([422, 493, 507, 517], 10, 10);
        add([439], 20, 2);
        add([636], 14, 14);
        add([657], 13, 13);
        add([668], 16, 16);
        return map;
    })();

    // grupo -> degrau; os grupos 18 a 24 não existem no Otherworld.
    static #TierOfGroup(group) {
        if (group === 7) return 8;
        if (group === 17 || group === 9 || group === 10 || group === 6) return 7;
        if (group === 8 || group === 11 || group === 12) return 5;
        return group > 0 ? 6 : 0;
    }

    // O grupo do PRIMEIRO NPC (na ordem do índice) perto da tela: o jogo para
    // no primeiro, não no mais importante.
    static #NpcGroup(tow) {
        const Main = Terraria.Main;
        const sx = Math.trunc(Main.screenPosition.X), sy = Math.trunc(Main.screenPosition.Y);
        const sw = Main.screenWidth, sh = Main.screenHeight;
        const ooa = Terraria.ID.NPCID.Sets.BelongsToInvasionOldOnesArmy;
        const special = Main.remixWorld && Main.getGoodWorld;
        const npcs = Main.npc;

        for (let i = 0; i < npcs.length; i++) {
            const npc = npcs[i];
            if (!npc.active) continue;

            const type = npc.type;
            let range = 5000;
            let group = 0;
            if (type === 379 || type === 438) {
                if (type === 379 ? npc.ai[3] >= 0 : npc.ai[1] === 1) {
                    range = 1600;
                    group = tow ? 2 : 20;
                }
            } else {
                const g = VanillaMusic.#GROUPS.get(type);
                if (g) group = g[tow ? 1 : 0];
            }
            if (type < ooa.length && ooa[type]) group = 12;
            if (group === 0 && npc.boss) group = 1;
            if (special && (type === 127 || type === 134 || type === 125 || type === 126)) group = 17;
            if (group === 0) continue;

            const cx = Math.trunc(npc.position.X + Math.trunc(npc.width / 2)) - range;
            const cy = Math.trunc(npc.position.Y + Math.trunc(npc.height / 2)) - range;
            if (sx < cx + range * 2 && cx < sx + sw && sy < cy + range * 2 && cy < sy + sh) return group;
        }
        return 0;
    }

    static Tier(tow) {
        const Main = Terraria.Main;
        if (Terraria.GameContent.Events.CreditsRollEvent.IsEventOngoing) return 9;

        const sm = Main.SceneMetrics;
        if (sm.InTorchGodMinigame) return 8;

        if (!Main.showSplash) {
            const group = VanillaMusic.#NpcGroup(tow);
            const tier = VanillaMusic.#TierOfGroup(group);
            // No Otherworld os grupos 18 a 24 não têm ramo: segue para baixo.
            if (tier > 0 && !(tow && group >= 18)) return tier;
        }

        const surfaceY = Main.worldSurface * 16 + Math.trunc(Main.screenHeight / 2);
        const high = Main.screenPosition.Y / 16 < Main.worldSurface + 10 || Main.remixWorld;
        if (high && (Main.pumpkinMoon || Main.snowMoon)) return 5;
        if (!tow && Main.ShouldPlayRainbowBoulderMusic) return 5;

        const center = sm.Center;
        const cy = center.Y;
        const underworld = Main.UnderworldLayer * 16;
        const rock = Main.rockLayer * 16;
        const town = sm.TownNPCCount >= 3 && !sm.ZoneShadowCandle && !sm.ZoneGraveyard;
        const scale = (Main.maxTilesX / 4200) ** 2;
        const space = ((Main.screenPosition.Y + Math.trunc(Main.screenHeight / 2)) / 16 - (65 + 10 * scale)) / (Main.worldSurface / 5);

        const environment =
            (Main.eclipse && (Main.remixWorld ? cy > rock : cy < surfaceY)) ||
            (!tow && Main.slimeRain && !sm.ZoneGraveyard && (!Main.bloodMoon || Main.dayTime) && cy < surfaceY) ||
            (Main.remixWorld && Main.bloodMoon && !sm.ZoneCrimson && !sm.ZoneCorrupt && cy > rock && cy <= underworld) ||
            (Main.remixWorld && Main.bloodMoon && cy > underworld &&
                center.X / 16 > Main.maxTilesX * 0.37 + 50 && center.X / 16 < Main.maxTilesX * 0.63) ||
            sm.ZoneShimmer ||
            (!tow && town && Main.dayTime && ((Main.cloudAlpha === 0 && !Main._shouldUseWindyDayMusic) || cy >= surfaceY)) ||
            (!tow && town && !Main.dayTime && ((!Main.bloodMoon && Main.cloudAlpha === 0) || cy >= surfaceY)) ||
            sm.ZoneSandstorm || cy > underworld || space < 1;
        if (environment) return 4;

        const tx = Math.trunc(center.X / 16), ty = Math.trunc(cy / 16);
        const inWorld = tx >= 0 && ty >= 0 && tx < Main.maxTilesX && ty < Main.maxTilesY;
        const temple = inWorld && Main.tile['Tile get_Item(int x, int y)'](tx, ty).wall === 87;
        const mushroom = (Main.bgStyle === 9 && cy < surfaceY) || Main.undergroundBackground === 2;
        if (temple || sm.ZoneDungeon || mushroom || sm.ZoneCorrupt || sm.ZoneCrimson) return 3;

        if (sm.ZoneMeteor || sm.ZoneGraveyard || sm.ZoneJungle || sm.ZoneSnow ||
            (!tow && (sm.ZoneUndergroundDesert || sm.ZoneDesert))) return 2;
        return 1;
    }
}
