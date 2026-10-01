const { DustID, ItemID, SoundID, TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const { AnchorType } = Terraria.Enums;
const Main = Terraria.Main;
const Chest = Terraria.Chest;

const tileAt = (i, j) => Main.tile['Tile get_Item(int x, int y)'](i, j);
const say = (message, color) => Main['void NewText(string newText, byte R, byte G, byte B, bool onlyCurrentPlayer)'](message, color.R, color.G, color.B, false);

// Baú 2x2. Dois estilos na textura: o normal e o trancado (abre com a chave
// de exemplo, e só de noite). O baú em si (os itens) é o do jogo: Main.chest.
export class ExampleChest extends ModTile {
    SetStaticDefaults() {
        Main.tileSpelunker[this.Type] = true;
        Main.tileContainer[this.Type] = true;
        Main.tileShine2[this.Type] = true;
        Main.tileShine[this.Type] = 1200;
        Main.tileFrameImportant[this.Type] = true;
        Main.tileNoAttach[this.Type] = true;
        Main.tileOreFinderPriority[this.Type] = 500;
        TileID.Sets.HasOutlines[this.Type] = true;
        TileID.Sets.BasicChest[this.Type] = true;
        TileID.Sets.DisableSmartCursor[this.Type] = true;
        TileID.Sets.AvoidedByNPCs[this.Type] = true;
        TileID.Sets.InteractableByNPCs[this.Type] = true;
        TileID.Sets.IsAContainer[this.Type] = true;
        TileID.Sets.FriendlyFairyCanLureTo[this.Type] = true;
        TileID.Sets.GeneralPlacementTiles[this.Type] = false;

        this.DustType = DustID.Platinum;
        this.AdjTiles = [TileID.Containers];
        this.AddMapEntry(Color.new(200, 200, 200), 'ExampleChest');
        this.AddMapEntry(Color.new(0, 141, 63), 'ExampleChestLocked');

        // Os dois estilos dão o baú de exemplo (o trancado também).
        this.RegisterItemDrop(ModContent.ItemType('ExampleChest'), 0, 1);

        // A forma e os ganchos de colocar (criar o Main.chest) do baú do jogo.
        const vanilla = TileObjectData['TileObjectData GetTileData(int type, int style, int alternate)'](TileID.Containers, 0, 0);
        const tile = TileObjectData.newTile;
        tile.CopyFrom(TileObjectData.Style2x2);
        tile.Origin = Point16.new(0, 1);
        tile.CoordinateHeights = [16, 18];
        tile.HookCheckIfCanPlace = vanilla.HookCheckIfCanPlace;
        tile.HookPostPlaceMyPlayer = vanilla.HookPostPlaceMyPlayer;
        tile.AnchorInvalidTiles = [TileID.MagicalIceBlock, TileID.Boulder, TileID.BouncyBoulder, TileID.LifeCrystalBoulder, TileID.RollingCactus];
        tile.StyleHorizontal = true;
        tile.LavaDeath = false;
        tile.AnchorBottom = AnchorData.new(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, tile.Width, 0);
        TileObjectData.addTile(this.Type);
    }

    GetMapOption(i, j) {
        return Math.floor(tileAt(i, j).frameX / 36);
    }

    DefaultContainerName(frameX, frameY) {
        return ModLocalization.Translate(Math.floor(frameX / 36) === 1 ? 'MapObject.ExampleChestLocked' : 'MapObject.ExampleChest');
    }

    IsLockedChest(i, j) {
        return Math.floor(tileAt(i, j).frameX / 36) === 1;
    }

    UnlockChest(i, j, frameXAdjustment, dustType, manual) {
        if (Main.dayTime) {
            say('Only openable at night', Color.Orange);
            return false;
        }
        dustType.value = this.DustType;
        return true;
    }

    // Só o estilo 0 (destrancado) se tranca de novo.
    LockChest(i, j, frameXAdjustment, manual) {
        return TileObjectData.GetTileStyle(tileAt(i, j)) === 0;
    }

    NumDust(i, j, fail, num) {
        num.value = 1;
    }

    KillMultiTile(i, j, frameX, frameY) {
        Chest.DestroyChest(i, j);
    }

    RightClick(i, j) {
        const player = Main.LocalPlayer;
        const { left, top } = ExampleChest.TopLeft(i, j);

        Main.mouseRightRelease = false;
        player.CloseSign(false);
        player.SetTalkNPC(-1);
        Main.npcChatCornerItem = 0;
        Main.npcChatText = '';
        if (Main.editChest) {
            SoundEngine.PlaySound(SoundID.MenuTick);
            Main.editChest = false;
            Main.npcChatText = '';
        }

        if (Chest['bool IsLocked(int x, int y)'](left, top)) {
            const key = ModContent.ItemType('ExampleChestKey');
            if (player['bool HasItem(int type)'](key) && Chest.Unlock(left, top)) player.ConsumeItem(key, false, false);
            return true;
        }

        const chest = Chest.FindChest(left, top);
        if (chest === -1) return true;

        Main.stackSplit = 600;
        if (chest === player.chest) {
            player.chest = -1;
            SoundEngine.PlaySound(SoundID.MenuClose);
        } else {
            SoundEngine.PlaySound(player.chest < 0 ? SoundID.MenuOpen : SoundID.MenuTick);
            player.OpenChest(left, top, chest);
        }
        return true;
    }

    MouseOver(i, j) {
        const player = Main.LocalPlayer;
        const { left, top } = ExampleChest.TopLeft(i, j);
        const chest = Chest.FindChest(left, top);

        player.cursorItemIconID = -1;
        player.cursorItemIconText = '';
        if (chest >= 0 && Main.chest[chest].name.length > 0) {
            player.cursorItemIconText = Main.chest[chest].name;
        } else {
            player.cursorItemIconID = Math.floor(tileAt(left, top).frameX / 36) === 1
                ? ModContent.ItemType('ExampleChestKey')
                : ModContent.ItemType('ExampleChest');
        }
        player.noThrow = 2;
        player.cursorItemIconEnabled = true;
    }

    MouseOverFar(i, j) {
        this.MouseOver(i, j);
        const player = Main.LocalPlayer;
        if (player.cursorItemIconText === '') {
            player.cursorItemIconEnabled = false;
            player.cursorItemIconID = ItemID.None;
        }
    }

    static TopLeft(i, j) {
        const tile = tileAt(i, j);
        return { left: tile.frameX % 36 !== 0 ? i - 1 : i, top: tile.frameY !== 0 ? j - 1 : j };
    }
}
