import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import { parse } from 'acorn';

const root = fileURLToPath(new URL('../../', import.meta.url));
const apiRoot = 'app/src/main/cpp/script/js/mod';
const outputRoot = 'docs/referencia';
const metadata = JSON.parse(await fs.readFile(new URL('./catalog.json', import.meta.url), 'utf8'));
const sources = new Map();
const definitions = new Map();
const clean = (text) => text.replace(/\r?\n\s*/g, ' ').trim();
const plain = (text) => clean(text.replace(/\[([^\]]+)\]\([^)]+\)/g, '$1').replace(/[`*]/g, ''));
const markdown = (text) => String(text).replace(/\|/g, '\\|').replace(/\r?\n/g, ' ');
const word = (name) => new RegExp('\\b' + name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '\\b');

async function files(directory) {
    const result = [];
    for (const entry of await fs.readdir(path.join(root, directory), { withFileTypes: true })) {
        const relative = directory + '/' + entry.name;
        if (entry.isDirectory()) result.push(...await files(relative));
        else if (entry.name.endsWith('.js')) result.push(relative);
    }
    return result.sort();
}

function children(node) {
    return Object.values(node).flatMap((value) => Array.isArray(value)
        ? value.filter((item) => item && typeof item.type === 'string')
        : value && typeof value.type === 'string' ? [value] : []);
}

function walk(node, visit) {
    visit(node);
    for (const child of children(node)) walk(child, visit);
}

function baseReturn(member, source) {
    if (member.kind === 'constructor') return 'instância';
    const body = member.value.body;
    if (body.type !== 'BlockStatement') return clean(source.slice(body.start, body.end));
    if (!body.body.length) return 'undefined';
    if (body.body.length !== 1 || body.body[0].type !== 'ReturnStatement') return 'Consultar fonte';
    const expression = body.body[0].argument;
    const text = expression ? clean(source.slice(expression.start, expression.end)) : 'undefined';
    return text.length <= 160 ? text : 'Consultar fonte';
}

function sourceComment(member, source) {
    const lines = source.slice(0, member.start).split(/\r?\n/);
    if (!lines.pop().trim()) {
        const comments = [];
        while (lines.length && /^\s*\/\//.test(lines.at(-1))) comments.unshift(lines.pop().replace(/^\s*\/\/\s?/, ''));
        return plain(comments.join(' '));
    }
    return '';
}

for (const relative of await files(apiRoot)) {
    const source = await fs.readFile(path.join(root, relative), 'utf8');
    const ast = parse(source, { ecmaVersion: 'latest', locations: true, sourceType: 'script' });
    sources.set(relative, { source, ast });
    for (const declaration of ast.body.filter((node) => node.type === 'ClassDeclaration')) {
        const name = declaration.id.name;
        if (definitions.has(name)) throw new Error('Classe duplicada: ' + name);
        const methods = [];
        for (const member of declaration.body.body) {
            if (member.type !== 'MethodDefinition' || member.key.type === 'PrivateIdentifier') continue;
            if (member.computed && member.key.type !== 'Literal') throw new Error('Método computado sem nome literal: ' + name);
            const method = member.key.name ?? member.key.value;
            const parameters = member.value.params.map((parameter) => clean(source.slice(parameter.start, parameter.end))).join(', ');
            methods.push({
                method, signature: member.kind === 'get' ? 'get ' + method : member.kind === 'set' ? 'set ' + method + '(' + parameters + ')' : method + '(' + parameters + ')',
                kind: member.kind === 'constructor' ? 'Construtor' : member.kind === 'get' ? member.static ? 'Getter estático' : 'Getter' : member.kind === 'set' ? member.static ? 'Setter estático' : 'Setter' : member.static ? 'Estático' : 'Instância',
                scope: member.static ? 'static' : 'instance', accessor: member.kind === 'get' || member.kind === 'set' ? member.kind : '',
                declaredIn: name, source: relative, line: member.loc.start.line,
                baseReturn: baseReturn(member, source), comment: sourceComment(member, source)
            });
        }
        definitions.set(name, { name, base: declaration.superClass?.name ?? '', source: relative, line: declaration.loc.start.line, methods });
    }
}

function resolveAlias(node, source, relative, bindings = {}) {
    if (node.type === 'ForOfStatement') {
        const identifier = node.left.declarations?.[0]?.id;
        if (identifier?.type === 'Identifier' && node.right.type === 'ArrayExpression') {
            for (const element of node.right.elements) {
                if (element?.type !== 'Literal' || typeof element.value !== 'string') continue;
                resolveAlias(node.body, source, relative, { ...bindings, [identifier.name]: element.value });
            }
            return;
        }
    }
    if (node.type === 'AssignmentExpression' && node.left.type === 'MemberExpression' && node.right.type === 'MemberExpression') {
        const left = node.left.object;
        const right = node.right.object;
        if (left.type === 'MemberExpression' && left.property.name === 'prototype' && right.type === 'MemberExpression' && right.property.name === 'prototype') {
            const target = definitions.get(left.object.name);
            const origin = definitions.get(right.object.name);
            const name = node.left.computed ? node.left.property.value ?? bindings[node.left.property.name] : node.left.property.name;
            const originName = node.right.computed ? node.right.property.value : node.right.property.name;
            const original = origin?.methods.find((method) => method.method === originName && method.scope === 'instance');
            if (target && name && original) target.methods.push({
                ...original, method: name, signature: original.signature.replace(/^[^(]+/, name), source: relative, line: node.loc.start.line,
                comment: 'Alias de ' + origin.name + '.' + originName + '.', aliasOf: origin.name + '.' + originName
            });
        }
    }
    for (const child of children(node)) resolveAlias(child, source, relative, bindings);
}

for (const [relative, { source, ast }] of sources) resolveAlias(ast, source, relative);

const exportsAst = sources.get(apiRoot + '/Exports.js').ast;
const assignment = exportsAst.body.find((node) => node.type === 'ExpressionStatement' && node.expression.type === 'CallExpression' && node.expression.callee.object?.name === 'Object' && node.expression.callee.property?.name === 'assign');
const exported = assignment.expression.arguments[1].properties.map((property) => property.key.name);
if (new Set(exported).size !== exported.length) throw new Error('Export duplicado');

const docs = [outputRoot + '/classes.md', outputRoot + '/modplayer-hooks.md'];
const descriptions = new Map();
for (const relative of docs) {
    const text = await fs.readFile(path.join(root, relative), 'utf8');
    let section = '';
    let subsection = '';
    let previousNative = '';
    for (const line of text.split(/\r?\n/)) {
        if (line.startsWith('## ')) { section = plain(line.slice(3)); subsection = ''; previousNative = ''; }
        if (line.startsWith('### ')) { subsection = plain(line.slice(4)); previousNative = ''; }
        if (!line.startsWith('|')) continue;
        const cells = line.split('|').slice(1, -1).map((cell) => cell.trim());
        if (cells.length < 2 || !cells[0].includes('`')) continue;
        const owners = relative.endsWith('modplayer-hooks.md') ? ['ModPlayer'] : exported.filter((name) => {
            if (/^Global(Item|NPC|Projectile)$/.test(subsection)) return name === subsection;
            if (/^Texturas vestidas/.test(subsection)) return name === 'EquipLoader' || name === 'EquipTexture';
            const anchor = metadata.sections[name];
            const slug = section.toLowerCase().replace(/[^\p{L}\p{N} _-]/gu, '').replace(/\s+/g, '-');
            return word(name).test(section) || anchor === slug;
        });
        let native = cells[2] ? plain(cells[2]) : '';
        if (/^idem\b/i.test(native)) native = native.replace(/^idem\b/i, previousNative || 'mesmo fluxo da tabela de referência');
        if (native) previousNative = native;
        for (const owner of owners) {
            for (const method of inherited(owner)) {
                if (method.method === 'constructor') continue;
                if (!word(method.method).test(cells[0])) continue;
                const description = plain(cells[1]);
                if (description && description !== '—') descriptions.set(owner + '.' + method.method, { description, native });
            }
        }
    }
}

