import fs from 'node:fs/promises';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { randomUUID } from 'node:crypto';

const root = fileURLToPath(new URL('../../', import.meta.url));
const option = (name) => {
    const index = process.argv.indexOf(name);
    return index >= 0 ? process.argv[index + 1] : undefined;
};
const runtime = option('--runtime');
if (!runtime) throw new Error('Informe --runtime com a pasta de trabalho que resolve @oai/artifact-tool.');
const requireRuntime = createRequire(path.resolve(runtime, 'package.json'));
const { Workbook, SpreadsheetFile } = await import(pathToFileURL(requireRuntime.resolve('@oai/artifact-tool')).href);
const catalog = JSON.parse(await fs.readFile(path.join(root, 'docs/referencia/metodos.json'), 'utf8'));
const outputDir = path.resolve(option('--output-dir') ?? path.join(root, 'build/api-docs/outputs', randomUUID()));
await fs.mkdir(outputDir, { recursive: true });

const workbook = Workbook.create();
const index = workbook.worksheets.add('Índice');
const methods = workbook.worksheets.add('Métodos');
const declared = catalog.rows.filter((row) => row.status === 'Declarado').length;
const unavailable = catalog.rows.length - declared;

function style(sheet, lastRow, lastColumn, widths) {
    sheet.showGridLines = false;
    sheet.getRange(`A1:${lastColumn}${lastRow}`).format.font = { name: 'Arial', size: 10, color: '#242B37' };
    sheet.getRange(`A1:${lastColumn}${lastRow}`).format.verticalAlignment = 'center';
    sheet.getRange(`A1:${lastColumn}${lastRow}`).format.rowHeight = 22;
    sheet.getRange('A2').format.font = { name: 'Arial', size: 15, bold: true, color: '#273C5A' };
    sheet.getRange(`A4:${lastColumn}4`).format.borders = { bottom: { style: 'thin', color: '#A8B9CF' } };
    widths.forEach((width, column) => sheet.getRangeByIndexes(0, column, lastRow, 1).format.columnWidthPx = width);
}

function header(sheet, range) {
    sheet.getRange(range).format = {
        fill: '#273C5A', font: { name: 'Arial', size: 10, bold: true, color: '#FFFFFF' },
        horizontalAlignment: 'center', verticalAlignment: 'center', rowHeight: 28,
        borders: { insideVertical: { style: 'thin', color: '#FFFFFF' } }
    };
}

const indexRows = catalog.classes.map((definition) => [definition.name, definition.base || '—', definition.own, definition.inherited,
    definition.own + definition.inherited]);
index.getRange('A2').values = [['Bunny Loader — métodos por classe']];
index.getRange('A3').values = [[`${catalog.classes.length} classes. ${declared} registros declarados. ${unavailable} métodos não implementados.`]];
index.getRange('A6:E6').values = [['Classe', 'Classe base', 'Próprios', 'Herdados', 'Total']];
index.getRange(`A7:E${indexRows.length + 6}`).values = indexRows;
style(index, indexRows.length + 6, 'E', [270, 210, 100, 100, 100]);
index.getRange(`C7:E${indexRows.length + 6}`).setNumberFormat('0');
index.getRange(`C7:E${indexRows.length + 6}`).format.horizontalAlignment = 'right';
index.tables.add(`A6:E${indexRows.length + 6}`, true, 'ClassesPublicas').showFilterButton = true;
header(index, 'A6:E6');
index.freezePanes.freezeRows(6);
index.tabColor = '#273C5A';

const labels = ['Classe', 'Método', 'Tipo', 'Assinatura', 'Declarado em', 'Herdado', 'Status', 'Retorno base', 'Contrato / limite', 'Etapa nativa', 'Fonte'];
const keys = ['class', 'method', 'kind', 'signature', 'declaredIn', 'inherited', 'status', 'baseReturn', 'description', 'native', 'sourceUrl'];
const values = catalog.rows.map((row) => keys.map((key) => key === 'inherited' ? row[key] ? 'Sim' : 'Não' : row[key]));
const lastRow = values.length + 6;
methods.getRange('A2').values = [['Catálogo de métodos']];
methods.getRange('A3').values = [['Declarado confirma a existência na API JS. Consulte os contratos e os resultados dos testes para o comportamento no jogo.']];
methods.getRange('A6:K6').values = [labels];
methods.getRange(`A7:K${lastRow}`).values = values;
const widths = [225, 290, 150, 600, 200, 85, 170, 390, 780, 420, 960];
style(methods, lastRow, 'K', widths);
methods.getRange(`A7:K${lastRow}`).format.wrapText = true;
methods.getRange(`A7:K${lastRow}`).format.verticalAlignment = 'top';
methods.tables.add(`A6:K${lastRow}`, true, 'MetodosPublicos').showFilterButton = true;
header(methods, 'A6:K6');
methods.freezePanes.freezeRows(6);
methods.freezePanes.freezeColumns(2);
methods.getRange(`G7:G${lastRow}`).conditionalFormats.add('containsText', {
    text: 'Não implementado', format: { fill: '#FFF0CC', font: { color: '#805209', bold: true } }
});
for (let row = 0; row < values.length; row++) {
    const lines = Math.max(...values[row].map((value, column) => Math.ceil(String(value).length / Math.max(12, (widths[column] - 16) / 7))));
    methods.getRange(`A${row + 7}:K${row + 7}`).format.rowHeightPx = Math.max(28, lines * 17 + 12);
}

workbook.recalculate();
const inspected = await workbook.inspect({ kind: 'table', range: 'Métodos!A6:I10', include: 'values,formulas', tableMaxRows: 5, tableMaxCols: 9, maxChars: 2400 });
await fs.writeFile(path.join(outputDir, 'inspect.ndjson'), inspected.ndjson);
const errors = await workbook.inspect({ kind: 'match', searchTerm: '#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!', options: { useRegex: true, maxResults: 20 }, maxChars: 1600 });
await fs.writeFile(path.join(outputDir, 'errors.ndjson'), errors.ndjson);
const previews = [
    ['Índice', 'A1:E17', 'indice'],
    ['Métodos', 'A1:D14', 'metodos-assinaturas'],
    ['Métodos', 'E6:K12', 'metodos-contratos']
];
const npcFirst = catalog.rows.findIndex((row) => row.class === 'ModNPC' && row.method === 'CanBeHitByProjectile') + 7;
previews.push(['Métodos', `A${npcFirst}:I${npcFirst + 6}`, 'modnpc-limites']);
if (process.argv.includes('--preview')) {
    for (const [sheetName, range, name] of previews) {
        const preview = await workbook.render({ sheetName, range, scale: 1, format: 'png' });
        await fs.writeFile(path.join(outputDir, name + '.png'), new Uint8Array(await preview.arrayBuffer()));
    }
}
const output = await SpreadsheetFile.exportXlsx(workbook);
const xlsx = path.join(outputDir, 'metodos.xlsx');
await output.save(xlsx);
await fs.copyFile(xlsx, path.join(root, 'docs/referencia/metodos.xlsx'));
console.log(JSON.stringify({ xlsx, rows: values.length, classes: catalog.classes.length, previews: outputDir }));
