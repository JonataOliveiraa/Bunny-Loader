const { Main, Item } = Terraria;
const ItemSlot = Terraria.UI.ItemSlot;
const place = 'void PlaceItem(ref Item[] inv, ref Item item, int context, int slot, int keepStack)';
let ticks = 0, done = false, checks = 0, failures = 0;

export class EquipProbe extends ModItem {
    Texture = 'Probe';
    HideFromModMenu = true;
    owner = '';
    count = 0;
    payload = null;

    SetDefaults(item) {
        item.width = item.height = 16;
        item.accessory = true;
        item.maxStack = 1;
        this.owner = '';
        this.count = 0;
        this.payload = null;
    }

    SaveData(tag) { tag.Set('owner', this.owner); tag.Set('count', this.count); }
    LoadData(tag) { this.owner = tag.GetString('owner'); this.count = tag.GetInt('count'); }
    NetSend(writer) { writer.WriteString(this.owner); writer.WriteInt32(this.count); }
    NetReceive(reader) { this.owner = reader.ReadString(); this.count = reader.ReadInt32(); }
    Clone(item) {
        const copy = super.Clone(item);
        copy.payload = this.payload && { values: this.payload.values.slice() };
        return copy;
    }
}

export class OtherEquipProbe extends EquipProbe {}

function make(owner = '', count = 0, cls = EquipProbe) {
    const item = Item.new();
    item['void .ctor()']();
    if (owner) {
        item['void SetDefaults(int Type, ItemVariant variant)'](ModContent.ItemType(cls), null);
        Object.assign(item.ModItem, { owner, count, payload: { values: [count] } });
    }
    return item;
}

function check(name, fn) {
    checks++;
    try {
        if (!fn()) throw Error('resultado falso');
        bl.log('itemequip PASS ' + name);
    } catch (error) {
        failures++;
        bl.log('itemequip FAIL ' + name + ': ' + error + ' ' + error.stack);
    }
}

function state(item, owner, count) {
    const m = item.ModItem;
    return m && m.owner === owner && m.count === count && bl.addressOf(m.Item) === bl.addressOf(item);
}