function inherited(name, visiting = new Set()) {
    if (visiting.has(name)) throw new Error('Herança cíclica: ' + name);
    const definition = definitions.get(name);
    if (!definition) return [];
    const all = new Map();
    for (const method of inherited(definition.base, new Set([...visiting, name]))) {
        if (method.method !== 'constructor') all.set(method.scope + '/' + method.accessor + '/' + method.method, method);
    }
    for (const method of definition.methods) all.set(method.scope + '/' + method.accessor + '/' + method.method, method);
    return [...all.values()];
}

const classes = exported.filter((name) => definitions.has(name)).sort().map((name) => ({ ...definitions.get(name), methods: undefined }));
const objects = exported.filter((name) => !definitions.has(name)).sort();
const rows = [];
for (const definition of classes) {
    for (const method of inherited(definition.name)) {
        const doc = descriptions.get(definition.name + '.' + method.method) ?? descriptions.get(method.declaredIn + '.' + method.method);
        const note = metadata.notes[definition.name + '.' + method.method] ?? metadata.notes[method.declaredIn + '.' + method.method];
        const description = [doc?.description || method.comment || (method.baseReturn === 'undefined' ? 'A implementação base não executa ações.' : 'Consulte a implementação na fonte.'), note].filter(Boolean).join(' ');
        rows.push({
            class: definition.name, method: method.method, kind: method.kind, signature: method.signature,
            declaredIn: method.declaredIn, inherited: method.declaredIn !== definition.name,
            status: 'Declarado', baseReturn: method.baseReturn, description, native: doc?.native ?? '',
            source: method.source, line: method.line,
            sourceUrl: metadata.repository + '/blob/main/' + method.source + '#L' + method.line,
            reference: outputRoot + '/classes.md#' + metadata.sections[definition.name], aliasOf: method.aliasOf ?? ''
        });
    }
}
for (const unavailable of metadata.unavailable) {
    if (rows.some((row) => row.class === unavailable.class && row.method === unavailable.method)) throw new Error('Método disponível marcado como ausente: ' + unavailable.method);
    const source = unavailable.source || 'tools/tests/modnpchooks/README.md';
    const anchor = unavailable.anchor || 'métodos-descartados';
    rows.push({ class: unavailable.class, method: unavailable.method, kind: 'Não implementado', signature: '', declaredIn: '', inherited: false,
        status: 'Não implementado', baseReturn: '', description: unavailable.reason, native: '', source, line: 0,
        sourceUrl: metadata.repository + '/blob/main/' + source + '#' + anchor, reference: outputRoot + '/classes.md#' + metadata.sections[unavailable.class], aliasOf: '' });
}
rows.sort((a, b) => a.class.localeCompare(b.class, 'en') || a.method.localeCompare(b.method, 'en') || a.kind.localeCompare(b.kind, 'en'));
for (const definition of classes) {
    if (!metadata.sections[definition.name]) throw new Error('Classe sem referência: ' + definition.name);
    const methods = rows.filter((row) => row.class === definition.name && row.status === 'Declarado');
    definition.own = methods.filter((row) => !row.inherited).length;
    definition.inherited = methods.filter((row) => row.inherited).length;
}
const keys = rows.map((row) => [row.class, row.method, row.kind].join('/'));
if (new Set(keys).size !== keys.length) throw new Error('Linha duplicada no catálogo');
for (const key of Object.keys(metadata.notes)) if (!rows.some((row) => row.class + '.' + row.method === key)) throw new Error('Nota sem método: ' + key);
const referenceText = await fs.readFile(path.join(root, outputRoot, 'classes.md'), 'utf8');
const anchors = new Set(referenceText.split(/\r?\n/).filter((line) => /^#{2,3} /.test(line)).map((line) => plain(line.replace(/^#+ /, '')).toLowerCase().replace(/[^\p{L}\p{N} _-]/gu, '').replace(/\s+/g, '-')));
for (const [name, anchor] of Object.entries(metadata.sections)) if (!anchors.has(anchor)) throw new Error('Âncora ausente para ' + name + ': ' + anchor);

const sourceFiles = new Set([apiRoot + '/Exports.js', ...classes.map((definition) => definition.source), ...rows.filter((row) => row.status === 'Declarado').map((row) => row.source)]);
const hash = createHash('sha256');
for (const relative of [...sourceFiles].sort()) hash.update(relative + '\n' + sources.get(relative).source.replace(/\r\n/g, '\n') + '\n');
const catalog = { schema: 1, sourceHash: hash.digest('hex'), classes, objects, rows };

const lines = [
    '# Métodos por classe', '',
    'Catálogo gerado das classes públicas de `Exports.js`, com uma linha por método, construtor ou getter/setter declarado em JavaScript. Inclui métodos herdados e aliases de `NetWriter`/`NetReader`. Campos, enums e objetos de descritores ficam fora da contagem de métodos. Os métodos das classes nativas de `Terraria.*`, de `bl.*` e os loaders privados são consultados pela [ponte](ponte-e-bl.md).', '',
    'Abra a [planilha Excel](metodos.xlsx) para filtrar a tabela por classe, método, tipo ou status. O [CSV UTF-8](metodos.csv) contém os mesmos registros e o [JSON](metodos.json) permite consulta por ferramentas.', '',
    '`Declarado` confirma que o membro existe na API JavaScript atual. Não indica que todos os seus fluxos foram testados no jogo. A coluna de retorno mostra a expressão da implementação base, não uma declaração de tipo nem o retorno obrigatório do override. `Consultar fonte` identifica implementações com mais de uma instrução. As assinaturas são do Bunny Loader e podem diferir das do tModLoader.', '',
    'Contratos e parâmetros mutáveis: [referência das classes](classes.md), [ModPlayer](modplayer-hooks.md) e [ref/out](../mods/02-ref-e-out.md). Os dez métodos de ModNPC sem adaptação completa aparecem com status `Não implementado` e sem assinatura inventada.', '',
    'Instale a dependência com `npm --prefix tools/docs ci` e atualize com `node tools/docs/generate.mjs`. Confira diferenças com `node tools/docs/generate.mjs --check`. A [ferramenta de documentação](../../tools/docs/README.md) explica a atualização da planilha.', '',
    '## Índice', '', '| Classe | Membros próprios | Herdados | Contratos |', '|---|---:|---:|---|'
];
for (const definition of classes) lines.push(`| [\`${definition.name}\`](#${definition.name.toLowerCase()}) | ${definition.own} | ${definition.inherited} | [Referência](classes.md#${metadata.sections[definition.name]}) |`);
lines.push('', `${classes.length} classes públicas, ${rows.filter((row) => row.status === 'Declarado').length} registros declarados e ${metadata.unavailable.length} métodos não implementados. A contagem inclui o mesmo membro em cada classe que o herda.`, '', '## Objetos e enums', '', objects.map((name) => '`' + name + '`').join(', ') + '. São exports públicos sem declaração de classe JavaScript neste módulo.', '');
for (const definition of classes) {
    lines.push('## ' + definition.name, '', '[Contrato e campos](classes.md#' + metadata.sections[definition.name] + ')' + (definition.base ? '. Herda de `' + definition.base + '`.' : '.'), '',
        '| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |', '|---|---|---|---|---|---|');
    for (const row of rows.filter((row) => row.class === definition.name)) {
        const source = row.status === 'Declarado' ? `../../${row.source}#L${row.line}` : '../../' + row.source + '#' + row.sourceUrl.split('#')[1];
        lines.push('| ' + [
            '`' + markdown(row.signature || row.method) + '`', row.kind,
            row.status === 'Declarado' ? '`' + row.declaredIn + '`' + (row.inherited ? ' (herdado)' : '') : 'Não implementado',
            row.baseReturn ? '`' + markdown(row.baseReturn) + '`' : '—', markdown(row.description), `[Código](${source})`
        ].join(' | ') + ' |');
    }
    lines.push('');
}
const columns = [['Classe', 'class'], ['Método', 'method'], ['Tipo', 'kind'], ['Assinatura', 'signature'], ['Declarado em', 'declaredIn'], ['Herdado', 'inherited'], ['Status', 'status'], ['Retorno base', 'baseReturn'], ['Contrato / limite', 'description'], ['Método nativo / etapa', 'native'], ['Fonte', 'sourceUrl']];
const csvCell = (value) => '"' + String(value).replace(/"/g, '""') + '"';
const csv = '\ufeff' + [columns.map(([label]) => label), ...rows.map((row) => columns.map(([, key]) => key === 'inherited' ? row[key] ? 'Sim' : 'Não' : row[key]))].map((row) => row.map(csvCell).join(',')).join('\r\n') + '\r\n';
const outputs = { 'metodos.md': lines.join('\n'), 'metodos.csv': csv, 'metodos.json': JSON.stringify(catalog, null, 2) + '\n' };
let failed = false;
for (const [name, content] of Object.entries(outputs)) {
    const target = path.join(root, outputRoot, name);
    if (process.argv.includes('--check')) {
        const current = await fs.readFile(target, 'utf8').catch(() => '');
        if (current.replace(/\r\n/g, '\n') !== content.replace(/\r\n/g, '\n')) { console.error('Desatualizado: ' + outputRoot + '/' + name); failed = true; }
    } else await fs.writeFile(target, content, 'utf8');
}
console.log(`${classes.length} classes, ${objects.length} objetos/enums, ${rows.filter((row) => row.status === 'Declarado').length} registros declarados, ${metadata.unavailable.length} não implementados.`);
if (failed) process.exitCode = 1;
