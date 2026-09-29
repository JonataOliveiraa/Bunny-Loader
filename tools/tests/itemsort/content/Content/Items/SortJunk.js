// Raridade -1 (cinza): só a última camada, "Last - Trash", o aceita.
export class SortJunk extends ModItem {
    SetDefaults(item) {
        item.maxStack = 9999;
        item.rare = -1;
    }
}
