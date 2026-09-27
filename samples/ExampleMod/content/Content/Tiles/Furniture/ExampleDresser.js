const { DustID, ItemID, SoundID, TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const Main = Terraria.Main;
const Chest = Terraria.Chest;

const tileAt = (i, j) => Main.tile['Tile get_Item(int x, int y)'](i, j);

// Cômoda 3x2: a linha de cima abre como baú, a de baixo o guarda-roupa.
export class ExampleDresser extends ModTile {
    SetStaticDefaults() {
        Main.tileSolidTop[this.Type] = true;
        Main.tileFrameImportant[this.Type] = true;
        Main.tileNoAttach[this.Type] = true;
        Main.tileTable[this.Type] = true;
        Main.tileContainer[this.Type] = true;
        Main.tileLavaDeath[this.Type] = true;
        TileID.Sets.HasOutlines[this.Type] = true;
        TileID.Sets.DisableSmartCursor[this.Type] = true;
        TileID.Sets.BasicDresser[this.Type] = true;
        TileID.Sets.AvoidedByNPCs[this.Type] = true;
        TileID.Sets.InteractableByNPCs[this.Type] = true;
        TileID.Sets.IsAContainer[this.Type] = true;
        TileID.Sets.RoomNeeds.CountsAsTable[this.Type] = true;

        this.AdjTiles = [TileID.Dressers];
        this.DustType = DustID.Platinum;
        this.AddMapEntry(Color.new(200, 200, 200), this.CreateMapEntryName());

        // A forma e os ganchos de colocar (criar o Main.chest) da cômoda do jogo.
        const vanilla = TileObjectData['TileObjectData GetTileData(int type, int style, int alternate)'](TileID.Dressers, 0, 0);
        const tile = TileObjectData.newTile;
        tile.CopyFrom(TileObjectData.Style3x2);
        tile.HookCheckIfCanPlace = vanilla.HookCheckIfCanPlace;
        tile.HookPostPlaceMyPlayer = vanilla.HookPostPlaceMyPlayer;
        tile.AnchorInvalidTiles = [TileID.MagicalIceBlock, TileID.Boulder, TileID.BouncyBoulder, TileID.LifeCrystalBoulder, TileID.RollingCactus];
        tile.LavaDeath = false;
        TileObjectData.addTile(this.Type);
    }

    RightClick(i, j) {
        const player = Main.LocalPlayer;
        const tile = tileAt(i, j);
        const left = i - Math.floor(tile.frameX / 18) % 3;
        const top = j - Math.floor(tile.frameY / 18);

        if (tile.frameY !== 0) {
            // A gaveta de baixo: as roupas.
            Main.playerInventory = false;
            player.chest = -1;
            player.SetTalkNPC(-1);
            Main.npcChatCornerItem = 0;
            Main.npcChatText = '';
            Main.interactedDresserTopLeftX = left;
            Main.interactedDresserTopLeftY = top;
            Main.OpenClothesWindow();
            return true;
        }

        Main['void CancelClothesWindow(bool quiet)'](true);
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

        const chest = Chest.FindChest(left, top);
        if (chest === -1) return true;

        Main.stackSplit = 600;
        if (chest === player.chest) {
            player.chest = -1;
            SoundEngine.PlaySound(SoundID.MenuClose);
        } else {
            const wasClosed = player.chest === -1;
            player.OpenChest(left, top, chest);
            SoundEngine.PlaySound(wasClosed ? SoundID.MenuOpen : SoundID.MenuTick);
        }
        return true;
    }

    #MouseOverShared(player, i, j) {
        const tile = tileAt(i, j);
        const left = i - Math.floor((tile.frameX % 54) / 18);
        const top = tile.frameY % 36 !== 0 ? j - 1 : j;
        const chest = Chest.FindChest(left, top);

        player.cursorItemIconID = -1;
        player.cursorItemIconText = '';
        if (chest >= 0 && Main.chest[chest].name !== '') player.cursorItemIconText = Main.chest[chest].name;
        else player.cursorItemIconID = ModContent.ItemType('ExampleDresser');

        player.noThrow = 2;
        player.cursorItemIconEnabled = true;
    }

    MouseOverFar(i, j) {
        const player = Main.LocalPlayer;
        this.#MouseOverShared(player, i, j);
        if (player.cursorItemIconText === '') {
            player.cursorItemIconEnabled = false;
            player.cursorItemIconID = ItemID.None;
        }
    }

    MouseOver(i, j) {
        const player = Main.LocalPlayer;
        this.#MouseOverShared(player, i, j);
        if (tileAt(i, j).frameY > 0) {
            player.cursorItemIconID = ItemID.FamiliarShirt;
            player.cursorItemIconText = '';
        }
    }

    NumDust(i, j, fail, num) {
        num.value = fail ? 1 : 3;
    }

    KillMultiTile(i, j, frameX, frameY) {
        Chest.DestroyChest(i, j);
    }
}
