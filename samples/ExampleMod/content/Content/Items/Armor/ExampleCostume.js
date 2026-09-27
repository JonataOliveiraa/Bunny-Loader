import { ExampleCostumePlayer } from '../../../Common/Players/ExampleCostumePlayer.js';

const { ArmorIDs, DustID } = Terraria.ID;
const NewDust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];

// A cabeça do fantasia: conjunto de vaidade sozinha, e solta faíscas.
class BlockyHead extends EquipTexture {
    IsVanitySet(head, body, legs) { return true; }

    UpdateVanitySet(player) {
        if (Math.random() >= 1 / 20) return;

        const dust = this.Name === 'ExampleCostume' ? DustID.GoldFlame : DustID.BlueFlare;
        NewDust(player.position, player.width, player.height, dust, 0, 0, 0, Color.White, 1);
    }
}

// O fantasia do tModLoader: um acessório que veste o jogador inteiro (e outro
// visual na água), pelas texturas registradas à mão no Load.
export class ExampleCostume extends ModItem {
    // As texturas _Head/_Body/_Legs são do fantasia, não do item: sem autoload.
    static AutoloadEquip = [];

    Load() {
        const texture = this.Texture;
        EquipLoader.AddEquipTexture(this.Mod, texture + '_Head', EquipType.Head, this, null, new BlockyHead());
        EquipLoader.AddEquipTexture(this.Mod, texture + '_Body', EquipType.Body, this);
        EquipLoader.AddEquipTexture(this.Mod, texture + '_Legs', EquipType.Legs, this);
        EquipLoader.AddEquipTexture(this.Mod, texture + 'Alt_Head', EquipType.Head, this, 'BlockyAlt', new BlockyHead());
        EquipLoader.AddEquipTexture(this.Mod, texture + 'Alt_Body', EquipType.Body, this, 'BlockyAlt');
        EquipLoader.AddEquipTexture(this.Mod, texture + 'Alt_Legs', EquipType.Legs, this, 'BlockyAlt');
    }

    // O fantasia cobre a cabeça, o tronco, os braços e as pernas.
    SetStaticDefaults() {
        for (const name of ['ExampleCostume', 'BlockyAlt']) {
            ArmorIDs.Head.Sets.DrawHead[EquipLoader.GetEquipSlot(this.Mod, name, EquipType.Head)] = false;
            const body = EquipLoader.GetEquipSlot(this.Mod, name, EquipType.Body);
            ArmorIDs.Body.Sets.HidesTopSkin[body] = true;
            ArmorIDs.Body.Sets.HidesArms[body] = true;
            ArmorIDs.Legs.Sets.HidesBottomSkin[EquipLoader.GetEquipSlot(this.Mod, name, EquipType.Legs)] = true;
        }
    }

    SetDefaults() {
        this.Item.width = 24;
        this.Item.height = 28;
        this.Item.accessory = true;
        this.Item.value = Terraria.Item.buyPrice(0, 15, 0, 0);
        this.Item.rare = ItemRarityID.Pink;
        this.Item.hasVanityEffects = true;
    }

    UpdateAccessory(item, player, vanity, hideVisual) {
        if (vanity) return;

        const p = player.GetModPlayer(ExampleCostumePlayer);
        p.BlockyAccessory = true;
        p.BlockyHideVanity = hideVisual;
    }

    // No slot de vaidade: só o visual, sempre.
    UpdateVanity(item, player) {
        const p = player.GetModPlayer(ExampleCostumePlayer);
        p.BlockyHideVanity = false;
        p.BlockyForceVanity = true;
    }
}
