import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const source = path.join(root, 'app/src/main/cpp/script/js/mod');
const dump = fs.readFileSync(path.join(root, 'refs/dump.cs'), 'utf8');
const files = {
    'en-US': {
        Common: { Name: 'Wand', Greeting: 'Hello', Format: '{0} of {1}' },
        ItemName: { Probe: '{$Common.Name}: {$LegacyInterface.28}' },
        ItemTooltip: { Probe: 'First\nBuy: {$LegacyInterface.28}\n{$Common.Greeting}' },
        Refs: { Game: '{$LegacyInterface.28}', Args: '{$Common.Format@2}', Nested: '{$Refs.Game@2}',
            Missing: '{$Unknown.Key}', Loop: 'a{$Refs.Loop}' },
        Configs: { ProbeConfig: { enabled: { Label: 'Enabled' }, mode: { Label: 'Mode', a: 'Alpha', b: 'Beta' },
            action: { Label: 'Action', Text: 'Run' } } },
    },
    'pt-BR': {
        Common: { Name: 'Varinha', Greeting: 'Olá' },
        ItemName: { Probe: '{$Common.Name}: {$LegacyInterface.28}' },
        ItemTooltip: { Probe: 'Primeira\nComprar: {$LegacyInterface.28}\n{$Common.Greeting}' },
        Configs: { ProbeConfig: { enabled: { Label: 'Ativo' }, mode: { Label: 'Modo', a: 'Alfa', b: 'Beta BR' },
            action: { Label: 'Ação', Text: 'Executar' } } },
    },
};
const values = new Map(), hooks = new Map(), ready = [], errors = [], events = [], reads = [];
const mod = { uuid: 'probe-uuid', id: 'probe' };
let checks = 0, failures = 0, loads = 0, invalidations = 0;
function check(label, fn) {
    checks++;
    try { fn(); } catch (error) { failures++; console.error(label + ': ' + error.message); }
}
function localized(key, value = key) {
    return { Key: key, Value: value, ['void SetValue(string text)'](text) { this.Value = text; },
        ['void .ctor(string key, string text)'](name, text) { this.Key = name; this.Value = text; } };
}
function put(key, text) {
    if (values.has(key)) values.get(key).Value = text;
    else values.set(key, localized(key, text));
}
const culture = name => ({ Name: name });
const manager = { ActiveCulture: culture('en-US'), LoadingCulture: null, intialLoadDone: true,
    _localizedTexts: { ContainsKey: key => values.has(key), get_Item: key => values.get(key),
        ['void set_Item(string key, LocalizedText value)'](key, text) { values.set(key, text); } },
};
function method(signature, original) {
    const fn = (...args) => {
        const callback = hooks.get(signature);
        return callback ? callback(original, ...args) : original(...args);
    };
    fn.hook = callback => {
        assert.ok(dump.includes(signature.replace(/, bool processCopyCommands\)/, ', bool processCopyCommands = True)')), signature);
        assert.ok(!hooks.has(signature), 'duplicate hook: ' + signature);
        hooks.set(signature, callback);
    };
    return fn;
}
const Manager = { Instance: manager };
const Language = {
    get ActiveCulture() { return manager.ActiveCulture; },
    ['bool Exists(string key)']: key => values.has(key),
    ['string GetTextValue(string key)']: key => values.get(key)?.Value ?? key,
    ['LocalizedText GetText(string key)']: key => values.get(key) ?? localized(key),
};
manager['void LoadLanguageFromFileText(string fileText)'] = text => {
    loads++;
    for (const [category, entries] of Object.entries(JSON.parse(text))) {
        for (const [key, value] of Object.entries(entries)) put(category + '.' + key, value);
    }
};
Manager['void LoadFilesForCulture(GameCulture culture)'] = method('void LoadFilesForCulture(GameCulture culture)', (self, next) => {
    events.push('files:' + next.Name);
    put('LegacyInterface.28', next.Name === 'pt-BR' ? 'Loja {0}' : 'Shop {0}');
});
Manager['void LoadLanguage(GameCulture culture, bool processCopyCommands)'] = method('void LoadLanguage(GameCulture culture, bool processCopyCommands)', (self, next) => {
    self.LoadingCulture = next;
    Manager['void LoadFilesForCulture(GameCulture culture)'](self, next);
    events.push({ changing: next.Name, name: Language['string GetTextValue(string key)']('ItemName.Probe'),
        hello: Language['string GetTextValue(string key)']('Mods.probe.Common.Greeting'),
        translated: run("ModLocalization.Translate('Common.Greeting')"),
        held: run("ModLocalization.GetText('Common.Greeting').Value") });
    run("ModLocalization.Register('Reentrant.Name', state.names)");
});
Manager['void SetLanguage(GameCulture culture)'] = method('void SetLanguage(GameCulture culture)', (self, next) => {
    if (!self.intialLoadDone) { self.delayedActiveCulture = next; return; }
    if (self.ActiveCulture.Name === next.Name) return;
    if (next.Name !== 'en-US' && self.ActiveCulture.Name !== 'en-US') {
        for (const [key, text] of values) text.Value = key;
        Manager['void LoadLanguage(GameCulture culture, bool processCopyCommands)'](self, culture('en-US'), true);
    }
    Manager['void LoadLanguage(GameCulture culture, bool processCopyCommands)'](self, next, true);
    self.ActiveCulture = next;
    events.push({ changed: next.Name, tooltip: values.get('BunnyLoader.ItemTooltip.probe-uuid.Probe')?.Value });
});
const context = vm.createContext({ Terraria: { Localization: { Language, LanguageManager: Manager,
    LocalizedText: { new: () => localized('') } }, UI: { ItemTooltip: { ['void InvalidateTooltips()']() { invalidations++; } } } },
    bl: { mod, readJson(file) { reads.push(file); return files[path.basename(file, '.json')]; },
        items: { setTooltip(type, texts) {
            assert.equal(type, 6000);
            put('BunnyLoader.ItemTooltip.probe-uuid.Probe', texts[manager.ActiveCulture.Name] ?? texts['en-US']);
        } } },
    Ready: { Add: fn => ready.push(fn) },
    ModItem: class { ModifyTooltipLines() {} },
    ModRegistry: { DataDirectory: () => null },
    Safe: { Run(label, fn) { try { return fn(); } catch (error) { errors.push([label, error]); } } },
    state: {}, assert,
});
for (const file of ['Core/Hooks.js', 'Core/Lang.js', 'ModLocalization.js', 'Loaders/LocalizationLoader.js',
    'Loaders/ItemLoader.js', 'Loaders/ConfigLoader.js']) {
    vm.runInContext(fs.readFileSync(path.join(source, file), 'utf8'), context, { filename: file });
}
const run = code => vm.runInContext(code, context);
const text = key => values.get(key)?.Value;
const set = name => Manager['void SetLanguage(GameCulture culture)'](manager, culture(name));
put('LegacyInterface.28', 'Shop {0}');
run(`LocalizationLoader.Load(bl.mod);
    state.names = Lang.Localized('ItemName', 'Probe');
    Lang.Follow('ItemName.Probe', state.names);
    class Probe extends ModItem { ModifyTooltipLines() { this.TooltipLines[0] += '!'; this.TooltipLines.push('End'); } }
    ItemLoader.SetupTooltip(new Probe(), 'Probe', 6000);
    class ProbeConfig { static Options = {
        enabled: { type: 'toggle', default: true },
        mode: { type: 'radio', default: 'a', choices: ['a', 'b'] },
        action: { type: 'button', action() {} },
        fallback: { type: 'toggle', default: false, label: 'Fallback' },
    }; Mod = bl.mod; OnLoaded() {} }
    ConfigLoader.Add(new ProbeConfig());
    state.options = ConfigLoader.List[0].options;
    ModLocalization.Register('Plain.Text', 'Literal');
    ModLocalization.Register('Custom.Name', { 'en-US': 'Name', 'pt-BR': 'Nome' });
    Lang.Follow('NPCName.Plain', 'Literal NPC');`);
