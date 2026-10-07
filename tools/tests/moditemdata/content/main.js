import { initMultiplayer, handleMultiplayer, tickMultiplayer } from './multiplayer.js';
import { role } from './config.js';
const Main = Terraria.Main;
const defaults = 'void SetDefaults(int Type, ItemVariant variant)';
let frames = 0,
    done = false,
    checks = 0,
    failures = 0,
    saves = 0,
    loads = 0,
    failSave = false,
    failLoad = false;

function finish() {
    bl.log('moditemdata FIM role=' + role + ' checks=' + checks + ' falhas=' + failures + ' saves=' + saves + ' loads=' + loads);
}

function check(name, fn) {
    checks++;

    try {
        if (!fn()) throw Error('resultado falso');
        bl.log('moditemdata PASS ' + name);
    }
    catch (error) {
        failures++;
        bl.log('moditemdata FAIL ' + name + ': ' + error + ' ' + error.stack);
    }
}

export class PersistentProbe extends ModItem {
    Texture = 'Box';
    HideFromModMenu = true;
    owner = '';
    count = 0;
    payload = null;

    SetDefaults(item) {
        item.width = item.height = 16;
        item.maxStack = 1;
        this.owner = '';
        this.count = 0;
        this.payload = null;
    }

    SaveData(tag) {
        saves++;
        if (failSave) throw Error('EXPECTED SaveData failure');
        tag.Set('owner', this.owner);
        tag.Set('count', this.count);
        tag.Set('payload', this.payload);
    }

    LoadData(tag) {
        loads++;
        if (failLoad) throw Error('EXPECTED LoadData failure');
        this.owner = tag.GetString('owner');
        this.count = tag.GetInt('count');
        this.payload = tag.Get('payload', null);
    }

    Clone(item) {
        const copy = super.Clone(item);
        copy.payload = this.payload === null ? null : JSON.parse(JSON.stringify(this.payload));
        return copy;
    }
}

export class InheritedProbe extends PersistentProbe {}

export class EmptyProbe extends ModItem {
    Texture = 'Box';
    HideFromModMenu = true;

    SaveData(tag) {}

    LoadData(tag) {
        this.emptyLoaded = tag.Count === 0;
    }
}

function item(cls = PersistentProbe, owner = 'Jogador A', count = 17) {
    const i = Terraria.Item.new();
    i['void .ctor()']();
    i[defaults](ModContent.ItemType(cls), null);
    if (i.ModItem instanceof PersistentProbe) {
        i.ModItem.owner = owner;
        i.ModItem.count = count;
        i.ModItem.payload = { flags: [true, false, null], text: 'João 🎮\t\n', value: 0.125 };
    }
    return i;
}

function places(player) {
    const list = ['inventory', 'armor', 'dye', 'miscEquips', 'miscDyes'].map(name => [name, player[name]]);
    for (const name of ['bank', 'bank2', 'bank3', 'bank4']) list.push([name, player[name].item]);
    for (let i = 0; i < player.Loadouts.length; ++i) {
        list.push(['loadout' + i + '.armor', player.Loadouts[i].Armor], ['loadout' + i + '.dye', player.Loadouts[i].Dye]);
    }
    return list;
}

