const hslToRgb = Terraria.Main['Color hslToRgb(float Hue, float Saturation, float Luminosity, byte a)'];

export class ExampleTooltipItem extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/ExampleItem';
    }

    SetDefaults() {
        this.Item.width = 20;
        this.Item.height = 20;
        this.Item.maxStack = ModItem.CommonMaxStack;
        this.Item.rare = ItemRarityID.Blue;
        this.Item.value = Terraria.Item.buyPrice(0, 0, 1, 0);
    }

    ModifyTooltips(item, tooltips) {
        const shift = Terraria.Main.GlobalTimeWrappedHourly * 0.25;
        let text = '';
        let letter = 0;
        for (const ch of 'Bunny Loader!') {
            if (ch === ' ') {
                text += ch;
                continue;
            }
            const hue = (letter++ / 12 + shift) % 1;
            text += TooltipLine.colorTag(ch, hslToRgb(hue, 1, 0.6, 255));
        }
        tooltips.splice(1, 0, new TooltipLine('BunnyLoader', text));

        const description = tooltips.find((line) => line.Name === 'Line1');
        if (description) description.OverrideColor = Color.new(255, 215, 90);
    }
}
