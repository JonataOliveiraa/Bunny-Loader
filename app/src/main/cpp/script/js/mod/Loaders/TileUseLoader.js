// O jogador e o tile: tocar (RightClick), passar por cima (MouseOver), porta,
// cadeira, cama, baú trancado e caixa de música. O jogo reconhece estes pelo
// número do tipo (10 é porta, 15 cadeira, 79 cama); o de mod passa por aqui.
class TileUseLoader {
    // Conjuntos que o jogo trata sozinho e que o RightClick do mod repetiria (o
    // baú abriria e fecharia no mesmo toque): desligados durante o do jogo.
    static #SHADOWED = ['BasicChest', 'BasicDresser', 'Campfires'];

    static Install() {
        Safe.Run('toque nos tiles', TileUseLoader.#HookInteract);
        Safe.Run('portas de mod', TileUseLoader.#HookDoors);
        Safe.Run('sentar e dormir', TileUseLoader.#HookResting);
        Safe.Run('baús de mod', TileUseLoader.#HookChests);
    }

    // O smart-interact escolhe os alvos por uma lista fixa de tipos. Como o
    // patch do tModLoader: o tile de mod entra pelo HasSmartInteract.
    static HookSmartInteract() {
        const Provider = Terraria.GameContent.ObjectInteractions.TileSmartInteractCandidateProvider;

        Provider['void FillPotentialTargetTiles(SmartInteractScanSettings settings)'].hook((original, self, settings) => {
            original(self, settings);

            const found = bl.tiles.find('tile.smart', settings.LX, settings.LY, settings.HX, settings.HY);
            for (let k = 0; k < found.length; k += 2) {
                const i = found[k], j = found[k + 1];
                const m = TileLoader.At(i, j);
                if (!m || !Safe.Run(m.constructor.name + '.HasSmartInteract', () => m.HasSmartInteract(i, j, settings))) continue;

                self.targets.Add(self['int JoinValue(int x, int y)'](i, j));
            }
        });
    }

    static WantNonSolidAnchor() {
        Hooks.Once('tile.nonSolidAnchor', () => Safe.Run('colocar ao lado de não sólido', TileUseLoader.#HookNonSolidAnchor));
    }

    // O TileID.Sets.CanPlaceNextToNonSolidTile do tModLoader: o jogo tem a
    // lista fixa (fogo vivo, moedas, teias...) no PlaceThing; para o tile de
    // mod marcado, basta um vizinho com tile ou parede.
    static #HookNonSolidAnchor() {
        const Player = Terraria.Player;
        const Sets = Terraria.ID.TileID.Sets;
        const tileAt = (x, y) => Terraria.Main.tile['Tile get_Item(int x, int y)'](x, y);
        const anchors = (x, y) => [[1, 0], [-1, 0], [0, 1], [0, -1]].some(([dx, dy]) => {
            const t = tileAt(x + dx, y + dy);
            return (t.sTileHeader & 0x20) !== 0 || t.wall > 0;
        });

        Player['bool PlaceThing_Tiles_BlockPlacementForAssortedThings(bool canPlace)'].hook((original, self, canPlace) => {
            const result = original(self, canPlace);
            if (result) return result;

            const type = self.inventory[self.selectedItemState.selected].createTile;
            if (type < FIRST_TILE || !Sets.CanPlaceNextToNonSolidTile[type]) return result;

            return anchors(Player.tileTargetX, Player.tileTargetY);
        });
    }

    // O quadro em que cada jogador tocou por último num tile de mod.
    static #lastPress = new Map();

    // O toque começou agora? No PC é o releaseUseTile. No celular o toque num
    // tile de mod começa mirando outro alvo e só chega ao tile no quadro
    // seguinte, com o releaseUseTile já falso: vale o primeiro quadro com
    // tileInteractAttempted depois de um sem.
    static #FreshPress(player) {
        if (!player.tileInteractAttempted) return false;

        const frame = Terraria.Main.GameUpdateCount;
        const last = TileUseLoader.#lastPress.get(player.whoAmI);
        TileUseLoader.#lastPress.set(player.whoAmI, frame);
        return player.releaseUseTile || last === undefined || frame - last > 1;
    }

    static #HookInteract() {
        const Player = Terraria.Player;
        const at = TileLoader.At;

        Player['void TileInteractionsUse(int myX, int myY)'].hook((original, self, x, y) => {
            const m = at(x, y);
            if (!m) return original(self, x, y);

            const use = TileUseLoader.#FreshPress(self);
            const right = TileLoader.Overrides(m, 'RightClick');
            TileUseLoader.#Shadow(m.Type, right, () => original(self, x, y));
            if (!use) return undefined;

            let handled = TileUseLoader.#UseDoor(self, x, y, m.Type);
            if (!handled && !right && TileLoader.MusicBoxes.has(m.Type)) {
                TileUseLoader.ToggleMusicBox(x, y);
                handled = true;
            }
            if (right && Safe.Run(m.constructor.name + '.RightClick', () => m.RightClick(x, y))) handled = true;

            if (handled) self.tileInteractionHappened = true;
            return undefined;
        }, { minType: FIRST_TILE, tileAt: [0, 1] });

        Player['void TileInteractionsMouseOver(int myX, int myY)'].hook((original, self, x, y) => {
            original(self, x, y);
            if (x !== Player.tileTargetX || y !== Player.tileTargetY) return;

            const m = at(x, y);
            if (m) Safe.Run(m.constructor.name + '.MouseOver', () => m.MouseOver(x, y));
        }, { minType: FIRST_TILE, tileAt: [0, 1] });

        Player['void TileInteractionsCheckLongDistance(int myX, int myY)'].hook((original, self, x, y) => {
            original(self, x, y);

            const m = at(x, y);
            if (m) Safe.Run(m.constructor.name + '.MouseOverFar', () => m.MouseOverFar(x, y));
        }, { minType: FIRST_TILE, tileAt: [0, 1] });
    }

    static #Shadow(type, active, run) {
        const Sets = Terraria.ID.TileID.Sets;
        const saved = active ? TileUseLoader.#SHADOWED.filter((name) => Sets[name][type]) : [];
        for (const name of saved) Sets[name][type] = false;
        try {
            return run();
        } finally {
            for (const name of saved) Sets[name][type] = true;
        }
    }

    // Porta fechada abre para o lado do jogador (ou o outro); aberta fecha.
    static #UseDoor(player, x, y, type) {
        const W = Terraria.WorldGen;
        const Sets = Terraria.ID.TileID.Sets;
        const send = (action, dir) => {
            if (Terraria.Main.netMode === 1) {
                Terraria.NetMessage['void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)'](
                    19, -1, -1, null, action, x, y, dir, 0, 0, 0);
            }
        };

        if (Sets.OpenDoorID[type] >= 0) {
            const dir = player.direction;
            if (W['bool OpenDoor(int i, int j, int direction)'](x, y, dir)) send(0, dir);
            else if (W['bool OpenDoor(int i, int j, int direction)'](x, y, -dir)) send(0, -dir);
            return true;
        }
        if (Sets.CloseDoorID[type] >= 0) {
            if (W['bool CloseDoor(int i, int j, bool forced)'](x, y, false)) send(1, player.direction);
            return true;
        }
        return false;
    }

    // As portas do jogo são o tipo 10 (fechada) e 11 (aberta). A de mod vira
    // porta do jogo durante o OpenDoor/CloseDoor, e o que ele montou volta a
    // ser de mod: o mesmo espaço livre, as mesmas âncoras, o mesmo som.
    static #HookDoors() {
        const W = Terraria.WorldGen;
        const Sets = Terraria.ID.TileID.Sets;

        W['bool OpenDoor(int i, int j, int direction)'].hook((original, i, j, direction) => {
            const closed = bl.tiles.typeAt(i, j);
            const open = Sets.OpenDoorID[closed];
            if (open < 0) return original(i, j, direction);

            const top = j - Math.floor((TileLoader.Tile(i, j).frameY % 54) / 18);
            return TileUseLoader.#AsVanilla(i - 1, top, 3, 3, closed, 10, 11, open, () => original(i, j, direction));
        }, { minType: FIRST_TILE, tileAt: [0, 1] });

        W['bool CloseDoor(int i, int j, bool forced)'].hook((original, i, j, forced) => {
            const open = bl.tiles.typeAt(i, j);
            const closed = Sets.CloseDoorID[open];
            if (closed < 0) return original(i, j, forced);

            const corner = Terraria.ObjectData.TileObjectData.TopLeft(i, j);
            return TileUseLoader.#AsVanilla(corner.X - 1, corner.Y, 4, 3, open, 11, 10, closed, () => original(i, j, forced));
        }, { minType: FIRST_TILE, tileAt: [0, 1] });
    }

