// Um pilar de teletransporte novo, como o ModPylon do tModLoader: um ModTile
// (3x4, como o do jogo) com uma TEModdedPylon. O tipo dele no sistema de
// pilares (PylonType) vem depois dos do jogo (TeleportPylonType.Count, 11).
// No SetStaticDefaults:
//   TileObjectData.newTile.HookPostPlaceMyPlayer = ModContent.GetInstance(MinhaEntidade).PlacementHook(false);
// e no KillMultiTile: ModContent.GetInstance(MinhaEntidade).Kill(i, j).
// O ícone no mapa: <Texture>_MapIcon.png, ou o DrawMapIcon. Ver PylonLoader.
class ModPylon extends ModTile {
    PylonType = -1;

    get MapIconTexture() { return this.Texture + '_MapIcon'; }

    // Um só por tipo no mundo, como os do jogo.
    CanPlacePylon() { return !Terraria.Main.PylonSystem.HasPylonOfType(this.PylonType); }

    // Os NPCs em volta (o padrão do jogo: 2 moradores, por perto e em casa).
    ValidTeleportCheck_NPCCount(pylonInfo, defaultNecessaryNPCCount) {
        return Terraria.GameContent.TeleportPylonsSystem['bool DoesPositionHaveEnoughNPCs(int necessaryNPCCount, Point16 centerPoint)'](
            defaultNecessaryNPCCount, pylonInfo.PositionInTiles);
    }

    // O bioma em volta (sceneData: o SceneMetrics já varrido ali).
    ValidTeleportCheck_BiomeRequirements(pylonInfo, sceneData) { return true; }

    // O ícone no mapa. O padrão: <Texture>_MapIcon.png (sem ele, o do pilar do
    // jogo) e o toque teletransporta.
    DrawMapIcon(context, mouseOverText, pylonInfo, isNearPylon, drawColor, deselectedScale, selectedScale) {
        const center = Vector2.new(pylonInfo.PositionInTiles.X + 1.5, pylonInfo.PositionInTiles.Y + 2);
        const mouseOver = this.DefaultDrawMapIcon(context, PylonLoader.MapIcon(this), center, drawColor, deselectedScale, selectedScale);
        this.DefaultMapClickHandle(mouseOver, pylonInfo, PylonLoader.ItemNameKey(this), mouseOverText);
    }

    // Desenha o ícone (Asset ou Texture2D) no mapa; true com o dedo em cima.
    DefaultDrawMapIcon(context, mapIcon, drawCenter, drawColor, deselectedScale, selectedScale) {
        return PylonLoader.DrawIcon(context, mapIcon, drawCenter, drawColor, deselectedScale, selectedScale);
    }

    // O nome com o dedo em cima e o teletransporte no toque.
    DefaultMapClickHandle(mouseIsHovering, pylonInfo, hoveringTextKey, mouseOverText) {
        PylonLoader.ClickIcon(mouseIsHovering, pylonInfo, hoveringTextKey, mouseOverText);
    }

    // O cristal flutuando, no SpecialDraw (pedido pelo DrawEffects, abaixo).
    DefaultDrawPylonCrystal(spriteBatch, i, j, crystalTexture, crystalHighlightTexture, crystalOffset, pylonShadowColor, dustColor, dustChanceDenominator, crystalVerticalFrameCount) {
        PylonLoader.DrawCrystal(spriteBatch, i, j, crystalTexture, crystalOffset, pylonShadowColor, dustColor, dustChanceDenominator, crystalVerticalFrameCount);
    }

    // Tocar no pilar abre o mapa como o pilar do jogo: enquadrando os pilares,
    // com as linhas a partir deste (ver PylonLoader.OpenMap).
    RightClick(i, j) {
        PylonLoader.OpenMap(i, j);
        return true;
    }

    HasSmartInteract(i, j, settings) { return true; }

    // O SpecialDraw uma vez, pela célula de cima à esquerda.
    DrawEffects(i, j, spriteBatch, drawData) {
        const data = Terraria.ObjectData.TileObjectData['TileObjectData GetTileData(int type, int style, int alternate)'](this.Type, 0, 0);
        if (data && drawData.tileFrameX % data.CoordinateFullWidth === 0 && drawData.tileFrameY === 0) {
            Terraria.Main.instance.TilesRenderer['void AddSpecialLegacyPoint(int x, int y)'](i, j);
        }
    }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModPylon)) {
            throw new TypeError('ModPylon.register(Class): pass the class, which extends ModPylon');
        }
        const type = ModTile.register(cls);
        PylonLoader.Add(TileLoader.ByType.get(type));
        return type;
    }
}
