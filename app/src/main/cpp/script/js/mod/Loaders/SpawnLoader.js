// O spawn natural, como o do tModLoader (NPCLoader.ChooseSpawn e os
// EditSpawn*), no NPC.Spawner do celular (o mesmo do PC 1.4.5). Para cada
// jogador, o TrySpawnAnNPC do jogo pede a taxa (GetSpawnRate), procura o ponto
// na área (FindSpawnTile -> GetSpawnArea), marca o ponto escolhido
// (SetSpawnFlagsForChosenTile) e faz nascer (SpawnAnNPC). Só roda no jogo
// sozinho ou no servidor: é onde o jogo faz o spawn.
//   - SetSpawnFlags: GlobalNPC.EditSpawnFlags(spawnInfo), antes de tudo;
//   - GetSpawnRate: GlobalNPC.EditSpawnRate(player, spawnRate, maxSpawns);
//   - GetSpawnArea: GlobalNPC.EditSpawnRange(player, spawnRangeX, spawnRangeY,
//     safeRangeX, safeRangeY), e a área refeita com os alcances novos;
//   - SetSpawnFlagsForChosenTile: GlobalNPC.EditSpawnInfo(spawnInfo);
//   - SpawnAnNPC: o sorteio. 0 (o do jogo, peso 1) e os ModNPC com SpawnChance,
//     GlobalNPC.EditSpawnPool(pool, spawnInfo); 0 roda o do jogo, outro tipo
//     nasce pelo SpawnNPC dele, e nada nasce com o total 0. O SpawnCondition
//     (as condições do jogo em objetos) vale para o spawnInfo do sorteio.
class SpawnLoader {
    // Os GlobalNPC que mudam `name` (na ordem de carga).
    static #Globals(name) {
        return globalNPCs.list.filter((g) => Hooks.Overrides(g.constructor, GlobalNPC, name));
    }

    static InstallPool() {
        Hooks.Once('spawn.Pool', () => {
            Terraria.NPC.Spawner['void SpawnAnNPC(int spawnTileX, int spawnTileY, int tileType, int wallType, bool xRange, int target)'].hook(
                (original, self, x, y, tileType, wallType, xRange, target) => {
                    const info = new NPCSpawnInfo(self, x, y, target, xRange);
                    SpawnCondition.Begin(info);
                    const pool = new SpawnPool();
                    pool[0] = 1;
                    for (const m of NPCLoader.Spawnable) {
                        const weight = Number(Safe.Run(m.constructor.name + '.SpawnChance', () => m.SpawnChance(info))) || 0;
                        if (weight > 0) pool[m.Type] = weight;
                    }
                    for (const g of SpawnLoader.#Globals('EditSpawnPool')) {
                        Safe.Run(g.constructor.name + '.EditSpawnPool', () => g.EditSpawnPool(pool, info));
                    }

                    const type = pool.Choose();
                    if (type === null) return;
                    if (type === 0) return original(self, x, y, tileType, wallType, xRange, target);
                    SpawnLoader.SpawnNPC(type, x, y);
                });
        });
    }

    // Como o NPCLoader.SpawnNPC do tModLoader: o de mod pelo SpawnNPC dele, o do
    // jogo em cima do bloco; depois o GlobalNPC.SpawnNPC de cada um.
    static SpawnNPC(type, tileX, tileY) {
        const N = Terraria.NPC;
        const m = NPCLoader.ByType.get(type);
        const index = m
            ? Safe.Run(m.constructor.name + '.SpawnNPC', () => m.SpawnNPC(tileX, tileY))
            : N.NewNPC(N.GetSpawnSourceForNaturalSpawn(), tileX * 16 + 8, tileY * 16, type, 0, 0, 0, 0, 0, 255);
        if (!(index >= 0 && index < 200)) return index;

        const npc = Terraria.Main.npc[index];
        if (npc.active) globalNPCs.Each(npc, 'SpawnNPC', (g) => g.SpawnNPC(index, tileX, tileY));
        return index;
    }

    static InstallFlags() {
        Hooks.Once('spawn.Flags', () => {
            Terraria.NPC.Spawner['void SetSpawnFlags(Player player)'].hook((original, self, player) => {
                original(self, player);
                const info = new NPCSpawnInfo(self, -1, -1, player.whoAmI);
                for (const g of SpawnLoader.#Globals('EditSpawnFlags')) {
                    Safe.Run(g.constructor.name + '.EditSpawnFlags', () => g.EditSpawnFlags(info));
                }
            });
        });
    }

    static InstallRate() {
        Hooks.Once('spawn.Rate', () => {
            Terraria.NPC.Spawner['void GetSpawnRate(Player player, out int spawnRate, out int maxSpawns)'].hook(
                (original, self, player, spawnRate, maxSpawns) => {
                    original(self, player, spawnRate, maxSpawns);
                    for (const g of SpawnLoader.#Globals('EditSpawnRate')) {
                        Safe.Run(g.constructor.name + '.EditSpawnRate', () => g.EditSpawnRate(player, spawnRate, maxSpawns));
                    }
                    // O jogo sorteia com Next(spawnRate): 0 ou menos lançaria.
                    spawnRate.value = Math.max(1, spawnRate.value | 0);
                    maxSpawns.value = maxSpawns.value | 0;
                });
        });
    }

    // Os alcances do GetSpawnArea do jogo (em blocos): a metade da largura e da
    // altura da área, como ele calcula antes de montar os retângulos.
    static #Ranges(player) {
        const N = Terraria.NPC;
        const w = Math.trunc(N.sWidth / 16), h = Math.trunc(N.sHeight / 16);
        let x = Math.trunc(w * 0.7), y = Math.trunc(h * 0.7);
        const held = player.inventory[player.selectedItem].type;
        if (held === 1254 || held === 1299 || player.scope) {
            // Rifle de sniper (1254), binóculos (1299) e mira (scope).
            const div = held === 1254 && player.scope ? 1.25 : held === 1254 || held === 1299 ? 1.5 : 2;
            x += Math.trunc(w * 0.5 / div);
            y += Math.trunc(h * 0.5 / div);
        }
        return { x, y };
    }

    static InstallRange() {
        Hooks.Once('spawn.Range', () => {
            const N = Terraria.NPC;
            N.Spawner['void GetSpawnArea(Player player, out Rectangle spawnArea, out Rectangle safeArea)'].hook(
                (original, player, spawnArea, safeArea) => {
                    original(player, spawnArea, safeArea);
                    const globals = SpawnLoader.#Globals('EditSpawnRange');
                    if (!globals.length) return;

                    const ranges = SpawnLoader.#Ranges(player);
                    const rx = new Ref(ranges.x), ry = new Ref(ranges.y);
                    const sx = new Ref(N.safeRangeX), sy = new Ref(N.safeRangeY);
                    for (const g of globals) {
                        Safe.Run(g.constructor.name + '.EditSpawnRange', () => g.EditSpawnRange(player, rx, ry, sx, sy));
                    }
                    N.safeRangeX = sx.value | 0;
                    N.safeRangeY = sy.value | 0;

                    // Os retângulos do jogo, com os alcances novos (Utils.CenteredRectangle,
                    // WorldUtils.ClampToWorld e o caso do mundo de duas masmorras).
                    const Main = Terraria.Main;
                    const cx = Math.trunc(player.position.X / 16), cy = Math.trunc(player.position.Y / 16);
                    const centered = (w, h) => [cx - Math.trunc(w / 2), cy - Math.trunc(h / 2), w, h];
                    const [ax, ay, aw, ah] = centered((rx.value | 0) * 2, (ry.value | 0) * 2);
                    const left = Math.max(0, Math.min(ax, Main.maxTilesX)), top = Math.max(0, Math.min(ay, Main.maxTilesY));
                    const right = Math.max(0, Math.min(ax + aw, Main.maxTilesX)), bottom = Math.max(0, Math.min(ay + ah, Main.maxTilesY));
                    spawnArea.value = Rectangle.new(left, top, right - left, bottom - top);
                    const halved = Main.dualDungeonsSeed && !player.ZoneOverworldHeight && !player.ZoneSkyHeight;
                    safeArea.value = Rectangle.new(...centered(N.safeRangeX * (halved ? 1 : 2), N.safeRangeY * (halved ? 1 : 2)));
                });
        });
    }

    // O SetSpawnFlagsForChosenTile não recebe o jogador: vem do TrySpawnAnNPC em curso.
    static InstallInfo() {
        Hooks.Once('spawn.Info', () => {
            const S = Terraria.NPC.Spawner;
            let target = -1;
            S['bool TrySpawnAnNPC(Player player)'].hook((original, self, player) => {
                const outer = target;
                target = player.whoAmI;
                try {
                    return original(self, player);
                } finally {
                    target = outer;
                }
            });
            S['void SetSpawnFlagsForChosenTile(int spawnTileX, int spawnTileY, int groundTileY, int tileType, int wallType)'].hook(
                (original, self, x, y, groundY, tileType, wallType) => {
                    original(self, x, y, groundY, tileType, wallType);
                    const info = new NPCSpawnInfo(self, x, y, target);
                    for (const g of SpawnLoader.#Globals('EditSpawnInfo')) {
                        Safe.Run(g.constructor.name + '.EditSpawnInfo', () => g.EditSpawnInfo(info));
                    }
                });
        });
    }
}
