// O tile no mundo: fio, crescimento (RandomUpdate), efeitos por perto
// (NearbyEffects), estação de criação (AdjTiles), colocar e martelar.
class TileWorldLoader {
    static Install() {
        Safe.Run('fios dos tiles', TileWorldLoader.#HookWire);
        Safe.Run('atualização aleatória dos tiles', TileWorldLoader.#HookRandomUpdate);
        Safe.Run('martelar tile de mod', TileWorldLoader.#HookSlope);
    }

    // Depois do do jogo (que só conhece os tipos dele). Caixa de música e
    // porta sem HitWire ligam e desligam, como as do jogo.
    static #HookWire() {
        Terraria.Wiring['void HitWireSingle(int i, int j)'].hook((original, i, j) => {
            original(i, j);

            const m = TileLoader.At(i, j);
            if (!m) return;
            if (TileLoader.Overrides(m, 'HitWire')) Safe.Run(m.constructor.name + '.HitWire', () => m.HitWire(i, j));
            else if (TileLoader.MusicBoxes.has(m.Type)) TileUseLoader.ToggleMusicBox(i, j);
            else TileWorldLoader.#WireDoor(i, j, m.Type);
        }, { minType: FIRST_TILE, tileAt: [0, 1], marks: 'tile.wire' });
    }

    // O Wiring.HitWireSingle do jogo com a porta 10/11: lado sorteado, o
    // outro se não couber; aberta fecha à força. O OpenDoor/CloseDoor já
    // pulam os outros fios da porta (SkipWire).
    static #WireDoor(i, j, type) {
        const W = Terraria.WorldGen;
        const Sets = Terraria.ID.TileID.Sets;
        const send = (action, dir) => Terraria.NetMessage['void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)'](
            19, -1, -1, null, action, i, j, dir, 0, 0, 0);

        if (Sets.OpenDoorID[type] >= 0) {
            const dir = Rand.Next(2) === 0 ? -1 : 1;
            if (W['bool OpenDoor(int i, int j, int direction)'](i, j, dir)) send(0, dir);
            else if (W['bool OpenDoor(int i, int j, int direction)'](i, j, -dir)) send(0, -dir);
        } else if (Sets.CloseDoorID[type] >= 0) {
            if (W['bool CloseDoor(int i, int j, bool forced)'](i, j, true)) send(1, 0);
        }
    }

    static #HookRandomUpdate() {
        for (const name of ['UpdateWorld_OvergroundTile', 'UpdateWorld_UndergroundTile']) {
            Terraria.WorldGen[`void ${name}(int i, int j, bool checkNPCSpawns, int wallDist)`].hook((original, i, j, npcs, wallDist) => {
                original(i, j, npcs, wallDist);

                const m = TileLoader.At(i, j);
                if (m) Safe.Run(m.constructor.name + '.RandomUpdate', () => m.RandomUpdate(i, j));
            }, { minType: FIRST_TILE, tileAt: [0, 1], marks: 'tile.random' });
        }
    }

    // Como o tModLoader: a área dos buffs em volta do centro (closer = false)
    // e a da tela (closer = true). A fogueira de mod liga o HasCampfire aqui.
    static HookNearby() {
        Terraria.SceneMetrics['void Scan(SceneMetricsScanSettings settings)'].hook((original, self, settings) => {
            original(self, settings);

            const Main = Terraria.Main;
            const center = settings.BiomeScanCenterPositionInWorld;
            const cx = Math.floor(center.X / 16), cy = Math.floor(center.Y / 16);
            const w = Math.floor(Main.buffScanAreaWidth / 2), h = Math.floor(Main.buffScanAreaHeight / 2);
            TileWorldLoader.#Nearby(cx - w, cy - h, cx + w, cy + h, false);

            const area = settings.VisualScanArea;
            if (area) TileWorldLoader.#Nearby(area.X, area.Y, area.X + area.Width, area.Y + area.Height, true);
        });
    }

    static #Nearby(x0, y0, x1, y1, closer) {
        const found = bl.tiles.find('tile.nearby', x0, y0, x1, y1);
        for (let k = 0; k < found.length; k += 2) {
            const i = found[k], j = found[k + 1];
            const m = TileLoader.At(i, j);
            if (m) Safe.Run(m.constructor.name + '.NearbyEffects', () => m.NearbyEffects(i, j, closer));
        }
    }

    // O jogo marca adjTile[tipo] de todo tile em volta (o de mod também); o
    // AdjTiles faz a bancada de mod contar como bancada do jogo.
    static HookAdjTiles() {
        Terraria.Player['void AdjTiles()'].hook((original, self) => {
            original(self);

            const x = Math.floor((self.position.X + self.width / 2) / 16);
            const y = Math.floor((self.position.Y + self.height) / 16);
            const found = bl.tiles.find('tile.adj', x - 4, y - 3, x + 4, y + 2);
            if (!found.length) return;

            const adj = self.adjTile;
            for (let k = 0; k < found.length; k += 2) {
                const m = TileLoader.At(found[k], found[k + 1]);
                if (!m) continue;
                for (const t of m.AdjTiles) adj[t] = true;
            }
        });
    }

    // Objeto (TileObjectData) pelo gancho de depois de colocar; bloco simples
    // pelo PlaceTile, só quando é o jogador colocando.
    static HookPlace() {
        const placed = (i, j, type) => {
            const m = TileLoader.ByType.get(type);
            if (!m) return;

            const player = Terraria.Main.player[Terraria.Main.myPlayer];
            Safe.Run(m.constructor.name + '.PlaceInWorld', () => m.PlaceInWorld(i, j, player.HeldItem));
        };

        Terraria.ObjectData.TileObjectData['bool CallPostPlacementPlayerHook(int tileX, int tileY, int type, int style, int dir, int alternate, TileObject data)'].hook(
            (original, x, y, type, style, dir, alternate, data) => {
                const r = original(x, y, type, style, dir, alternate, data);
                placed(x, y, type);
                return r;
            }, { minType: FIRST_TILE, arg: 2, marks: 'tile.place' });

        const placing = Terraria.Player['void PlaceThing_Tiles(bool doPlacementAction)'];
        placing.hook((original, self, doPlacementAction) => original(self, doPlacementAction));

        Terraria.WorldGen['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'].hook(
            (original, i, j, type, mute, forced, plr, style) => {
                const ok = original(i, j, type, mute, forced, plr, style);
                if (ok && !TileLoader.HasObjectData(type)) placed(i, j, type);
                return ok;
            }, { minType: FIRST_TILE, arg: 2, marks: 'tile.place', whileIn: placing });
    }

    // O martelo: Slope devolvendo false deixa o tile como está (a armadilha gira).
    static #HookSlope() {
        const W = Terraria.WorldGen;
        const hammer = (original, i, j, run) => {
            const m = TileLoader.At(i, j);
            if (m && Safe.Run(m.constructor.name + '.Slope', () => m.Slope(i, j)) === false) return false;
            return run();
        };
        const filter = { minType: FIRST_TILE, tileAt: [0, 1], marks: 'tile.slope' };

        W['bool SlopeTile(int i, int j, int slope, bool noEffects, bool quiet)'].hook(
            (original, i, j, slope, noEffects, quiet) => hammer(original, i, j, () => original(i, j, slope, noEffects, quiet)), filter);
        W['bool PoundTile(int i, int j)'].hook(
            (original, i, j) => hammer(original, i, j, () => original(i, j)), filter);
    }
}