for (const fn of ready) fn();
const held = values.get('ItemName.Probe');
check('initial content reference', () => assert.equal(held.Value, 'Wand: Shop {0}'));
check('initial tooltip lines', () => assert.equal(text('BunnyLoader.ItemTooltip.probe-uuid.Probe'), 'First!\nBuy: Shop {0}\nHello\nEnd'));
check('initial config label', () => assert.equal(run('state.options[0].label'), 'Enabled'));
const beforeReads = reads.length;
for (const name of ['pt-BR', 'en-US', 'pt-BR', 'fr-FR', 'pt-BR', 'en-US']) {
    const pt = name === 'pt-BR';
    set(name);
    check(name + ' content reference', () => assert.equal(held.Value, (pt ? 'Varinha' : 'Wand') + ': ' + (pt ? 'Loja {0}' : 'Shop {0}')));
    check(name + ' content identity', () => assert.equal(values.get('ItemName.Probe'), held));
    check(name + ' tooltip', () => assert.equal(text('BunnyLoader.ItemTooltip.probe-uuid.Probe'), pt ?
        'Primeira!\nComprar: Loja {0}\nOlá\nEnd' : 'First!\nBuy: Shop {0}\nHello\nEnd'));
    check(name + ' config label', () => assert.equal(run('state.options[0].label'), pt ? 'Ativo' : 'Enabled'));
    check(name + ' config choices', () => assert.equal(run('state.options[1].choiceLabels.join()'), pt ? 'Alfa,Beta BR' : 'Alpha,Beta'));
    check(name + ' config button', () => assert.equal(run('state.options[2].text'), pt ? 'Executar' : 'Run'));
    check(name + ' config fallback', () => assert.equal(run('state.options[3].label'), 'Fallback'));
    check(name + ' English fallback args', () => assert.equal(text('Mods.probe.Refs.Args'), '{2} of {3}'));
    check(name + ' game args offset', () => assert.equal(text('Mods.probe.Refs.Nested'), (pt ? 'Loja' : 'Shop') + ' {2}'));
    check(name + ' missing reference', () => assert.equal(text('Mods.probe.Refs.Missing'), 'Unknown.Key'));
    check(name + ' cyclic reference', () => assert.equal(text('Mods.probe.Refs.Loop'), 'aMods.probe.Refs.Loop'));
    check(name + ' literal name', () => assert.equal(text('NPCName.Plain'), 'Literal NPC'));
    check(name + ' registered text', () => assert.equal(text('Custom.Name'), pt ? 'Nome' : 'Name'));
    check(name + ' reentrant registration', () => assert.equal(text('Reentrant.Name'), (pt ? 'Varinha' : 'Wand') + ': ' + (pt ? 'Loja {0}' : 'Shop {0}')));
}
check('JSON read only once', () => assert.equal(reads.length, beforeReads));
check('texts before OnLanguageChanging', () => {
    for (const event of events.filter(e => e.changing)) {
        const pt = event.changing === 'pt-BR';
        assert.equal(event.hello, pt ? 'Olá' : 'Hello');
        assert.equal(event.translated, pt ? 'Olá' : 'Hello');
        assert.equal(event.held, pt ? 'Olá' : 'Hello');
        assert.equal(event.name, (pt ? 'Varinha' : 'Wand') + ': ' + (pt ? 'Loja {0}' : 'Shop {0}'));
    }
});
check('tooltip before OnLanguageChanged', () => {
    for (const event of events.filter(e => e.changed)) {
        assert.equal(event.tooltip, event.changed === 'pt-BR' ? 'Primeira!\nComprar: Loja {0}\nOlá\nEnd' :
            'First!\nBuy: Shop {0}\nHello\nEnd');
    }
});
check('same culture does not reload texts', () => { const count = loads; set('en-US'); assert.equal(loads, count); });
check('delayed initialization does not apply pending culture', () => {
    manager.intialLoadDone = false;
    const count = loads;
    set('pt-BR');
    assert.equal(loads, count);
    assert.equal(held.Value, 'Wand: Shop {0}');
    manager.intialLoadDone = true;
    set(manager.delayedActiveCulture.Name);
    assert.equal(held.Value, 'Varinha: Loja {0}');
});
check('tooltip invalidation', () => assert.ok(invalidations > 0));
check('no runtime errors', () => assert.deepEqual(errors, []));
check('one language hook', () => assert.deepEqual([...hooks.keys()], ['void LoadFilesForCulture(GameCulture culture)']));
console.log('Localization: ' + (checks - failures) + '/' + checks + ' checks passed');
if (failures) process.exitCode = 1;
