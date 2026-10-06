import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const source = path.join(root, 'app/src/main/cpp/script/js/mod');
const dump = fs.readFileSync(path.join(root, 'refs/dump.cs'), 'utf8');
const methods = new Map();
const parameterNames = new Map();
let namespace = '', owner = '';
function parts(text) {
    const result = [], start = [];
    let depth = 0;
    for (const c of text) {
        if (c === ',' && depth === 0) { result.push(start.join('').trim()); start.length = 0; continue; }
        start.push(c);
        if (c === '<' || c === '[') depth++;
        if (c === '>' || c === ']') depth--;
    }
    if (start.length) result.push(start.join('').trim());
    return result;
}
function type(value) {
    return value.replace(/<.*>/g, '').replace(/`\d/g, '').replace(/.*\./g, '');
}
function signature(value) {
    const match = value.match(/(\S+)\s+(\.?\w+)\((.*)\)/);
    if (!match) throw Error('Invalid signature: ' + value);
    const args = parts(match[3]).map((p) => {
        p = p.split('=')[0].trim();
        const words = p.split(/\s+/);
        return (words[0] === 'ref' || words[0] === 'out' ? '&' + type(words[1]) : type(words[0]));
    });
    return type(match[1]) + ' ' + match[2] + '(' + args.join(',') + ')';
}
function names(value) { return parts(value.match(/\((.*)\)/)[1]).map((parameter) => parameter.split('=')[0].trim().split(/\s+/).at(-1)); }
for (const line of dump.split(/\r?\n/)) {
    if (line.startsWith('// Namespace:')) namespace = line.slice(13).trim();
    const cls = line.match(/^(?:public|private|internal|protected).*?\b(?:class|struct)\s+([^\s:<]+)/);
    if (cls) { owner = (namespace ? namespace + '.' : '') + cls[1]; if (!methods.has(owner)) methods.set(owner, new Set()); }
    if (/^\s+(?:public|private|internal|protected).*?\([^)]*\).*\{ \}/.test(line) && owner) {
        try { methods.get(owner).add(signature(line)); parameterNames.set(owner + ':' + signature(line), names(line)); } catch {}
    }
}
const failures = [], installed = new Map(), stages = new Map(), natives = new Map(), warnings = [];
let address = 1;
function native(name) {
    if (natives.has(name)) return natives.get(name);
    const fields = {};
    const value = new Proxy(fields, {
        get(target, key) {
            if (key in target) return target[key];
            if (typeof key !== 'string') return undefined;
            if (key.includes('(')) {
                const normalized = signature(key), id = name + ':' + normalized;
                if (!installed.has(id)) installed.set(id, { name, signature: key, callbacks: [], filters: [], active: 0, vanilla: () => undefined });
                const entry = installed.get(id);
                const available = methods.get(name);
                if (available && !available.has(normalized) && !(name === 'Terraria.GameContent.Generation.PassLegacy' && methods.get('Terraria.WorldBuilding.GenPass')?.has(normalized))) failures.push(name + ' ' + key);
                const fn = (...args) => invoke(entry, args);
                fn.entry = entry;
                fn.hook = (callback, filter = {}) => {
                    const known = methods.get(name) || methods.get(name.replace(/^Terraria\.(Player\.)/, '$1'));
                    if (!known?.has(normalized)) failures.push(name + ' ' + key);
                    const expected = parameterNames.get(id) || parameterNames.get(name.replace(/^Terraria\.(Player\.)/, '$1') + ':' + normalized);
                    if (expected && JSON.stringify(expected) !== JSON.stringify(names(key))) failures.push(name + ' ' + key + ' => ' + expected.join(', '));
                    entry.callbacks.push(callback);
                    entry.filters.push(filter);
                };
                return fn;
            }
            if (key === 'new') return () => instance(name);
            if (key === 'newArray') return values => typeof values === 'number' ? new Array(values).fill(0) : Array.from(values);
            return native(name + '.' + key);
        },
    });
    natives.set(name, value);
    return value;
}
function invoke(entry, args, index = 0) {
    if (index >= entry.callbacks.length) return entry.vanilla(...args);
    const filter = entry.filters[index];
    if (filter.flag && !flags.get(filter.flag) || filter.whileIn && !filter.whileIn.entry.active) return invoke(entry, args, index + 1);
    entry.active++;
    try { return entry.callbacks[index]((...next) => invoke(entry, next, index + 1), ...args); }
    finally { entry.active--; }
}
function method(owner, name) {
    const matches = [...installed.values()].filter((entry) => entry.name === owner && entry.signature.split('(')[0].endsWith(' ' + name));
    assert.equal(matches.length, 1, owner + '.' + name + ' must resolve uniquely');
    return matches[0];
}
function call(owner, name, ...args) { return invoke(method(owner, name), args); }
function vanilla(owner, name, fn) { method(owner, name).vanilla = fn; }
function instance(name) {
    const target = { __address: address++ };
    return new Proxy(target, { get(obj, key) {
        if (key in obj) return obj[key];
        if (typeof key === 'string' && key.includes('(')) return (...args) => native(name)[key](obj, ...args);
        return undefined;
    }});
}
const flags = new Map();
const Terraria = native('Terraria'), System = native('System'), Microsoft = native('Microsoft');
const Main = Terraria.Main;
Object.assign(Main, { gameMenu: false, netMode: 0, dayRate: 1, desiredWorldTilesUpdateRate: 1, player: [], worldPathName: '/test.wld',
    ActivePlayerFileData: { Name: 'Player' }, ActiveWorldFileData: { Name: 'World', Path: '/test.wld' }, GameViewMatrix: {} });
const files = new Map(), errors = [], events = [], tasks = [], definitions = new Map();
let contentReady;
const sandbox = { Terraria, System, Microsoft, assert,
    Ref: class { constructor(value) { this.value = value; } },
    bl: { mod: { uuid: 'systems' }, log() {}, error: error => errors.push(String(error)),
        classOf: (ns, cls) => native((ns ? ns + '.' : '') + cls),
        defineMethod: (cls, name, method) => definitions.set(name, method),
        installSystemStage: (name, callback) => stages.set(name, callback),
        onContentReady: fn => { contentReady = fn; },
        file: { read: name => files.get(name), write: (name, data) => files.set(name, data), delete: name => files.delete(name) }
    },
    Templates: { Adopt(cls, inst) { inst.Mod = sandbox.bl.mod; } },
    LocalizationLoader: { WantLoaded(fn) { tasks.push(fn); } },
    BestiaryLoader: { Finish: () => events.push('bestiary') }, RecipeLoader: { Finish: () => events.push('recipe-finish') },
    ModNet: { InstallEntity() { events.push('entity-net'); } },
};
const context = vm.createContext(sandbox);
for (const file of ['Core/Safe.js', 'Core/Hooks.js', 'Core/Ready.js', 'TagCompound.js', 'GenPass.js', 'Loaders/PlayerDrawHooks.js', 'Loaders/SystemUpdateHooks.js',
    'Loaders/SystemDrawHooks.js', 'Loaders/SystemWorldHooks.js', 'Loaders/SystemNetworkHooks.js', 'Loaders/SystemLoader.js', 'ModSystem.js']) {
    vm.runInContext(fs.readFileSync(path.join(source, file), 'utf8'), context, { filename: file });
}
vm.runInContext(`
    class All extends ModSystem {}
    for (const name of Object.getOwnPropertyNames(ModSystem.prototype)) {
        if (name === 'constructor') continue;
        All.prototype[name] = function (...args) { return ModSystem.prototype[name].apply(this, args); };
    }
    const all = ModSystem.register(All);
    globalThis.api = { ModSystem, All, all, SystemLoader, SystemWorldHooks, SystemNetworkHooks, GenPass, PassLegacy, Ready };
`, context);
assert.deepEqual(failures, [], 'Native signatures and parameter names');
const { All, all, ModSystem, SystemLoader, SystemWorldHooks, SystemNetworkHooks, GenPass, PassLegacy, Ready } = sandbox.api;
let checks = 0;
function test(name, fn) { fn(); checks++; process.stdout.write('PASS ' + name + '\n'); }
function pair(owner, name, pre, post, args = []) {
    const order = [];
    All.prototype[pre] = () => order.push('before');
    All.prototype[post] = () => order.push('after');
    vanilla(owner, name, () => order.push('native'));
    call(owner, name, ...args);
    assert.deepEqual(order, ['before', 'native', 'after']);
}
test('No native hooks for empty system', () => {
    const count = [...installed.values()].reduce((sum, entry) => sum + entry.callbacks.length, 0);
    class Empty extends ModSystem {}
    ModSystem.register(Empty);
    assert.equal([...installed.values()].reduce((sum, entry) => sum + entry.callbacks.length, 0), count);
});
test('Cached listeners include only overrides', () => {
    assert.equal(SystemLoader.Entries('ModifyTimeRate').length, 1);
    assert.equal(SystemLoader.Entries('Nonexistent').length, 0);
});
test('Update groups native edges', () => {
    const mapping = { PlayersBegin: ['PreUpdatePlayers'], PlayersEnd: ['PostUpdatePlayers'], NPCsBegin: ['PreUpdateNPCs'],
        NPCsEnd: ['PostUpdateNPCs','PreUpdateGores'], GoresEnd: ['PostUpdateGores'], ItemsBegin: ['PostUpdateProjectiles','PreUpdateItems'], ItemsEnd: ['PostUpdateItems'] };
    for (const [edge, methods] of Object.entries(mapping)) {
        const order=[];
        for (const name of methods) All.prototype[name]=()=>order.push(name);
        stages.get(edge)();
        assert.deepEqual(order, methods);
    }
});
for (const [owner, name, pre, post, args] of [
    ['Terraria.Main','DoUpdateInWorld','PreUpdateEntities','PostUpdateEverything',[{}]],
    ['Terraria.Dust','UpdateDust','PreUpdateDusts','PostUpdateDusts',[]],
    ['Terraria.Main','UpdateInvasion','PreUpdateInvasions','PostUpdateInvasions',[]],
    ['Terraria.Main','UpdateTime','PreUpdateTime','PostUpdateTime',[]],
    ['Terraria.WorldGen','UpdateWorld','PreUpdateWorld','PostUpdateWorld',[]],
]) test(name + ' before and after', () => pair(owner,name,pre,post,args));
test('UI paused world and input', () => {
    const order=[];
    All.prototype.UpdateUI = time => order.push(time);
    All.prototype.PostUpdateInput = () => order.push('input');
    call('Terraria.Main','UpdateUIStates', 12);
    Main.gameMenu=true;
    call('Terraria.Main','UpdateUIStates', 24);
    call('Terraria.Main','DoUpdate_HandleInput',{});
    Main.gameMenu=false;
    assert.deepEqual(order,[12,'input']);
});
test('Three independent rates and fractional accumulation', () => {
    All.prototype.ModifyTimeRate = (time,tiles,events) => { time.value=0.25; tiles.value=0.5; events.value=0.75; };
    vanilla('Terraria.Main','UpdateTimeRate',()=>{ Main.dayRate=1; Main.desiredWorldTilesUpdateRate=1; });
    const tiles=[], events=[];
    for(let i=0;i<4;i++) { call('Terraria.Main','UpdateTimeRate'); tiles.push(Main.desiredWorldTilesUpdateRate); events.push(Main.dayRate); assert.equal(stages.get('ClockAdvance')(),0.25); }
    assert.deepEqual(tiles,[0,1,0,1]); assert.deepEqual(events,[0,1,1,1]);
});
test('Invalid rates fail back to vanilla', () => {
    All.prototype.ModifyTimeRate = (time,tiles,events) => { time.value=NaN; tiles.value=-1; events.value=Infinity; };
    call('Terraria.Main','UpdateTimeRate'); assert.equal(stages.get('ClockAdvance')(),1); assert.equal(Main.dayRate,1); assert.equal(Main.desiredWorldTilesUpdateRate,1);
});
test('Sunlight Ref replacements', () => {
    All.prototype.ModifySunLightColor=(tiles,sky)=>{ tiles.value='tile'; sky.value='sky'; };
    call('Terraria.Main','ApplyColorOfTheSkiesToTiles'); assert.equal(Main.tileColor,'tile'); assert.equal(Main.ColorOfTheSkies,'sky');
});
test('Brightness and invalid value', () => {
    Terraria.Lighting.GlobalBrightness=1;
    All.prototype.ModifyLightingBrightness=scale=>scale.value=1.03;
    call('Terraria.Lighting','UpdateGlobalBrightness'); assert.equal(Terraria.Lighting.GlobalBrightness,1.03);
    All.prototype.ModifyLightingBrightness=scale=>scale.value=NaN;
    call('Terraria.Lighting','UpdateGlobalBrightness'); assert.equal(Terraria.Lighting.GlobalBrightness,1.03);
});
test('Camera and transform skip menus', () => {
    let count=0;
    All.prototype.ModifyScreenPosition=()=>count++;
    All.prototype.ModifyTransformMatrix=matrix=>matrix.value={ Zoom: 2 };
    call('Terraria.Main','DoDraw_UpdateCameraPosition'); stages.get('Transform')();
    assert.equal(Main.GameViewMatrix.Zoom,2);
    Main.gameMenu=true; call('Terraria.Main','DoDraw_UpdateCameraPosition'); stages.get('Transform')(); Main.gameMenu=false;
    assert.equal(count,1);
});
test('Unsupported Unity screen capture is absent', () => { assert.equal(typeof ModSystem.prototype.RequiresScreenTarget, 'undefined'); });

test('Shared camera player then system in both registration orders', () => {
    for (const playerFirst of [true, false]) {
        const order=[], callbacks=[];
        const main={gameMenu:false,player:[{}],myPlayer:0,
            'void DoDraw_UpdateCameraPosition()': {hook:fn=>callbacks.push(fn)}};
        const isolated=vm.createContext({Terraria:{Main:main},
            PlayerLoader:{Call:()=>order.push('player')},SystemLoader:{Call:()=>order.push('system')}});
        for (const file of ['Core/Hooks.js','Loaders/PlayerDrawHooks.js']) {
            vm.runInContext(fs.readFileSync(path.join(source,file),'utf8'),isolated);
        }
        vm.runInContext(`PlayerDrawHooks.InstallCamera(${playerFirst}); PlayerDrawHooks.InstallCamera(${!playerFirst});`,isolated);
        assert.equal(callbacks.length,1);
        callbacks[0](()=>order.push('native'));
        assert.deepEqual(order,['native','player','system']);
        order.length=0; main.gameMenu=true; callbacks[0](()=>order.push('native'));
        assert.deepEqual(order,['native']);
    }
});
test('Recipes order and resize', () => {
    for(const name of ['ResizeArrays','AddRecipeGroups','PostSetupContent','AddRecipes','PostAddRecipes','PostSetupRecipes']) All.prototype[name]=()=>events.push(name);
    events.length=0; contentReady();
    assert.deepEqual(events,['ResizeArrays','AddRecipeGroups','PostSetupContent','AddRecipes','PostAddRecipes','bestiary','recipe-finish','PostSetupRecipes']);
});
function initializePasses() {
    const ctor=native('Terraria.GameContent.Generation.PassLegacy')['void .ctor(string name, WorldGenLegacyMethod method, double weight)'];
    ctor.entry.vanilla=(pass,name,method,weight)=>Object.assign(pass,{Name:name,Weight:weight,Enabled:true});
    native('Terraria.WorldBuilding.GenPass')['void Disable()'].entry.vanilla=pass=>pass.Enabled=false;
    native('Terraria.WorldBuilding.GenPass')['void Enable()'].entry.vanilla=pass=>pass.Enabled=true;
    native('Terraria.WorldBuilding.GenPass')['void Apply(GenerationProgress progress, GameConfiguration configuration)'].entry.vanilla=(pass,p,c)=> {
        if(pass.Enabled) native('Terraria.GameContent.Generation.PassLegacy')['void ApplyPass(GenerationProgress progress, GameConfiguration configuration)'](pass,p,c);
    };
    native('Terraria.GameContent.Generation.PassLegacy')['void Disable()'].entry.vanilla=pass=>pass.Enabled=false;
    native('Terraria.GameContent.Generation.PassLegacy')['void Enable()'].entry.vanilla=pass=>pass.Enabled=true;
}
initializePasses();
test('GenPass validates names callbacks and weights', () => {
    assert.throws(()=>new PassLegacy('',()=>{})); assert.throws(()=>new PassLegacy('Bad',null)); assert.throws(()=>new PassLegacy('Bad',()=>{},-1));
    const pass=new PassLegacy('Custom',()=>{},2); assert.equal(pass.Name,'Custom'); assert.equal(pass.Weight,2); pass.Disable(); assert.equal(pass.Enabled,false); pass.Enable(); assert.equal(pass.Enabled,true);
});

test('Direct GenPass subclass runs through native ApplyPass with instance state', () => {
    class Custom extends GenPass {
        constructor() { super('Subclass',2); this.calls=0; }
        ApplyPass(progress,config) { this.calls++; this.progress=progress; this.config=config; }
    }
    const pass=new Custom(), progress={}, config={};
    const apply=native('Terraria.GameContent.Generation.PassLegacy')['void ApplyPass(GenerationProgress progress, GameConfiguration configuration)'];
    pass.Disable(); apply(pass.Native,progress,config); assert.equal(pass.calls,0);
    pass.Enable(); apply(pass.Native,progress,config);
    assert.equal(pass.calls,1); assert.equal(pass.progress,progress); assert.equal(pass.config,config);
    pass.Weight=5; assert.equal(pass.Native.Weight,5); assert.throws(()=>pass.Weight=NaN);
    assert.equal(pass.Weight,5);
});

test('A failing GenPass subclass does not prevent the next legacy pass', () => {
    class Failure extends GenPass { ApplyPass() { throw Error('subclass fixture'); } }
    let calls=0;
    const bad=new Failure('Fail'), next=new PassLegacy('Next',()=>calls++), count=errors.length;
    const apply=native('Terraria.GameContent.Generation.PassLegacy')['void ApplyPass(GenerationProgress progress, GameConfiguration configuration)'];
    apply(bad.Native,{},{}); apply(next.Native,{},{});
    assert.equal(calls,1); assert.equal(errors.length,count+1);
});
test('World passes insert reorder disable and complete', () => {
    const order=[];
    const p=new PassLegacy('Native',()=>order.push('native'),2);
    const generator={_passes:{_items:[p.Native],_size:1,_version:0}};
    All.prototype.PreWorldGen=()=>order.push('pre');
    All.prototype.ModifyWorldGenTasks=tasks=> { tasks[0].Disable(); tasks.unshift(new PassLegacy('Before',()=>order.push('before'),3)); tasks.push(new PassLegacy('After',()=>order.push('after'),4)); };
    All.prototype.PostWorldGen=()=>order.push('post');
    vanilla('Terraria.WorldBuilding.WorldGenerator','GenerateWorld',self=>{
        for(const pass of self._passes._items) native('Terraria.GameContent.Generation.PassLegacy')['void ApplyPass(GenerationProgress progress, GameConfiguration configuration)'](pass,{},{ });
        return true;
    });
    assert.equal(call('Terraria.WorldBuilding.WorldGenerator','GenerateWorld',generator),true);
    assert.deepEqual(order,['pre','before','after','post']); assert.equal(generator._passes._size,3); assert.equal(generator._passes._version,1);
});
test('Aborted world has no PostWorldGen', () => {
    let called=false; All.prototype.ModifyWorldGenTasks=()=>{}; All.prototype.PostWorldGen=()=>called=true;
    vanilla('Terraria.WorldBuilding.WorldGenerator','GenerateWorld',()=>false);
    call('Terraria.WorldBuilding.WorldGenerator','GenerateWorld',{_passes:{_items:[],_size:0,_version:0}}); assert.equal(called,false);
});

test('Invalid generation list preserves the native list', () => {
    const pass=new PassLegacy('Original',()=>{});
    const items=[pass.Native], list={_items:items,_size:1,_version:0};
    All.prototype.ModifyWorldGenTasks=tasks=>tasks.push({Name:'Invalid',Weight:1});
    vanilla('Terraria.WorldBuilding.WorldGenerator','GenerateWorld',self=> {
        assert.equal(self._passes._items,items); assert.equal(self._passes._size,1); return true;
    });
    assert.equal(call('Terraria.WorldBuilding.WorldGenerator','GenerateWorld',{_passes:list}),true);
    assert.equal(list._version,0);
    All.prototype.ModifyWorldGenTasks=()=>{};
});
test('Hardmode before conversion after and disabled', () => {
    const order=[]; vanilla('Terraria.WorldGen','initializeHardMode',()=>order.push('conversion'));
    All.prototype.ModifyHardmodeTasks=tasks=> { tasks.unshift(new PassLegacy('Before',()=>order.push('before'))); tasks.push(new PassLegacy('After',()=>order.push('after'))); };
    call('Terraria.WorldGen','initializeHardMode'); assert.deepEqual(order,['before','conversion','after']);
    order.length=0; All.prototype.ModifyHardmodeTasks=tasks=>tasks[0].Disable(); call('Terraria.WorldGen','initializeHardMode'); assert.deepEqual(order,[]);
});
test('World header save read preserve and clear', () => {
    files.set('/test.wld.bl.header.json',JSON.stringify({'other/System':{value:3}}));
    All.prototype.SaveWorldHeader=tag=>tag.Set('ore',7);
    SystemWorldHooks.SaveHeader(); const ref={};
    assert.equal(SystemWorldHooks.TryGetHeaderData(Main.ActiveWorldFileData,All,ref),true); assert.equal(ref.value.GetInt('ore'),7);
    assert.equal(JSON.parse(files.get('/test.wld.bl.header.json'))['other/System'].value,3);
    All.prototype.SaveWorldHeader=()=>{}; SystemWorldHooks.SaveHeader(); assert.equal(SystemWorldHooks.TryGetHeaderData(Main.ActiveWorldFileData,all,ref),false);
});
test('World rejection reaches menu and loader', () => {
    All.prototype.CanWorldBePlayed=()=>false; All.prototype.WorldCanBePlayedRejectionMessage=()=> 'blocked';
    assert.equal(SystemWorldHooks.Rejection(Main.ActivePlayerFileData,Main.ActiveWorldFileData),'blocked');
    let loaded=0; vanilla('Terraria.IO.WorldFile','LoadWorld',()=>loaded++);
    call('Terraria.IO.WorldFile','LoadWorld',true); assert.equal(loaded,0);
    All.prototype.CanWorldBePlayed=()=>true; call('Terraria.IO.WorldFile','LoadWorld',true); assert.equal(loaded,1);
});
test('Send cancellation and buffer identity', () => {
    Main.netMode=2; const seen=[];
    All.prototype.HijackSendData=(who,type)=>{seen.push(who); return type===7;};
    assert.equal(SystemNetworkHooks.HijackSend(7,4,-1,null,0,0,0,0,0,0,0),true); assert.equal(SystemNetworkHooks.HijackSend(23,-1,-1,null,0,0,0,0,0,0,0),false); assert.deepEqual(seen,[4,256]); Main.netMode=0;
});

test('World veto survives rejection message exceptions', () => {
    All.prototype.CanWorldBePlayed=()=>false;
    All.prototype.WorldCanBePlayedRejectionMessage=()=>{throw Error('message fixture');};
    assert.ok(SystemWorldHooks.Rejection(Main.ActivePlayerFileData,Main.ActiveWorldFileData));
    let loaded=0; vanilla('Terraria.IO.WorldFile','LoadWorld',()=>loaded++);
    call('Terraria.IO.WorldFile','LoadWorld',true); assert.equal(loaded,0);
    All.prototype.CanWorldBePlayed=()=>true;
});
test('Receive reads do not advance another system', () => {
    native('System.IO.MemoryStream')['void .ctor(byte[] buffer, int index, int count)'].entry.vanilla=(stream,data,index,count)=>Object.assign(stream,{bytes:data.slice(index,index+count),Position:0,Length:count});
    native('System.IO.BinaryReader')['void .ctor(Stream input)'].entry.vanilla=(reader,stream)=>reader.BaseStream=stream;
    native('System.IO.BinaryReader')['byte[] ReadBytes(int count)'].entry.vanilla=(reader,count)=>{ const stream=reader.BaseStream; const data=stream.bytes.slice(stream.Position,stream.Position+count); stream.Position+=data.length; return data; };
    native('System.Array')['void Copy(Array sourceArray, int sourceIndex, Array destinationArray, int destinationIndex, int length)'].entry.vanilla=(source,start,target,end,count)=>{for(let i=0;i<count;i++)target[end+i]=source[start+i];};
    let seen=[];
    class ReaderProbe extends ModSystem { HijackGetData(type, reader, who) { seen.push([type.value,reader.value.BaseStream.Position,who]); return false; } }
    ModSystem.register(ReaderProbe);
    All.prototype.HijackGetData=(type,reader)=>{reader.value.BaseStream.Position=3; return false;};
    let packet;
    vanilla('Terraria.MessageBuffer','ProcessData',(self,data,length,out)=>{packet=Array.from(data).slice(0,length); out.value=data[0];});
    const out={};
    call('Terraria.MessageBuffer','ProcessData',{whoAmI:4},[23,7,8],3,out);
    assert.deepEqual(seen,[[23,1,4]]); assert.deepEqual(packet,[23,7,8]); assert.equal(out.value,23);
    All.prototype.HijackGetData=type=>{type.value=27;return true;}; packet=null;
    call('Terraria.MessageBuffer','ProcessData',{whoAmI:4},[23,7,8],3,out); assert.equal(packet,null); assert.equal(out.value,27);
    All.prototype.HijackGetData=type=>{type.value=27;return false;};
    call('Terraria.MessageBuffer','ProcessData',{whoAmI:4},[23,7,8],3,out); assert.deepEqual(packet,[27,7,8]);
    All.prototype.HijackGetData=(type,reader)=> {reader.value={BaseStream:{Position:0},'byte[] ReadBytes(int count)':()=>[99,42]}; return false;};
    call('Terraria.MessageBuffer','ProcessData',{whoAmI:4},[23,7,8],3,out); assert.deepEqual(packet,[23,99,42]);
});

test('Packet veto survives invalid Ref replacements', () => {
    let processed=0;
    vanilla('Terraria.MessageBuffer','ProcessData',()=>processed++);
    All.prototype.HijackGetData=(type,reader)=>{type.value=NaN; reader.value=null; return true;};
    const out={};
    call('Terraria.MessageBuffer','ProcessData',{whoAmI:4},[23,7,8],3,out);
    assert.equal(processed,0); assert.equal(out.value,23);
});
test('Isolated exceptions do not block other systems', () => {
    class Throws extends ModSystem { PreUpdatePlayers() { throw Error('fixture'); } }
    class Later extends ModSystem { PreUpdatePlayers() { events.push('later'); } }
    ModSystem.register(Throws); ModSystem.register(Later); events.length=0; stages.get('PlayersBegin')(); assert.ok(events.includes('later'));
});
test('Unload exactly once', () => { let count=0; All.prototype.OnModUnload=()=>count++; SystemLoader.Unload(); SystemLoader.Unload(); sandbox.bl.__unloadMods(); assert.equal(count,1); });
assert.deepEqual(failures,[]);
process.stdout.write(JSON.stringify({ checks, nativeHooks: [...installed.values()].filter(e=>e.callbacks.length).length, nativeStages: stages.size })+'\n');
