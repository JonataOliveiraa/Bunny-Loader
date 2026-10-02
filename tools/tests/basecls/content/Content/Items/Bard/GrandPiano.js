import { BardItem } from './BardItem.js';

export class GrandPiano extends BardItem {
    InspirationCost = 3;

    SetDefaults() {
        super.SetDefaults();
        this.Item.damage = 40;
        this.Item.width = 40;
        this.Item.height = 30;
    }
}
