# Documentation generators

## API reference (C#)

[`ApiReference/`](ApiReference) writes the API reference pages of the wiki,
`wiki/reference/API-*.md`, into the wiki clone at `wiki/` (it refuses to run
without one). For every public class of Bunny Loader's C# API it says whether
tModLoader has the same member, and it takes the descriptions from
tModLoader's code documentation, so it needs the tModLoader checkout at
`exmod_tmod/` and the decompiled source of Terraria for PC:

```powershell
cd tools/docs/ApiReference
dotnet run -c Release -- "<repository>" "<Terraria PC source folder>"
```

The generated pages are never edited by hand. The text that is not in
tModLoader goes in the generator's input files:

- [`intro.md`](ApiReference/intro.md): the top of the reference's index page.
- [`start.txt`](ApiReference/start.txt): the classes a mod is built with
  first, in groups, with one line each, shown at the top of the index.
- [`notes.txt`](ApiReference/notes.txt): a description for a class or member
  (`key|text`), a note about the phone (`key~|text`) or the reason something
  is missing (`key!|text`).

Then check and publish the wiki as [`tools/README.md`](../README.md#wiki)
explains.

## JavaScript API catalog (retired)

`generate.mjs`, `catalog.json`, `workbook.mjs`, `update_xlsx.py`,
`verify.py` and the npm files generated the catalog of the JavaScript API,
which was removed in `abf48278`. They no longer run against the current
code. Their last output is kept in
[`docs/historico/referencia-js/`](../../docs/historico/referencia-js).