    // No retângulo: `from` vira `asType`, roda `run`; se deu certo, o que virou
    // `result` passa a `to`; se não, tudo volta.
    static #AsVanilla(left, top, width, height, from, asType, result, to, run) {
        const cells = [];
        for (let x = left; x < left + width; x++) {
            for (let y = top; y < top + height; y++) {
                const type = bl.tiles.typeAt(x, y);
                cells.push({ x, y, type });
                if (type === from) TileLoader.Tile(x, y).type = asType;
            }
        }

        let ok = false;
        try {
            ok = run();
        } finally {
            // O que o jogo não mexeu (ou tudo, se falhou) volta; o que ele montou vira de mod.
            for (const c of cells) {
                const now = bl.tiles.typeAt(c.x, c.y);
                if (now === asType && c.type === from) TileLoader.Tile(c.x, c.y).type = from;
                else if (ok && now === result && c.type !== result) TileLoader.Tile(c.x, c.y).type = to;
            }
        }
        return ok;
    }

    // Cadeira e cama de mod: o jogo acha o lugar como para um tipo que não
    // conhece, e o ModifySittingTargetInfo/ModifySleepingTargetInfo acerta.
    static #HookResting() {
        const GC = Terraria.GameContent;

        GC.PlayerSittingHelper['bool GetSittingTargetInfo(Player player, int x, int y, out int targetDirection, out Vector2 playerSittingPosition, out Vector2 seatDownOffset, out ExtraSeatInfo extraInfo)'].hook(
            (original, player, x, y, direction, position, seatOffset, extra) => {
                if (!original(player, x, y, direction, position, seatOffset, extra)) return false;

                const m = TileLoader.At(x, y);
                if (!m) return true;

                const info = new TileRestingInfo(player, x, y, Vector2.new(0, 0), 1, 6);
                Safe.Run(m.constructor.name + '.ModifySittingTargetInfo', () => m.ModifySittingTargetInfo(x, y, info));

                direction.value = info.TargetDirection;
                seatOffset.value = info.VisualOffset;
                position.value = TileUseLoader.#RestingPosition(info, 0);

                const seat = extra.value;
                seat.IsAToilet = !!info.ExtraInfo.IsAToilet;
                extra.value = seat;
                return true;
            }, { minType: FIRST_TILE, tileAt: [1, 2] });

        GC.PlayerSleepingHelper['bool GetSleepingTargetInfo(int x, int y, out int targetDirection, out Vector2 anchorPosition, out Vector2 visualoffset)'].hook(
            (original, x, y, direction, anchor, visualOffset) => {
                if (!original(x, y, direction, anchor, visualOffset)) return false;

                const m = TileLoader.At(x, y);
                if (!m) return true;

                // O jogo pôs a âncora em (x1, y1 + 1) em pixels: (x1 * 16 + 8, (y1 + 1) * 16 + 16).
                const a = anchor.value;
                const info = new TileRestingInfo(null, Math.round((a.X - 8) / 16), Math.round((a.Y - 16) / 16) - 1,
                    Vector2.new(-9, 1), direction.value);
                Safe.Run(m.constructor.name + '.ModifySleepingTargetInfo', () => m.ModifySleepingTargetInfo(x, y, info));

                direction.value = info.TargetDirection;
                visualOffset.value = info.VisualOffset;
                anchor.value = TileUseLoader.#RestingPosition(info, 1);
                return true;
            }, { minType: FIRST_TILE, tileAt: [0, 1] });

        // Cama de mod com TileID.Sets.IsValidSpawnPoint: o CheckSpawn do jogo
        // quer o tipo 79 logo acima do ponto; ela vira cama do jogo na conta.
        Terraria.Player['bool CheckSpawn(int x, int y)'].hook((original, x, y) => {
            const type = bl.tiles.typeAt(x, y - 1);
            if (type < FIRST_TILE || !Terraria.ID.TileID.Sets.IsValidSpawnPoint[type]) return original(x, y);

            const tile = TileLoader.Tile(x, y - 1);
            tile.type = 79;
            try {
                return original(x, y);
            } finally {
                TileLoader.Tile(x, y - 1).type = type;
            }
        });
    }

    static #RestingPosition(info, extraRow) {
        const p = info.AnchorTilePosition;
        return Vector2.new(
            p.X * 16 + 8 + info.TargetDirection * info.DirectionOffset + info.FinalOffset.X,
            (p.Y + extraRow) * 16 + 16 + info.FinalOffset.Y);
    }

    static #HookChests() {
        const Chest = Terraria.Chest;
        const at = TileLoader.At;
        const filter = { minType: FIRST_TILE, tileAt: [0, 1], marks: 'tile.chest' };

        Chest['bool IsLocked(int x, int y)'].hook((original, x, y) => {
            const m = at(x, y);
            return m ? !!Safe.Run(m.constructor.name + '.IsLockedChest', () => m.IsLockedChest(x, y)) : original(x, y);
        }, filter);

        Chest['bool Unlock(int X, int Y)'].hook((original, x, y) => {
            const m = at(x, y);
            if (!m) return original(x, y);

            const shift = new Ref(-36), dust = new Ref(11), manual = new Ref(false);
            if (!Safe.Run(m.constructor.name + '.UnlockChest', () => m.UnlockChest(x, y, shift, dust, manual))) return false;
            if (!manual.value) TileUseLoader.#ShiftChest(x, y, m.Type, shift.value, dust.value);
            return true;
        }, filter);

        Chest['bool Lock(int X, int Y)'].hook((original, x, y) => {
            const m = at(x, y);
            if (!m) return original(x, y);

            const shift = new Ref(36), manual = new Ref(false);
            if (!Safe.Run(m.constructor.name + '.LockChest', () => m.LockChest(x, y, shift, manual))) return false;
            if (!manual.value) TileUseLoader.#ShiftChest(x, y, m.Type, shift.value, -1);
            return true;
        }, filter);
    }

    static #ShiftChest(x, y, type, shift, dust) {
        Terraria.Audio.SoundEngine['SoundEffectInstance PlaySound(int type, int x, int y, int Style, float volumeScale, float pitchOffset)'](
            22, x * 16, y * 16, 1, 1, 0);

        const newDust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];
        for (let i = x; i <= x + 1; i++) {
            for (let j = y; j <= y + 1; j++) {
                if (bl.tiles.typeAt(i, j) !== type) continue;

                const tile = TileLoader.Tile(i, j);
                tile.frameX = tile.frameX + shift;
                if (dust < 0) continue;
                for (let k = 0; k < 4; k++) newDust(Vector2.new(i * 16, j * 16), 16, 16, dust, 0, 0, 0, Color.White, 1);
            }
        }
    }

    // A caixa de música 2x2 do jogo (WorldGen.SwitchMB): quadro X + 36 toca.
    static ToggleMusicBox(i, j) {
        const type = bl.tiles.typeAt(i, j);
        const tile = TileLoader.Tile(i, j);
        const left = i - Math.floor(tile.frameX / 18) % 2;
        const top = j - Math.floor(tile.frameY / 18) % 2;

        for (let x = left; x < left + 2; x++) {
            for (let y = top; y < top + 2; y++) {
                if (bl.tiles.typeAt(x, y) !== type) continue;

                const cell = TileLoader.Tile(x, y);
                cell.frameX = cell.frameX < 36 ? cell.frameX + 36 : cell.frameX - 36;
            }
        }
        if (Terraria.Main.netMode === 1) {
            Terraria.NetMessage['void SendTileSquare(int whoAmi, int tileX, int tileY, int xSize, int ySize, TileChangeType changeType)'](-1, left, top, 2, 2, 0);
        }
    }
}
