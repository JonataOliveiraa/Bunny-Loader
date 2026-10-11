// Os pilares de mod (ModPylon) no sistema de pilares do jogo
// (Main.PylonSystem, TeleportPylonsSystem):
//   - a lista: o UpdatePylonsListAndBroadcastChanges do jogo só vê os dele
//     (TileEntity.ByPosition); os de mod (TEModdedPylon) entram depois, e a
//     diferença vai aos clientes pela mensagem do próprio jogo;
//   - "perto de um pilar": o jogo só procura o tile 597;
//   - quem decide o teletransporte: NPCs e bioma pelo ModPylon do destino e
//     do pilar de onde se sai;
//   - o mapa: o jogo desenharia um quadro que não existe para o tipo novo; os
//     de mod saem da lista durante o desenho dele e são desenhados aqui;
//   - colocar: um só por tipo (CanPlacePylon).
class PylonLoader {
    static ByPylonType = new Map();   // PylonType -> ModPylon
    static #FIRST = 11;               // TeleportPylonType.Count
    static #current = [];             // os de mod na lista do jogo agora: [{ x, y, type }]

    static Add(pylon) {
        pylon.PylonType = PylonLoader.#FIRST + PylonLoader.ByPylonType.size;
        PylonLoader.ByPylonType.set(pylon.PylonType, pylon);
        bl.hookMarks.set('pylon.place', pylon.Type);
        Hooks.Once('pylon.hooks', () => Safe.Run('ModPylon: ganchos', () => PylonLoader.#Hook()));
    }

    // Uma TEModdedPylon entrou ou saiu: o jogo refaz a lista no próximo Update.
    static Changed() {
        const Main = Terraria.Main;
        if (Main.netMode === 1 || Main.gameMenu || !Main.PylonSystem) return;
        Main.PylonSystem.RequestImmediateUpdate();
    }

    static IsModType(type) { return type >= PylonLoader.#FIRST; }

    // O ícone padrão: <Texture>_MapIcon.png, ou o do pilar da superfície do jogo.
    static MapIcon(pylon) {
        if (pylon.__mapIcon === undefined) {
            const mod = pylon.Mod;
            const file = bl.path.join(mod.path, ModFiles.Texture(pylon.MapIconTexture, mod));
            pylon.__mapIcon = bl.file.exists(file) ? ContentLookup.RequestFile(file) : null;
        }
        return pylon.__mapIcon;
    }

    // A chave do nome do item que coloca o pilar (o texto ao tocar no ícone).
    static ItemNameKey(pylon) {
        const item = TileLoader.GetItemDropFromTypeAndStyle(pylon.Type, 0);
        return item > 0 ? Terraria.Lang.GetItemName(item).Key : 'Mods.BunnyLoader.Pylon';
    }

    static #Frame(columns, column) {
        const frame = Terraria.DataStructures.SpriteFrame.new();
        frame['void .ctor(byte columns, byte rows, byte currentColumn, byte currentRow)'](columns, 1, column, 0);
        frame.PaddingY = 0;
        return frame;
    }

    static DrawIcon(context, icon, center, color, deselectedScale, selectedScale) {
        const draw = 'DrawResult Draw(Texture2D texture, Vector2 position, Color color, SpriteFrame frame, float scaleIfNotSelected, float scaleIfSelected, Alignment alignment)';
        const ctx = context instanceof Ref ? context.value : context;
        let texture, frame;
        if (icon) {
            texture = 'Value' in icon ? icon.Value : icon;
            frame = PylonLoader.#Frame(1, 0);
        } else {
            texture = Terraria.GameContent.TextureAssets.Extra[182].Value;
            frame = PylonLoader.#Frame(11, 0);
        }
        return ctx[draw](texture, center, color, frame, deselectedScale, selectedScale, Terraria.UI.Alignment.Center).IsMouseOver;
    }

    static ClickIcon(mouseIsHovering, info, textKey, mouseOverText) {
        if (!mouseIsHovering) return;
        const Main = Terraria.Main;
        Main.cancelWormHole = true;
        const text = Terraria.Localization.Language['string GetTextValue(string key)'](textKey);
        if (mouseOverText instanceof Ref) mouseOverText.value = text;
        if (!Main.mouseLeft || !Main.mouseLeftRelease) return;
        Main.mouseLeftRelease = false;
        Main.mapFullscreen = false;
        Main.PylonSystem.RequestTeleportation(info, Main.player[Main.myPlayer]);
    }

    // O DefaultDrawPylonCrystal do tModLoader, sem o contorno do cursor esperto.
    static DrawCrystal(spriteBatch, i, j, crystalTexture, crystalOffset, shadowColor, dustColor, dustChance, frames) {
        const Main = Terraria.Main;
        if (bl.tiles.typeAt(i, j) < 0) return;
        const tile = TileLoader.Tile(i, j);
        const data = Terraria.ObjectData.TileObjectData['TileObjectData GetTileData(Tile getTile)'](tile);
        if (!data) return;
        const texture = 'Value' in crystalTexture ? crystalTexture.Value : crystalTexture;
        const frameHeight = Math.floor(texture.Height / frames);
        const frameY = Math.floor(Main.tileFrameCounter[Terraria.ID.TileID.TeleportationPylon] / frames) % frames;
        const source = Rectangle.new(0, frameY * frameHeight, texture.Width, frameHeight - 1);
        const origin = Vector2.new(source.Width / 2, source.Height / 2);
        const offscreen = Main.drawToScreen ? 0 : Main.offScreenRange;
        const time = Main.GlobalTimeWrappedHourly;
        const bob = Math.sin(time * Math.PI * 2 / 5);
        const cx = i * 16 + data.CoordinateFullWidth / 2 - 2 + crystalOffset.X;
        const cy = j * 16 + data.CoordinateFullHeight / 2 + crystalOffset.Y;
        const sp = Main.screenPosition;
        const x = cx + offscreen - sp.X, y = cy + offscreen + bob * 4 - sp.Y;

        if (!Main.gamePaused && dustChance > 0 && Math.random() < 1 / (4 * dustChance)) {
            const dust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'](
                Vector2.new(cx - source.Width / 2, cy - source.Height / 2), source.Width, source.Height, 43, 0, 0, 254, dustColor, 0.5);
            const d = Main.dust[dust];
            d.velocity = Vector2.new(d.velocity.X * 0.1, d.velocity.Y * 0.1 - 0.2);
        }

        const draw = Microsoft.Xna.Framework.Graphics.SpriteBatch['void Draw(Texture2D texture, Vector2 position, Nullable<Rectangle> sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'];
        const light = Terraria.Lighting['Color GetColor(int x, int y)'](i, j);
        const mix = (a, b) => Math.round(a + (b - a) * 0.8);
        const lit = Color.new(Math.round(mix(light.R, 255) * 0.7), Math.round(mix(light.G, 255) * 0.7), Math.round(mix(light.B, 255) * 0.7), Math.round(mix(light.A, 255) * 0.7));
        draw(spriteBatch, texture, Vector2.new(x, y), source, lit, 0, origin, 1, 0, 0);

        const pulse = Math.sin(time * Math.PI * 2) * 0.2 + 0.8;
        const shadow = Color.new(Math.round(shadowColor.R * pulse), Math.round(shadowColor.G * pulse), Math.round(shadowColor.B * pulse), Math.round(shadowColor.A * pulse));
        for (let k = 0; k < 6; k++) {
            const angle = Math.PI * 2 * k / 6, r = 6 + bob * 2;
            draw(spriteBatch, texture, Vector2.new(x + Math.cos(angle) * r, y + Math.sin(angle) * r), source, shadow, 0, origin, 1, 0, 0);
        }
    }

    // Os de mod no mundo agora: as TEModdedPylon sobre um ModPylon.
    static #Current() {
        const out = [];
        for (const e of TileEntityLoader.ByID.values()) {
            if (!(e instanceof TEModdedPylon)) continue;
            const pylon = e.TryGetModPylon();
            if (pylon) out.push({ x: e.Position.X, y: e.Position.Y, type: pylon.PylonType });
        }
        return out;
    }

