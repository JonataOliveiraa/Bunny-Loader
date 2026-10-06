const Main = Terraria.Main;
const counts = {};
const failures = [];
const passOrder = [];
let ticks = 0, testingGeneration = false, testingHardmode = false, finished = false;
let testingReceive = false;
let rateStart = 0, tileSteps = 0, eventSteps = 0;
let cameraPlayerSeen = false, cameraPairs = 0;
bl.classOf('', 'ThreadManager')['void CheckThreadTerminiate()'].hook(original => {
    if (!testingGeneration) original();
});

function check(condition, name) {
    if (!condition) failures.push(name);
}

export class NativeSystem extends ModSystem {}
export class NativeCameraPlayer extends ModPlayer {
    ModifyScreenPosition(player) {
        cameraPlayerSeen = true;
    }
}
for (const name of Object.getOwnPropertyNames(ModSystem.prototype)) {
    if (name === 'constructor') continue;
    NativeSystem.prototype[name] = function (...args) {
        counts[name] = (counts[name] || 0) + 1;
        return ModSystem.prototype[name].apply(this, args);
    };
}
NativeSystem.prototype.ModifyWorldGenTasks = tasks => {
    counts.ModifyWorldGenTasks = (counts.ModifyWorldGenTasks || 0) + 1;
    if (!testingGeneration) return;
    check(tasks.length === 1 && tasks[0].Name === 'Original', 'native task list');
    tasks[0].Disable();
    tasks.unshift(new PassLegacy('Before', progress => {
        passOrder.push('before');
        progress.Message = 'ModSystem integration';
        progress['void Set(double value)'](0.5);
    }, 2));
    tasks.push(new PassLegacy('After', () => passOrder.push('after'), 3));
};
NativeSystem.prototype.PostWorldGen = () => {
    counts.PostWorldGen = (counts.PostWorldGen || 0) + 1;
    if (testingGeneration) passOrder.push('post');
};
NativeSystem.prototype.ModifyHardmodeTasks = tasks => {
    counts.ModifyHardmodeTasks = (counts.ModifyHardmodeTasks || 0) + 1;
    if (!testingHardmode) return;
    check(tasks[0].Name === 'Hardmode Conversion', 'hardmode native task');
    tasks[0].Disable();
    tasks.push(new PassLegacy('Hardmode probe', () => passOrder.push('hardmode')));
};
NativeSystem.prototype.HijackGetData = (type, reader, who) => {
    counts.HijackGetData = (counts.HijackGetData || 0) + 1;
    if (!testingReceive) return false;
    check(type.value === 250 && reader.value.BaseStream.Position === 1 && who === 256, 'native receive arguments');
    type.value = 251;
    return true;
};
NativeSystem.prototype.ModifyTimeRate = (time, tiles, events) => {
    counts.ModifyTimeRate = (counts.ModifyTimeRate || 0) + 1;
    if (ticks < 60 || ticks >= 64) return;
    time.value = 0.25;
    tiles.value = 0.5;
    events.value = 0.75;
};
NativeSystem.prototype.ModifyScreenPosition = () => {
    counts.ModifyScreenPosition = (counts.ModifyScreenPosition || 0) + 1;
    if (ticks > 0 && !finished) check(cameraPlayerSeen, 'native camera player then system');
    if (cameraPlayerSeen) cameraPairs++;
    cameraPlayerSeen = false;
};
NativeSystem.prototype.PostUpdateEverything = () => {
    counts.PostUpdateEverything = (counts.PostUpdateEverything || 0) + 1;
    if (finished || Main.gameMenu || Main.netMode !== 0) return;
    ticks++;
    if (ticks === 60) rateStart = Main.time;
    if (ticks > 60 && ticks <= 64) {
        tileSteps += Main.desiredWorldTilesUpdateRate;
        eventSteps += Main.dayRate;
        if (ticks === 64) {
            check(Math.abs(Main.time - rateStart - 1) < 0.000001, 'native fractional clock');
            check(tileSteps === 2 && eventSteps === 3, 'native independent fractional rates');
        }
    }
    if (ticks === 30) {
        try {
            testingGeneration = true;
            const building = Terraria.WorldBuilding;
            const json = bl.classOf('Newtonsoft.Json.Linq', 'JObject');
            const root = json['JObject Parse(string json)']('{"Passes":{},"Biomes":{}}');
            const config = building.WorldGenConfiguration.new();
            config['void .ctor(JObject configurationRoot)'](root);
            const generator = building.WorldGenerator.new();
            generator['void .ctor(int seed, WorldGenConfiguration configuration, GenerationProgress progress, WorldGenerator.Controller controller)'](1337, config, null, null);
            const original = new PassLegacy('Original', () => failures.push('disabled generation pass executed'));
            building.WorldGenerator['GenPassResult RunPass(GenPass pass)'].hook((run, self, pass) => {
                bl.log('MODSYSTEM_PASS ' + JSON.stringify({ name: pass.Name, enabled: pass.Enabled }));
                return run(self, pass);
            });
            Terraria.GameContent.Generation.PassLegacy['void ApplyPass(GenerationProgress progress, GameConfiguration configuration)'].hook((apply, self, progress, config) => {
                bl.log('MODSYSTEM_APPLY ' + self.Name);
                return apply(self, progress, config);
            });
            generator['void Append(GenPass pass)'](original.Native);
            const previousAbort = building.WorldGenerator.TerminateWorldGen;
            const previousMenu = Main.menuMode;
            bl.log('MODSYSTEM_GENERATION ' + JSON.stringify({ previousAbort }));
            building.WorldGenerator.TerminateWorldGen = false;
            try { check(generator['bool GenerateWorld()'](), 'native generation completed'); }
            finally {
                building.WorldGenerator.TerminateWorldGen = previousAbort;
                Main.menuMode = previousMenu;
            }
            check(passOrder.join(',') === 'before,after,post', 'native generation ordering');
            testingGeneration = false;
            testingHardmode = true;
            Terraria.WorldGen['void initializeHardMode()']();
            testingHardmode = false;
            check(passOrder.join(',') === 'before,after,post,hardmode', 'native hardmode custom pass');
            testingReceive = true;
            const buffer = Terraria.MessageBuffer.new();
            buffer['void .ctor()']();
            buffer.whoAmI = 256;
            const received = new Ref(0);
            buffer['void ProcessData(byte[] messageData, int length, out int messageType)'](System.Byte.newArray([250,1,2]),3,received);
            check(received.value === 251, 'native receive cancellation and Ref');
            testingReceive = false;
        } catch (error) {
            testingGeneration = testingHardmode = false;
            failures.push('generation exception: ' + error);
        }
    }
    if (ticks >= 240) {
        finished = true;
        const required = ['OnModLoad','OnLocalizationsLoaded','ResizeArrays','PostSetupRecipes','PreUpdateEntities','PreUpdatePlayers','PostUpdatePlayers',
            'PreUpdateNPCs','PostUpdateNPCs','PreUpdateGores','PostUpdateGores','PreUpdateProjectiles','PostUpdateProjectiles','PreUpdateItems',
            'PostUpdateItems','PreUpdateDusts','PostUpdateDusts','PreUpdateInvasions','PostUpdateInvasions','UpdateUI','PostUpdateInput',
            'ModifyTimeRate','ModifySunLightColor','ModifyLightingBrightness','ModifyScreenPosition','ModifyTransformMatrix',
            'PreWorldGen','ModifyWorldGenTasks','PostWorldGen','ModifyHardmodeTasks','HijackGetData'];
        for (const name of required) check(counts[name] > 0, 'missing ' + name);
        for (const group of ['Players','NPCs','Gores','Projectiles','Items','Dusts','Invasions']) {
            check(counts['PreUpdate' + group] === counts['PostUpdate' + group], 'paired ' + group);
        }
        check(cameraPairs > 0, 'native shared camera');
        bl.log('MODSYSTEM_RESULT ' + JSON.stringify({ ticks, counts, passOrder, tileSteps, eventSteps, cameraPairs, failures, passed: failures.length === 0 }));
    }
};

export default class SystemHookChecks extends Mod {}
