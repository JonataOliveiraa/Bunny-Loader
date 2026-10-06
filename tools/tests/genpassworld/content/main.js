import { worldName } from './config.js';

const Main = Terraria.Main;
const building = Terraria.WorldBuilding;
const threadManager = bl.classOf('', 'ThreadManager');
const events = [], failures = [], nativePasses = [];
let creatorThread = 0, generationThread = 0, managedThread = '';
let generating = false, postRuns = 0, subclassRuns = 0, disabledRuns = 0, legacyRuns = 0;
let marker = null;
let generationProof = null;

function check(condition, label) {
    if (!condition) failures.push(label);
}

function threadId() {
    return System.Threading.Thread.CurrentThread.ManagedThreadId;
}

function isTestWorld() {
    return Main.ActiveWorldFileData && Main.ActiveWorldFileData.Name === worldName;
}

function record(name, progress, configuration) {
    check(!!progress && !!configuration, name + ' native arguments');
    check(threadId() === generationThread, name + ' generation thread');
    check(!!threadManager.CurrentManagedThread, name + ' managed thread');
    progress.Message = name;
    progress['void Set(double value)'](0.5);
    events.push(name);
    bl.log('GENPASS_CALLBACK ' + JSON.stringify({ name, thread: threadId() }));
}

function verifyMarker(data) {
    if (!data) return 0;
    let matching = 0;
    for (let dx = 0; dx < data.width; dx++) {
        for (let dy = 0; dy < data.height; dy++) {
            const expected = (dx + dy) % 2 ? data.typeA : data.typeB;
            const tile = Main.tile['Tile get_Item(int x, int y)'](data.x + dx, data.y + dy);
            if (tile['bool active()']() && tile.type === expected) matching++;
        }
    }
    return matching;
}

class BeforePass extends GenPass {
    constructor() { super('BL GenPass Before', 2); }
    ApplyPass(progress, configuration) { record('genpass-before', progress, configuration); }
}

class DisabledPass extends GenPass {
    constructor() { super('BL GenPass Disabled'); }
    ApplyPass(progress, configuration) {
        disabledRuns++;
        failures.push('disabled subclass executed');
    }
}

class StructurePass extends GenPass {
    constructor() {
        super('BL GenPass Structure', 3);
        this.Weight = 5;
        this.Disable();
        this.Enable();
    }
    ApplyPass(progress, configuration) {
        subclassRuns++;
        record('genpass-structure', progress, configuration);
        marker = { x: Math.floor(Main.maxTilesX / 2) + 80, y: 100, width: 7, height: 5,
            typeA: Terraria.ID.TileID.Copper, typeB: Terraria.ID.TileID.Tin };
        for (let dx = 0; dx < marker.width; dx++) {
            for (let dy = 0; dy < marker.height; dy++) {
                const tile = Main.tile['Tile get_Item(int x, int y)'](marker.x + dx, marker.y + dy);
                tile['void ResetToType(ushort type)']((dx + dy) % 2 ? marker.typeA : marker.typeB);
            }
        }
        for (let dx = 0; dx < marker.width; dx++) {
            for (let dy = 0; dy < marker.height; dy++) {
                Terraria.WorldGen['void SquareTileFrame(int i, int j, bool resetFrame)'](marker.x + dx, marker.y + dy, true);
            }
        }
        check(verifyMarker(marker) === 35, 'subclass placed 35 tiles');
        progress['void Set(double value)'](1);
    }
}

bl.classOf('', 'GUIWorldCreateMenu')['void CreateWorld()'].hook((original, self) => {
    creatorThread = threadId();
    Main.newWorldName = worldName;
    self._worldName = worldName;
    self._worldSeed = '73377337';
    self.worldSize = 0;
    self.difficulty = 0;
    self.evilType = 0;
    bl.log('GENPASS_CREATE ' + JSON.stringify({ worldName, creatorThread }));
    return original(self);
});

building.WorldGenerator['GenPassResult RunPass(GenPass pass)'].hook((original, self, pass) => {
    const result = original(self, pass);
    if (generating && !pass.Name.startsWith('BL ')) {
        nativePasses.push({ name: pass.Name, skipped: result.Skipped });
        if (nativePasses.length % 10 === 0) bl.log('GENPASS_PROGRESS ' + JSON.stringify({ count: nativePasses.length, name: pass.Name }));
    }
    return result;
});