function run(player) {
    const a = item(),
        b = item(PersistentProbe, 'Jogador B', 99);
    check('instancias independentes', () => a.ModItem.owner === 'Jogador A' && b.ModItem.owner === 'Jogador B');
    for (const signature of ['Item Clone()', 'Item DeepClone()', 'Item clientClone(Item cloneDestination)']) {
        check(signature, () => {
            const copy = signature.includes('clientClone') ? a[signature](item()) : a[signature]();
            if (copy.ModItem.owner !== a.ModItem.owner || copy.ModItem.count !== 17) return false;
            copy.ModItem.payload.flags.push(1);
            return a.ModItem.payload.flags.length === 3 && bl.addressOf(copy.ModItem.Item) === bl.addressOf(copy);
        });
    }
    const chest = Terraria.Chest['Chest CreateOutOfArray(int index, int x, int y, int maxItems)'](-1, 0, 0, 40);
    check('ChestItem.SetToItem -> ExpandItem', () => {
        chest.item[0]['void SetToItem(Item item)'](a);
        const expanded = chest.item[0]['Item ExpandItem()']();
        return expanded.ModItem.owner === 'Jogador A' && expanded.ModItem.count === 17 && expanded.ModItem.payload.text === a.ModItem.payload.text;
    });
    check('Chest.Resize preserva dados', () => {
        chest['void Resize(int newSize)'](80);
        return chest.item[0]['Item ExpandItem()']().ModItem.owner === 'Jogador A';
    });
    check('Chest.CloneWithSeparateItems preserva dados', () => {
        const copy = chest['Chest CloneWithSeparateItems()']();
        return copy.item[0]['Item ExpandItem()']().ModItem.owner === 'Jogador A';
    });
    check('limpeza de slot nao reutiliza dono', () => {
        chest.item[0]['void Clear()']();
        chest.item[0]['void SetDefaults(int id)'](a.type);
        return chest.item[0]['Item ExpandItem()']().ModItem.owner === '';
    });
    check('empty LoadData runs during native chest expansion', () => {
        chest.item[1]['void SetToItem(Item item)'](item(EmptyProbe));
        return chest.item[1]['Item ExpandItem()']().ModItem.emptyLoaded === true;
    });
    const selected = player.selectedItemState.selected,
        previous = player.inventory[0],
        oldMouse = Main.mouseItem;

    try {
        Main.mouseItem = Terraria.Item.new();
        Main.mouseItem['void .ctor()']();
        player.selectedItemState.selected = 0;
        player.inventory[0] = a;
        check('Player.DropSelectedItem preserva dados', () => {
            player['void DropSelectedItem()']();
            for (let n = 0; n < Main.item.length; n++) {
                const world = Main.item[n];
                if (world.active && world.inner.type === a.type && world.inner.ModItem.owner === 'Jogador A') {
                    const picked = world.inner['Item Clone()']();
                    world['void TurnToAir()']();
                    return picked.ModItem.count === 17 && picked.ModItem.owner === 'Jogador A';
                }
            }
            return false;
        });
    }
    finally {
        player.inventory[0] = previous;
        player.selectedItemState.selected = selected;
        Main.mouseItem = oldMouse;
    }
    check('GUIPageIcons.DropUIItem preserves explicit source data', () => {
        const value = item(PersistentProbe, 'UI_OWNER', 902);
        GUIPageIcons['void DropUIItem(Player player, Item item, int additionalVelocity)'](player, value, 0);
        for (let n = 0; n < Main.item.length; ++n) {
            const world = Main.item[n];
            if (world.active && world.inner.type === value.type && world.inner.ModItem.count === 902) {
                const good = world.inner.ModItem.owner === 'UI_OWNER';
                world['void TurnToAir()']();
                return good;
            }
        }
        return false;
    });
    // Save to a separate file, restoring the live slots before the next frame.
    // The runner already boots a copy of the player's save and world.
    const expected = places(player),
        originals = expected.map(entry => entry[1][0]);
    const inventory1 = player.inventory[1],
        inventory2 = player.inventory[2];

    try {
        for (let i = 0; i < expected.length; ++i) expected[i][1][0] = item(i % 2 ? InheritedProbe : PersistentProbe, expected[i][0], i + 100);
        player.inventory[1] = item(EmptyProbe);
        player.inventory[2] = item(PersistentProbe, 'large', 123);
        player.inventory[2].ModItem.payload = { text: 'x'.repeat(16000) };
        const path = Main.ActivePlayerFileData.Path.replace(/[^/]+$/, 'BL_ItemData_Probe.plr');
        const file = Terraria.IO.PlayerFileData.new();
        file['void .ctor(string path, bool cloudSave)'](path, false);
        file.Player = player;
        file.Metadata = Terraria.IO.FileMetadata['FileMetadata FromCurrentSettings(FileType type)'](3);
        bl.log('moditemdata STAGE native-save');
        check('native SaveData callbacks are called', () => {
            const before = saves;
            Terraria.Player['void SavePlayer(PlayerFileData playerFile, bool skipMapSave, bool forceSave)'](file, true, true);
            return saves - before >= expected.length + 1;
        });
        let restored;
        bl.log('moditemdata STAGE native-load');
        check('native LoadData callbacks are called', () => {
            const before = loads;
            restored = Terraria.Player['PlayerFileData LoadPlayer(string playerPath, bool cloudSave)'](path, false).Player;
            return loads - before >= expected.length + 1;
        });
        if (restored) {
            for (const [name, array] of places(restored)) check('save/load ' + name, () => array[0].ModItem.owner === name && array[0].ModItem.count === expected.findIndex(e => e[0] === name) + 100);
            check('empty tag on native reload', () => restored.inventory[1].ModItem.emptyLoaded === true);
            check('payload beyond old 1024-byte limit', () => restored.inventory[2].ModItem.payload.text.length === 16000);
            check('SaveData failure preserves previous native sidecar', () => {
                failSave = true;
                player.inventory[0].ModItem.owner = 'MUST_NOT_OVERWRITE';

                try {
                    Terraria.Player['void SavePlayer(PlayerFileData playerFile, bool skipMapSave, bool forceSave)'](file, true, true);
                }
                finally {
                    failSave = false;
                }
                return Terraria.Player['PlayerFileData LoadPlayer(string playerPath, bool cloudSave)'](path, false).Player.inventory[0].ModItem.owner === 'inventory';
            });
            let failedPlayer;
            check('LoadData failure is isolated and flagged', () => {
                failLoad = true;

                try {
                    failedPlayer = Terraria.Player['PlayerFileData LoadPlayer(string playerPath, bool cloudSave)'](path, false).Player;
                }
                finally {
                    failLoad = false;
                }
                return failedPlayer.inventory[0].__blItemDataLoadFailed === true && failedPlayer.inventory[1].ModItem.emptyLoaded;
            });
            check('failed native LoadData cannot overwrite previous data', () => {
                file.Player = failedPlayer;
                failedPlayer.inventory[0].ModItem.owner = 'MUST_NOT_OVERWRITE';

                try {
                    Terraria.Player['void SavePlayer(PlayerFileData playerFile, bool skipMapSave, bool forceSave)'](file, true, true);
                }
                finally {
                    file.Player = player;
                }
                const recovered = Terraria.Player['PlayerFileData LoadPlayer(string playerPath, bool cloudSave)'](path, false).Player;
                return recovered.inventory[0].ModItem.owner === 'inventory' && !recovered.inventory[0].__blItemDataLoadFailed;
            });
        }
    }
    finally {
        expected.forEach((entry, i) => {
            entry[1][0] = originals[i];
        });
        player.inventory[1] = inventory1;
        player.inventory[2] = inventory2;
    }
    const worldChest = Array.from({ length: Main.chest.length }, (_, i) => Main.chest[i]).find(Boolean);
    check('native world chest exists in test copy', () => !!worldChest);
    if (worldChest) {
        const old = worldChest.item[39]['Item ExpandItem()']();
        const stream = System.IO.MemoryStream.new();
        stream['void .ctor()']();
        const writer = System.IO.BinaryWriter.new();
        writer['void .ctor(Stream output)'](stream);

        try {
            worldChest.item[39]['void SetToItem(Item item)'](item(InheritedProbe, 'WORLD_CHEST', 901));
            check('native world SaveChests -> FixAgainstExploits restores custom fields', () => {
                Terraria.IO.WorldFile['int SaveChests(BinaryWriter writer)'](writer);
                worldChest.item[39]['void Clear()']();
                Terraria.IO.WorldFile['void FixAgainstExploits()']();
                const reloaded = worldChest.item[39]['Item ExpandItem()']();
                return reloaded.ModItem.owner === 'WORLD_CHEST' && reloaded.ModItem.count === 901 && reloaded.ModItem.payload.flags.length === 3;
            });
        }
        finally {
            worldChest.item[39]['void SetToItem(Item item)'](old);
            Terraria.IO.WorldFile['int SaveChests(BinaryWriter writer)'](writer);
            writer['void Close()']();
            stream['void Close()']();
        }
    }
}
Terraria.Player['void Update(int i)'].hook((original, player, index) => {
    original(player, index);
    if (Main.gameMenu || index !== Main.myPlayer) return;
    if (done) return;
    if (++frames !== 60) return;
    done = true;

    try {
        if (role === 'native') run(player);
    }
    catch (error) {
        failures++;
        bl.log('moditemdata FAIL EXEC ' + error + ' ' + error.stack);
    }
    if (role === 'native') finish();
});
Terraria.Main['void DoUpdate_HandleInput()'].hook((original, self) => {
    original(self);
    if (done && role !== 'native') tickMultiplayer(Main.player[Main.myPlayer]);
});

export default class ItemDataTests extends Mod {

    Load() {
        initMultiplayer(this, item, check, finish);
    }

    HandlePacket(reader, from) {
        handleMultiplayer(reader, from);
    }
}