function run(player) {
    const old = {
        armor: player.armor[3], inventory: player.inventory[0], mouse: Main.mouseItem,
        source: Main.mouseItemSource, index: Main.mouseItemSourceIndex, context: Main.mouseItemSourceContext,
        left: Main.mouseLeft, release: Main.mouseLeftRelease
    };
    try {
        check('arraste por LeftClick para slot vazio', () => {
            player.inventory[0] = make('DRAG', 52);
            player.armor[3] = make();
            Main.mouseItem = player.inventory[0]['Item Clone()']();
            player.inventory[0] = make();
            Main.mouseItemSource = player.inventory;
            Main.mouseItemSourceIndex = 0;
            Main.mouseItemSourceContext = 0;
            Main.mouseLeft = true; Main.mouseLeftRelease = true;
            const action = ItemSlot['int PickItemMovementAction(Item[] inv, int context, int slot, Item checkItem, bool backwardCheck, int initialSlot)'](player.armor, 10, 3, Main.mouseItem, false, -1);
            bl.log('itemequip TRACE drag action=' + action + ' pending=' + Main['bool LocalPlayerHasPendingInventoryActions()']());
            ItemSlot['void LeftClick(Item[] inv, int context, int slot)'](player.armor, 10, 3);
            bl.log('itemequip TRACE drag type=' + player.armor[3].type + ' owner=' + player.armor[3].ModItem?.owner);
            return state(player.armor[3], 'DRAG', 52);
        });
        check('troca por arraste preserva os dois donos', () => {
            Main.mouseItem = make('INCOMING', 61);
            player.armor[3] = make('EQUIPPED', 62, OtherEquipProbe);
            player.inventory[0] = make();
            Main.mouseItemSource = player.inventory;
            Main.mouseItemSourceIndex = 0; Main.mouseItemSourceContext = 0;
            Main.mouseLeft = true; Main.mouseLeftRelease = true;
            ItemSlot['void LeftClick(Item[] inv, int context, int slot)'](player.armor, 10, 3);
            bl.log('itemequip TRACE swap armor=' + player.armor[3].ModItem?.owner + ' inventory=' + player.inventory[0].ModItem?.owner + ' mouse=' + Main.mouseItem.ModItem?.owner);
            return state(player.armor[3], 'INCOMING', 61) && (state(Main.mouseItem, 'EQUIPPED', 62) || state(player.inventory[0], 'EQUIPPED', 62));
        });
        check('arraste devolve excedente ao inventario sem perder dono', () => {
            Main.mouseItem = make('REMAINDER', 81);
            Main.mouseItem.stack = 2;
            player.armor[3] = make(); player.inventory[0] = make();
            Main.mouseItemSource = player.inventory;
            Main.mouseItemSourceIndex = 0; Main.mouseItemSourceContext = 0;
            Main.mouseLeft = true; Main.mouseLeftRelease = true;
            ItemSlot['void LeftClick(Item[] inv, int context, int slot)'](player.armor, 10, 3);
            bl.log('itemequip TRACE remainder armor=' + player.armor[3].ModItem?.owner + ' inventory=' + player.inventory[0].ModItem?.owner);
            return state(player.armor[3], 'REMAINDER', 81) && state(player.inventory[0], 'REMAINDER', 81);
        });
        check('PlaceItem em slot vazio preserva estado e Clone', () => {
            const original = make('FIRST', 37);
            player.armor[3] = make();
            const held = new Ref(original);
            ItemSlot[place](new Ref(player.armor), held, 10, 3, 0);
            const equipped = player.armor[3];
            bl.log('itemequip TRACE empty source=' + original.ModItem.owner + ' destination=' + equipped.ModItem?.owner);
            if (!state(equipped, 'FIRST', 37) || equipped.ModItem === original.ModItem) return false;
            equipped.ModItem.payload.values.push(100);
            return original.ModItem.payload.values.length === 1 && held.value.stack === 0;
        });
        check('20 transferencias nao misturam donos', () => {
            for (let i = 0; i < 20; i++) {
                player.armor[3] = make();
                ItemSlot[place](new Ref(player.armor), new Ref(make('OWNER_' + i, i + 1)), 10, 3, 0);
                if (!state(player.armor[3], 'OWNER_' + i, i + 1)) return false;
            }
            return true;
        });
        check('PlaceItem com keepStack preserva ambos os estados', () => {
            const original = make('SPLIT', 91);
            original.stack = 5; original.maxStack = 99;
            player.inventory[0] = make();
            const held = new Ref(original);
            ItemSlot[place](new Ref(player.inventory), held, 0, 0, 3);
            return state(player.inventory[0], 'SPLIT', 91) && player.inventory[0].stack === 2 && held.value.stack === 3 && state(held.value, 'SPLIT', 91);
        });
        check('SetDefaults normal ainda limpa a instancia', () => {
            const original = make('RESET', 9);
            original['void SetDefaults(int Type, ItemVariant variant)'](original.type, null);
            return state(original, '', 0);
        });
    } finally {
        player.armor[3] = old.armor;
        player.inventory[0] = old.inventory;
        Main.mouseItem = old.mouse;
        Main.mouseItemSource = old.source;
        Main.mouseItemSourceIndex = old.index;
        Main.mouseItemSourceContext = old.context;
        Main.mouseLeft = old.left;
        Main.mouseLeftRelease = old.release;
    }
}

Main['void DoUpdate(GameTime gameTime)'].hook((original, self, time) => {
    original(self, time);
    if (done || Main.gameMenu || ++ticks < 120) return;
    done = true;
    try { run(Main.player[Main.myPlayer]); }
    catch (error) { failures++; bl.log('itemequip FAIL EXEC ' + error + ' ' + error.stack); }
    bl.log('itemequip FIM checks=' + checks + ' falhas=' + failures);
});

export default class EquipmentTests extends Mod {}