Terraria.IO.WorldFile['void InternalSaveWorld(bool useCloudSaving, bool resetTime)'].hook((original, cloud, reset) => {
    original(cloud, reset);
    if (!isTestWorld()) return;
    const valid = Terraria.IO.WorldFile['bool IsValidWorld(string file, bool cloudSave)'](Main.worldPathName, false);
    check(valid, 'saved world valid');
    bl.log('GENPASS_SAVED ' + JSON.stringify({ valid, path: Main.worldPathName, matchingTiles: verifyMarker(marker), failures }));
});

export class FullWorldSystem extends ModSystem {
    PreWorldGen() {
        if (!isTestWorld()) return;
        generating = true;
        generationThread = threadId();
        const managed = threadManager.CurrentManagedThread;
        managedThread = managed ? managed.Id : '';
        check(generationThread !== creatorThread && creatorThread > 0, 'normal worker thread');
        check(managedThread === 'worldGenCallback', 'normal managed world generation');
        events.push('pre');
        bl.log('GENPASS_PRE ' + JSON.stringify({ generationThread, managedThread }));
    }
    ModifyWorldGenTasks(tasks) {
        if (!isTestWorld()) return;
        check(tasks.length > 50, 'full native generation task list');
        check(tasks.every(pass => pass instanceof GenPass), 'native stages use GenPass');
        const before = new BeforePass(), disabled = new DisabledPass(), structure = new StructurePass();
        disabled.Disable();
        check(structure.Enabled && structure.Weight === 5 && !disabled.Enabled, 'native pass properties');
        tasks.unshift(before, disabled);
        tasks.push(structure, new PassLegacy('BL PassLegacy After', (progress, configuration) => {
            legacyRuns++;
            record('passlegacy-after', progress, configuration);
            check(verifyMarker(marker) === 35, 'legacy sees subclass tiles');
        }, 4));
        bl.log('GENPASS_TASKS ' + JSON.stringify({ count: tasks.length, names: tasks.map(pass => pass.Name) }));
    }
    PostWorldGen() {
        if (!isTestWorld()) return;
        postRuns++;
        events.push('post');
        check(events.join(',') === 'pre,genpass-before,genpass-structure,passlegacy-after,post', 'custom stages ordering');
        check(subclassRuns === 1 && legacyRuns === 1 && disabledRuns === 0 && postRuns === 1, 'exact callback counts');
        check(nativePasses.filter(pass => !pass.skipped).length > 50, 'native terrain stages completed');
        check(verifyMarker(marker) === 35, 'post generation structure');
        generating = false;
        bl.log('GENPASS_GENERATED ' + JSON.stringify({ worldName, generationThread, creatorThread, managedThread,
            size: [Main.maxTilesX, Main.maxTilesY], nativePasses: nativePasses.length,
            nativeExecuted: nativePasses.filter(pass => !pass.skipped).length, events, subclassRuns, legacyRuns, disabledRuns,
            matchingTiles: verifyMarker(marker), failures, passed: failures.length === 0 }));
    }
    SaveWorldData(tag) {
        if (!isTestWorld() || !marker) return;
        if (!generationProof) generationProof = { worldName, marker, events: events.slice(), subclassRuns,
            legacyRuns, disabledRuns, failures: failures.slice() };
        tag.Set('proof', generationProof);
    }
    LoadWorldData(tag) {
        if (!isTestWorld()) return;
        const proof = tag.Get('proof', null);
        check(!!proof, 'saved generation proof');
        if (proof) {
            generationProof = proof;
            marker = proof.marker;
            check(proof.failures.length === 0 && proof.subclassRuns === 1 && proof.legacyRuns === 1 && proof.disabledRuns === 0, 'saved callback proof');
            check(verifyMarker(marker) === 35, 'persisted structure after reload');
        }
        bl.log('GENPASS_RELOADED ' + JSON.stringify({ worldName, matchingTiles: verifyMarker(marker),
            generationCallbacksThisProcess: subclassRuns + legacyRuns, failures, passed: failures.length === 0 }));
    }
}

export default class FullGenerationChecks extends Mod {}
