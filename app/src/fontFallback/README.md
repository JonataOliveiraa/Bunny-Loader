# Fallback font

The launcher and the cheat menu use **Andy Bold**, Terraria's font. It belongs
to Monotype and does not go into Git: whoever integrates the game binaries
also puts in `app/src/main/res/font/bunny.ttf`.

Without that file, the build uses this folder: **Comic Neue Bold**, under the
SIL Open Font License 1.1 (text in `OFL.txt`), with the same resource name
(`R.font.bunny`) and the same path in the assets (`fonte/bunny.ttf`). Nothing in
the code changes; only the look of the text.

Source: <https://github.com/crozynski/comicneue>
