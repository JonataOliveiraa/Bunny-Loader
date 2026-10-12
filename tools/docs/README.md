# JavaScript API catalog

The catalog lists the classes published in `script/js/mod/Exports.js`.
The signatures come from a parse with [Acorn](https://github.com/acornjs/acorn).
The generator does not run the game's scripts nor use mocks to discover the API.

## Published files

- [Methods by class](../../docs/referencia/metodos.md): an index and one table per class.
- [Excel](../../docs/referencia/metodos.xlsx): Index and Methods sheets, with filters and frozen headers.
- [UTF-8 CSV](../../docs/referencia/metodos.csv): the same records in one table.
- [JSON](../../docs/referencia/metodos.json): records, classes, sources and the hash of the API files.

The inventory includes public instance and static methods, explicit
constructors, getters/setters, inheritance between the module's classes and
the NetWriter/NetReader aliases. Private `#` methods, fields, enums, native
bases like `Error`, `Terraria.*` methods and `bl.*` functions are left out of
that count. The exported objects and enums are listed separately in the index.

`Declared` means there is a declaration in the JavaScript API. That status
does not claim test coverage nor full equivalence with tModLoader. The base
return is an expression from the code, not an inferred type. Dropped methods
do not get an estimated signature.

## Updating

At the repository root, with Node.js and npm:

```powershell
npm --prefix tools/docs ci
node tools/docs/generate.mjs
node tools/docs/generate.mjs --check
python tools/docs/update_xlsx.py
python tools/docs/verify.py
```

`generate.mjs` reads the signatures and their lines straight from the code.
It extracts descriptions from the tables in `classes.md` and
`modplayer-hooks.md`; when a specific description does not exist, it uses the
adjacent comment in the source or says the implementation should be
consulted. It does not infer parameter types nor turn a method's name into a
made-up description.

`catalog.json` holds the references' anchors, compatibility notes and the
reasons for the dropped ModNPC methods. Review those notes when changing
contracts. The generator rejects duplicate classes, duplicate exports, cyclic
inheritance, duplicate rows, missing anchors, notes without a method and
present methods that are marked as not implemented.

`--check` returns an error when the Markdown, CSV or JSON differ from the
current generation. Run it after changing the API or the contracts'
documentation. `verify.py` only uses Python's standard library. It checks
every CSV and Excel record against the JSON, the index counts, the absence of
formulas, the filters and the freezing of the headers and identification
columns.

## Spreadsheet

`workbook.mjs` uses `@oai/artifact-tool`, made available by the environment's
document runtime. The runtime is not a dependency of the Android app nor of
the Markdown/CSV/JSON generator.

Prepare a working folder with `node_modules` pointing to that runtime's
dependencies. The `--runtime` parameter identifies that folder:

```powershell
node tools/docs/workbook.mjs --runtime build/api-docs --output-dir build/api-docs/outputs/catalogo
```

Use the same runtime's Node.js executable. The spreadsheet is created in the
output folder and copied to `docs/referencia/metodos.xlsx`. The previews and
inspection reports stay only in the build folder. Review the previews of both
sheets and check the exported table's rows against `metodos.json` before
versioning the Excel file. It needs no formulas or macros. Add `--preview` to
also render both sheets. On the Windows runtime used for this update, that
option saved the previews and the spreadsheet, but Node exited with a native
error. Generation without rendering finished normally; `verify.py` fully
checked the exported file.

Update the spreadsheet together with the three text files. The hash in
`metodos.json` identifies the sources of the classes and their inherited
methods; it is not an identifier of in-game test results.
