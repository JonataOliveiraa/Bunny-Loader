import { SimplePylonTileEntity } from '../TileEntities/SimplePylonTileEntity.js';

const { TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const Main = Terraria.Main;

// Um pilar de teletransporte que funciona como os do jogo (ModPylon). Fica
// na lista de pilares, aparece no mapa (ExamplePylonTile_MapIcon.png) e
// teletransporta para qualquer outro pilar. Os moradores o vendem no bioma de
// exemplo (Common/GlobalNPCs/ExamplePylonShop.js).
export class ExamplePylonTile extends ModPylon {
    static CrystalVerticalFrameCount = 8;

    SetStaticDefaults() {
        Main.tileLighted[this.Type] = true;
        Main.tileFrameImportant[this.Type] = true;

        // As texturas do cristal, pedidas uma vez aqui e não a cada desenho.
        this.crystalTexture = ModContent.Request(this.Texture + '_Crystal');
        this.crystalHighlightTexture = ModContent.Request(this.Texture + '_CrystalHighlight');

        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x4);
        TileObjectData.newTile.LavaDeath = false;
        TileObjectData.newTile.DrawYOffset = 2;
        TileObjectData.newTile.StyleHorizontal = true;
        // O pilar de mod precisa da sua tile entity (TEModdedPylon): ela entra
        // na lista de pilares do jogo ao ser colocada.
        TileObjectData.newTile.HookPostPlaceMyPlayer = ModContent.GetInstance(SimplePylonTileEntity).PlacementHook(false);
        TileObjectData.addTile(this.Type);

        TileID.Sets.InteractableByNPCs[this.Type] = true;

        this.AddMapEntry(Color.White, this.CreateMapEntryName());
    }

    // O ícone do pilar no dedo, em cima dele (tocar abre o mapa: o RightClick
    // do ModPylon).
    MouseOver(i, j) {
        const player = Main.player[Main.myPlayer];
        player.cursorItemIconEnabled = true;
        player.cursorItemIconID = ModContent.ItemType('ExamplePylonItem');
    }

    // O pilar sai, a entidade também (é ela que está na lista de pilares).
    KillMultiTile(i, j, frameX, frameY) {
        ModContent.GetInstance(SimplePylonTileEntity).Kill(i, j);
    }

    // Por diversão, este não pede moradores por perto. Para pedir como os do
    // jogo, não sobrescreva o método.
    ValidTeleportCheck_NPCCount(pylonInfo, defaultNecessaryNPCCount) {
        return true;
    }

    // O bioma do pilar de destino: o sceneData já vem varrido em volta dele.
    // Como o bioma de exemplo, pede 40 Blocos de Exemplo por perto.
    ValidTeleportCheck_BiomeRequirements(pylonInfo, sceneData) {
        return sceneData._tileCounts[ModContent.TileType('ExampleTile')] >= 40;
    }

    // Os pilares do jogo brilham; este, de branco.
    ModifyLight(i, j, r, g, b) {
        r.value = g.value = b.value = 0.75;
    }

    // O cristal flutuando, desenhado como os do jogo. Com mais poeira (1 em vez
    // de 4) e a sombra mais fraca (branco a 10%).
    SpecialDraw(i, j, spriteBatch) {
        this.DefaultDrawPylonCrystal(spriteBatch, i, j, this.crystalTexture, this.crystalHighlightTexture,
            Vector2.new(0, -12), Color.new(25, 25, 25, 25), Color.White, 1, ExamplePylonTile.CrystalVerticalFrameCount);
    }

    // O ícone no mapa já sai do ExamplePylonTile_MapIcon.png, com o nome do
    // ExamplePylonItem ao tocar. Para mudar, sobrescreva o DrawMapIcon e use o
    // DefaultDrawMapIcon e o DefaultMapClickHandle.
}
