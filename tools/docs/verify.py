import csv
import json
from pathlib import Path
from xml.etree import ElementTree as ET
from zipfile import ZipFile


root = Path(__file__).resolve().parents[2]
reference = root / "docs" / "referencia"
catalog = json.loads((reference / "metodos.json").read_text(encoding="utf-8"))
namespace = {"s": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}
keys = ["class", "method", "kind", "signature", "declaredIn", "inherited", "status", "baseReturn", "description", "native", "sourceUrl"]
labels = ["Classe", "Método", "Tipo", "Assinatura", "Declarado em", "Herdado", "Status", "Retorno base", "Contrato / limite", "Método nativo / etapa", "Fonte"]
expected = [[("Sim" if row[key] else "Não") if key == "inherited" else row[key] for key in keys] for row in catalog["rows"]]
with (reference / "metodos.csv").open(encoding="utf-8-sig", newline="") as stream:
    csv_rows = list(csv.reader(stream))
if csv_rows != [labels] + expected:
    raise ValueError("CSV difere do JSON")

with ZipFile(reference / "metodos.xlsx") as archive:
    strings_file = "xl/sharedStrings.xml"
    strings = []
    if strings_file in archive.namelist():
        strings_tree = ET.fromstring(archive.read(strings_file))
        strings = ["".join(item.itertext()) for item in strings_tree.findall("s:si", namespace)]

    def rows(sheet_file):
        tree = ET.fromstring(archive.read(sheet_file))
        result = {}
        for row in tree.findall("s:sheetData/s:row", namespace):
            values = {}
            for cell in row.findall("s:c", namespace):
                value_node = cell.find("s:v", namespace)
                value = value_node.text if value_node is not None else ""
                if cell.get("t") == "s":
                    value = strings[int(value)]
                elif cell.get("t") == "inlineStr":
                    value = "".join(cell.find("s:is", namespace).itertext())
                if cell.find("s:f", namespace) is not None:
                    raise ValueError("Fórmula inesperada na planilha de consulta")
                values[cell.get("r")] = value or ""
            result[int(row.get("r"))] = values
        return tree, result

    workbook = ET.fromstring(archive.read("xl/workbook.xml"))
    names = [sheet.get("name") for sheet in workbook.findall("s:sheets/s:sheet", namespace)]
    if names != ["Índice", "Métodos"]:
        raise ValueError("Abas inesperadas: " + str(names))
    methods_tree, actual_rows = rows("xl/worksheets/sheet2.xml")
    columns = "ABCDEFGHIJK"
    for number, expected_row in enumerate(expected, start=7):
        actual = [actual_rows.get(number, {}).get(column + str(number), "") for column in columns]
        if actual != expected_row:
            raise ValueError(f"Linha de métodos diferente: {number}")
    if max(actual_rows) != len(expected) + 6:
        raise ValueError("Quantidade de linhas diferente")
    pane = methods_tree.find("s:sheetViews/s:sheetView/s:pane", namespace)
    if pane is None or pane.get("xSplit") != "2" or pane.get("ySplit") != "6":
        raise ValueError("Os cabeçalhos e as duas colunas de identificação devem ficar fixos")
    table = ET.fromstring(archive.read("xl/tables/table2.xml"))
    if table.get("ref") != f"A6:K{len(expected) + 6}" or table.find("s:autoFilter", namespace) is None:
        raise ValueError("Filtro não cobre a tabela de métodos")
    index_tree, index_rows = rows("xl/worksheets/sheet1.xml")
    for number, definition in enumerate(catalog["classes"], start=7):
        actual = [index_rows[number].get(column + str(number), "") for column in "ABCDE"]
        target = [definition["name"], definition["base"] or "—", str(definition["own"]), str(definition["inherited"]), str(definition["own"] + definition["inherited"])]
        if actual != target:
            raise ValueError(f"Índice diferente: {number}")
print(f"CSV e Excel conferidos: {len(catalog['classes'])} classes, {len(expected)} registros, filtros e painéis fixos.")
