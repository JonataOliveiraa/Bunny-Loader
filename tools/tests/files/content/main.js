// bl.file, bl.directory, bl.path, bl.mod, bl.info, classes sem namespace
// como globais e o bl.log com varios valores. Roda na carga do mod.
// Loga "files <caso>: ok | FALHOU".
let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('files ' + label + ': ok');
        else { fails++; bl.log('files ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('files ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

check('bl.mod', () => {
    const m = bl.mod;
    if (!m) return 'undefined';
    if (m.uuid !== 'fb946d3a-5473-4209-9767-c680168b26e3') return 'uuid ' + m.uuid;
    if (m.name !== 'Teste: arquivos, bl.mod e globais') return 'name ' + m.name;
    if (!m.path.endsWith('/content') || !m.root.endsWith(m.uuid)) return `path ${m.path}, root ${m.root}`;
    return bl.directory.exists(m.dataDirectory) || 'dataDirectory ' + m.dataDirectory;
});

check('bl.info', () => {
    const i = bl.info;
    return (i.terrariaVersionCode > 0 && bl.directory.exists(i.appDirectory) && bl.directory.exists(i.logsDirectory)) ||
        JSON.stringify(i);
});

check('arquivos do mod (relativo)', () => {
    if (!bl.file.exists('main.js') || bl.file.exists('nao-existe.js')) return 'exists';
    const src = bl.file.read('main.js');
    if (!src || !src.includes("check('arquivos do mod")) return 'read';
    return bl.file.read('nao-existe.txt') === undefined || 'read de arquivo que nao existe';
});

check('listFiles e listDirectories', () => {
    const files = bl.directory.listFiles('Textures');
    const dirs = bl.directory.listDirectories('Textures');
    return (JSON.stringify(files) === '["Textures/a.png","Textures/b.png"]' &&
            JSON.stringify(dirs) === '["Textures/Sub"]') || JSON.stringify({ files, dirs });
});

check('dataDirectory: escrever, anexar, ler, apagar', () => {
    const dir = bl.path.join(bl.mod.dataDirectory, 'teste', 'fundo');
    const f = bl.path.join(dir, 'save.json');
    bl.file.write(f, JSON.stringify({ a: 1 }));
    bl.file.append(f, '\n//fim');
    const txt = bl.file.read(f);
    if (txt !== '{"a":1}\n//fim') return 'conteudo ' + txt;
    bl.file.write(bl.path.join(dir, 'b.bin'), new Uint8Array([0, 255, 7]));
    const bytes = bl.file.readBytes(bl.path.join(dir, 'b.bin'));
    if (!(bytes instanceof Uint8Array) || bytes.length !== 3 || bytes[1] !== 255) return 'bytes ' + bytes;
    if (!bl.file.delete(f) || bl.file.exists(f)) return 'delete';
    const top = bl.path.join(bl.mod.dataDirectory, 'teste');
    return (bl.directory.delete(top) && !bl.directory.exists(top)) || 'directory.delete';
});

check('bl.path', () => {
    const r = [
        bl.path.join('a/', '/b', 'c.png'), bl.path.join('a', 'b', 'c.png'),
        bl.path.getName('x/y/z.png'), bl.path.getParentPath('x/y/z.png'), bl.path.getExtension('z.tar.gz'),
    ];
    return JSON.stringify(r) === '["/b/c.png","a/b/c.png","z.png","x/y",".gz"]' || JSON.stringify(r);
});

check('classe sem namespace como global', () => {
    if (typeof GUIBuffs === 'undefined') return 'GUIBuffs undefined';
    const draw = GUIBuffs['void Draw()'];
    return (typeof draw === 'function' || typeof draw === 'object') || 'Draw ' + typeof draw;
});

check('globais nossas nao foram sombreadas', () =>
    (typeof Vector2.new === 'function' && typeof ModItem === 'function' && typeof Math.max === 'function') ||
    'Vector2/ModItem/Math');

bl.log('files bl.log:', 1, 'texto', { a: [1, 2] }, [3, 'x'], null, undefined);
bl.log('files FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
