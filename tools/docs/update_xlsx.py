"""Atualiza as linhas da docs/referencia/metodos.xlsx a partir do metodos.json,
sem o @oai/artifact-tool do workbook.mjs: troca só as linhas de dados das duas
abas (mesmos estilos), o resumo do Índice e o alcance das tabelas e do
realce. Rodar depois do generate.mjs; o verify.py confere.

    python tools/docs/update_xlsx.py
"""
import json, re, shutil
from pathlib import Path
from xml.sax.saxutils import escape
from zipfile import ZipFile, ZIP_DEFLATED

ref = Path(__file__).resolve().parents[2] / "docs" / "referencia"
catalog = json.loads((ref / "metodos.json").read_text(encoding="utf-8"))
src = ref / "metodos.xlsx"
tmp = ref / "metodos.tmp.xlsx"

keys = ["class", "method", "kind", "signature", "declaredIn", "inherited", "status", "baseReturn", "description", "native", "sourceUrl"]
rows = [[("Sim" if r[k] else "Não") if k == "inherited" else str(r[k] or "") for k in keys] for r in catalog["rows"]]
last = len(rows) + 6

def cell(col, n, value, style, kind="str"):
    if kind == "n":
        return f'<x:c r="{col}{n}" s="{style}" t="n"><x:v>{value}</x:v></x:c>'
    if value == "":
        return f'<x:c r="{col}{n}" s="{style}" t="str" />'
    return f'<x:c r="{col}{n}" s="{style}" t="str"><x:v>{escape(value)}</x:v></x:c>'

def replace_rows(xml, keep, new_rows):
    head = xml[:xml.index('<x:sheetData>') + len('<x:sheetData>')]
    tail = xml[xml.index('</x:sheetData>'):]
    body = xml[len(head):xml.index('</x:sheetData>')]
    old = re.findall(r'<x:row [^>]*>.*?</x:row>', body, re.S)
    return head + "".join(old[:keep]) + "".join(new_rows) + tail

with ZipFile(src) as z:
    files = {n: z.read(n) for n in z.namelist()}

# Métodos
s2 = files['xl/worksheets/sheet2.xml'].decode('utf-8')
method_rows = []
for i, values in enumerate(rows):
    n = i + 7
    cells = "".join(cell("ABCDEFGHIJK"[c], n, v, 16) for c, v in enumerate(values))
    method_rows.append(f'<x:row r="{n}" ht="21.75" customHeight="1">{cells}</x:row>')
s2 = replace_rows(s2, 6, method_rows)
s2 = re.sub(r'sqref="G7:G\d+"', f'sqref="G7:G{last}"', s2)
files['xl/worksheets/sheet2.xml'] = s2.encode('utf-8')
t2 = files['xl/tables/table2.xml'].decode('utf-8')
t2 = re.sub(r'ref="A6:K\d+"', f'ref="A6:K{last}"', t2)
files['xl/tables/table2.xml'] = t2.encode('utf-8')

# Índice
classes = catalog["classes"]
declared = sum(1 for r in catalog["rows"] if r["status"] == "Declarado")
missing = len(catalog["rows"]) - declared
s1 = files['xl/worksheets/sheet1.xml'].decode('utf-8')
s1 = re.sub(r'(<x:c r="A3" s="2" t="str"><x:v>)[^<]*(</x:v>)', lambda m: m.group(1) + f"{len(classes)} classes. {declared} registros declarados. {missing} métodos não implementados." + m.group(2), s1)
index_rows = []
for i, d in enumerate(classes):
    n = i + 7
    cells = (cell("A", n, d["name"], 2) + cell("B", n, d["base"] or "—", 2) +
             cell("C", n, d["own"], 6, "n") + cell("D", n, d["inherited"], 6, "n") + cell("E", n, d["own"] + d["inherited"], 6, "n"))
    index_rows.append(f'<x:row r="{n}" ht="22" customHeight="1">{cells}</x:row>')
s1 = replace_rows(s1, 6, index_rows)
files['xl/worksheets/sheet1.xml'] = s1.encode('utf-8')
t1 = files['xl/tables/table1.xml'].decode('utf-8')
t1 = re.sub(r'ref="A6:E\d+"', f'ref="A6:E{len(classes) + 6}"', t1)
files['xl/tables/table1.xml'] = t1.encode('utf-8')

with ZipFile(tmp, 'w', ZIP_DEFLATED) as z:
    for name, data in files.items():   # a ordem do arquivo original
        z.writestr(name, data)
shutil.move(tmp, src)
print(f"{len(classes)} classes, {len(rows)} linhas")