    static #Info(entry) {
        const info = Terraria.GameContent.TeleportPylonInfo.new();
        info.PositionInTiles = Point16.new(entry.x, entry.y);
        info.TypeOfPylon = entry.type;
        return info;
    }

    // Tira da lista do jogo os de mod; devolve o que tirou.
    static #Strip(list) {
        const removed = [];
        for (let k = list.Count - 1; k >= 0; k--) {
            const info = list.get_Item(k);
            if (!PylonLoader.IsModType(info.TypeOfPylon)) continue;
            removed.push({ x: info.PositionInTiles.X, y: info.PositionInTiles.Y, type: info.TypeOfPylon });
            list.RemoveAt(k);
        }
        return removed;
    }

    // O ValidTeleportCheck_BiomeRequirements com o SceneMetrics do sistema
    // varrido em volta do pilar (o centro do bloco, como o ToWorldCoordinates).
    static #AcceptsBiome(system, pylon, info) {
        const metrics = system._sceneMetrics;
        const settings = Terraria.SceneMetricsScanSettings.new();
        settings.BiomeScanCenterPositionInWorld = Vector2.new(info.PositionInTiles.X * 16 + 8, info.PositionInTiles.Y * 16 + 8);
        metrics['void Scan(SceneMetricsScanSettings settings)'](settings);
        return Safe.Run(pylon.constructor.name + '.ValidTeleportCheck_BiomeRequirements',
            () => pylon.ValidTeleportCheck_BiomeRequirements(info, metrics)) === true;
    }

    static #Hook() {
        const System = Terraria.GameContent.TeleportPylonsSystem;
        const self = PylonLoader;
        const same = (a, b) => a.x === b.x && a.y === b.y && a.type === b.type;
        const send = (entry, sub) => Terraria.Net.NetManager.Instance.BroadcastOrLoopback(
            Terraria.GameContent.NetModules.NetTeleportPylonModule['NetPacket SerializePylonWasAddedOrRemoved(TeleportPylonInfo info, NetTeleportPylonModule.SubPacketType packetType)'](self.#Info(entry), sub));

        // A lista: os de mod fora durante o do jogo (senão ele os anunciaria
        // como tirados a cada vez), depois dentro, com a diferença anunciada.
        System['void UpdatePylonsListAndBroadcastChanges()'].hook((original, system) => {
            self.#Strip(system._pylons);
            original(system);
            Safe.Run('ModPylon: lista', () => {
                const now = self.#Current(), before = self.#current;
                for (const entry of now) system._pylons.Add(self.#Info(entry));
                for (const entry of now) if (!before.some((b) => same(b, entry))) send(entry, 0);
                for (const entry of before) if (!now.some((n) => same(n, entry))) send(entry, 1);
                self.#current = now;
            });
        });

        System['bool IsPlayerNearAPylon(Player player)'].hook((original, player) => {
            if (original(player)) return true;
            const reach = Terraria.DataStructures.TileReachCheckSettings.Pylons;
            for (const pylon of self.ByPylonType.values()) {
                if (player['bool IsTileTypeInInteractionRange(int targetTileType, TileReachCheckSettings settings)'](pylon.Type, reach)) return true;
            }
            return false;
        });

        const request = System['void HandleTeleportRequest(TeleportPylonInfo info, int playerIndex)'];
        // O bioma do pilar de destino. Não dá para hookar o
        // DoesPylonAcceptTeleportation: o celular a chama sem passar o `player`
        // (ela não o usa) e o hook leria o lixo do registrador (derrubou o
        // servidor no multijogador). Para tipos de mod ela já devolve true; o
        // ValidTeleportCheck_BiomeRequirements de quem o escreve roda antes, com
        // o SceneMetrics varrido em volta do destino, como o jogo faz.
        request.hook((original, system, info, playerIndex) => {
            const pylon = self.ByPylonType.get(info.TypeOfPylon);
            if (pylon && Hooks.Overrides(pylon.constructor, ModPylon, 'ValidTeleportCheck_BiomeRequirements') && !self.#AcceptsBiome(system, pylon, info)) {
                Terraria.Chat.ChatHelper['void SendChatMessageToClient(NetworkText text, Color color, int playerId)'](
                    Terraria.Localization.NetworkText['NetworkText FromKey(string key, object[] substitutions)']('Net.CannotTeleportToPylonBecauseNotMeetingBiomeRequirements', []),
                    Terraria.Chat.ChatColors.ServerMessage, playerIndex);
                return undefined;
            }
            return original(system, info, playerIndex);
        });

        System['bool DoesPositionHaveEnoughNPCs(int necessaryNPCCount, Point16 centerPoint)'].hook((original, count, center) => {
            const entry = self.#current.find((e) => e.x === center.X && e.y === center.Y);
            const pylon = entry && self.ByPylonType.get(entry.type);
            if (!pylon) return original(count, center);
            return Safe.Run(pylon.constructor.name + '.ValidTeleportCheck_NPCCount', () => pylon.ValidTeleportCheck_NPCCount(self.#Info(entry), count)) === true;
        }, { whileIn: request });

        Terraria.Map.TeleportPylonsMapLayer['void Draw(ref MapOverlayDrawContext context, ref string text)'].hook((original, layer, context, text) => {
            const list = Terraria.Main.PylonSystem._pylons;
            const mine = self.#Strip(list);
            try {
                original(layer, context, text);
            } finally {
                for (const entry of mine) list.Add(self.#Info(entry));
            }
            if (mine.length) Safe.Run('ModPylon: mapa', () => self.#DrawMap(mine, context, text));
        });

        Terraria.TileObject['bool CanPlace(int x, int y, int type, int style, int dir, out TileObject objectData, bool onlyCheck, Nullable<int> forcedRandom)'].hook(
            (original, x, y, type, style, dir, data, onlyCheck, random) => {
                const pylon = TileLoader.ByType.get(type);
                if (pylon instanceof ModPylon && Safe.Run(pylon.constructor.name + '.CanPlacePylon', () => pylon.CanPlacePylon()) === false) return false;
                return original(x, y, type, style, dir, data, onlyCheck, random);
            }, { minType: 0, arg: 2, marks: 'pylon.place' });
    }

    static #DrawMap(entries, context, text) {
        const Main = Terraria.Main;
        const near = Terraria.GameContent.TeleportPylonsSystem['bool IsPlayerNearAPylon(Player player)'](Main.player[Main.myPlayer]);
        const color = near ? Color.White : Color.new(64, 64, 64, 127);
        for (const entry of entries) {
            const info = PylonLoader.#Info(entry);
            if (!Terraria.Map.TeleportPylonsMapLayer['bool IsRevealed(TeleportPylonInfo info)'](info)) continue;
            const pylon = PylonLoader.ByPylonType.get(entry.type);
            Safe.Run(pylon.constructor.name + '.DrawMapIcon', () => pylon.DrawMapIcon(context, text, info, near, color, 1, 2));
        }
    }
}
