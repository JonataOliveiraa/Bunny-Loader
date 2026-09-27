const { BuffID, DustID, ProjectileID, TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const { TileObjectDirection } = Terraria.Enums;
const Main = Terraria.Main;

const tileAt = (i, j) => Main.tile['Tile get_Item(int x, int y)'](i, j);
const NewProjectile = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];
const SIT_DISTANCE = 40;

// Vaso sanitário: uma cadeira com ExtraInfo.IsAToilet. No fio, dá a descarga.
export class ExampleToilet extends ModTile {
    static NextStyleHeight = 40;

    SetStaticDefaults() {
        Main.tileFrameImportant[this.Type] = true;
        Main.tileNoAttach[this.Type] = true;
        Main.tileLavaDeath[this.Type] = true;
        TileID.Sets.HasOutlines[this.Type] = true;
        TileID.Sets.CanBeSatOnForNPCs[this.Type] = true;
        TileID.Sets.CanBeSatOnForPlayers[this.Type] = true;
        TileID.Sets.DisableSmartCursor[this.Type] = true;
        TileID.Sets.RoomNeeds.CountsAsChair[this.Type] = true;

        this.DustType = DustID.Platinum;
        this.AdjTiles = [TileID.Toilets];
        this.AddMapEntry(Color.new(200, 200, 200), 'Toilet');

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x2);
        TileObjectData.newTile.CoordinateHeights = [16, 18];
        TileObjectData.newTile.CoordinatePaddingFix = Point16.new(0, 2);
        TileObjectData.newTile.Direction = TileObjectDirection.PlaceLeft;
        TileObjectData.newTile.StyleWrapLimit = 2;
        TileObjectData.newTile.StyleMultiplier = 2;
        TileObjectData.newTile.StyleHorizontal = true;

        TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
        TileObjectData.newAlternate.Direction = TileObjectDirection.PlaceRight;
        TileObjectData.addAlternate(1);
        TileObjectData.addTile(this.Type);
    }

    NumDust(i, j, fail, num) {
        num.value = fail ? 1 : 3;
    }

    ModifySittingTargetInfo(i, j, info) {
        const tile = tileAt(i, j);
        info.TargetDirection = tile.frameX !== 0 ? 1 : -1;
        info.AnchorTilePosition.X = i;
        info.AnchorTilePosition.Y = tile.frameY % ExampleToilet.NextStyleHeight === 0 ? j + 1 : j;
        info.ExtraInfo.IsAToilet = true;

        // Quem está fedendo treme no trono.
        const who = info.RestingEntity;
        if (who && who.FindBuffIndex(BuffID.Stinky) >= 0) info.VisualOffset = Vector2.new(Rand.NextFloat(-2, 2), Rand.NextFloat(-2, 2));
    }

    RightClick(i, j) {
        const player = Main.LocalPlayer;
        if (player.IsWithinSnappngRangeToTile(i, j, SIT_DISTANCE)) {
            player.GamepadEnableGrappleCooldown();
            player.sitting.SitDown(player, i, j);
        }
        return true;
    }

    MouseOver(i, j) {
        const player = Main.LocalPlayer;
        if (!player.IsWithinSnappngRangeToTile(i, j, SIT_DISTANCE)) return;

        player.noThrow = 2;
        player.cursorItemIconEnabled = true;
        player.cursorItemIconID = ModContent.ItemType('ExampleToilet');
        if (tileAt(i, j).frameX < 18) player.cursorItemIconReversed = true;
    }

    HitWire(i, j) {
        const Wiring = Terraria.Wiring;
        const x = i, y = j - Math.floor((tileAt(i, j).frameY % ExampleToilet.NextStyleHeight) / 18);
        Wiring['void SkipWire(int x, int y)'](x, y);
        Wiring['void SkipWire(int x, int y)'](x, y + 1);

        if (Wiring.CheckMech(x, y, 60)) {
            NewProjectile(Wiring.GetProjectileSource(x, y), x * 16 + 8, y * 16 + 12, 0, 0,
                          ProjectileID.ToiletEffect, 0, 0, Main.myPlayer, 0, 0, 0, null);
        }
    }
}
