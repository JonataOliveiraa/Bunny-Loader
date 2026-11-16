# Plan: online catalog on Bunny-Loader-CSharp-Mods

Status: app side implemented (2026-11-15) and tested against a local store on the MuMu 16384
beta: featured pages, Populares top 10, missing files, `blVersion: 2` filtered, real download and
install of `hellomod-2.0.0.bl`. The mods repository has `store/` committed locally, not pushed.

## Goal

The online catalog comes back, now reading
[Bunny-Loader-CSharp-Mods](https://github.com/JonataOliveiraa/Bunny-Loader-CSharp-Mods)
instead of Bunny-Loader-Mods, whose 75 packages are all `blVersion: 2` and no longer load.

The Home tab gets two curated lists, each from its own JSON in that repository:

- **Featured:** every mod listed in `featured.json`, in that order, as pages of the carousel
  (swipe, dots, auto-turn). No limit on how many.
- **Popular:** the 10 most downloaded mods, from `popular.json`, ranked 1 to 10.

## Contract with the mods repository

Everything the app reads lives under `store/` on `main`, so it never mixes with the C# sources
in `mods/<Id>/`. Packages live in Releases.

| Path | Who writes it | Contents |
|---|---|---|
| `store/index.json` | publish script | every published mod (same schema as today's `RemoteIndex`) |
| `store/featured.json` | the maintainer, by hand or `publish.py --feature` | uids shown in Featured, in order |
| `store/stats.json` | Action, every 6 h | downloads per uid (same schema as today's `RemoteStats`) |
| `store/popular.json` | same Action | top 10 uids by downloads, ranked |
| `store/mods/<uid>/...` | publish script | showcase: icon, banner, description, authors |
| Release `mod-<id>-v<version>` | publish script | the `.bl` (format 3) |

```json
// store/featured.json
{ "format": 1, "mods": ["<uid>", "<uid>"] }

// store/popular.json
{ "format": 1, "updatedAt": "2026-11-15T12:00:00Z",
  "mods": [ { "uid": "<uid>", "downloads": 1234 } ] }
```

Rules the app enforces (anything else is dropped, not an error):

- uid in the `ModManifest.isValidUid` format; unknown uids in `featured`/`popular` are skipped.
- `download.url` starts with `https://github.com/JonataOliveiraa/Bunny-Loader-CSharp-Mods/releases/download/`.
- `download.sha256` is 64 lowercase hex; showcase paths match `SAFE_PATH`.
- `manifest.blVersion` is 3 (`MIN_BL_VERSION..BL_VERSION`).
- Size caps stay: index 8 MB, stats/featured/popular 1 MB, showcase file 4 MB, package 200 MB.
- `featured.json` and `popular.json` missing (404) means an empty list, not a failed catalog.

## App changes

1. `RemoteCatalog`
   - `BASE_URL = https://raw.githubusercontent.com/JonataOliveiraa/Bunny-Loader-CSharp-Mods/main/store/`;
     index, stats, featured and popular all derive from it.
   - `ALLOWED_HOSTS` keeps only the new repository's Releases.
   - Fetch `featured.json` and `popular.json` with the same ETag cache as `stats.json`
     (one generic `cachedJson(name)` instead of three copies). Offline keeps the last copy.
   - `CatalogSnapshot` gains `popular: List<String>`; `featured` comes from `featured.json`,
     falling back to `index.featured` while the file does not exist.
   - Filter out mods whose `manifest.blVersion` is outside `MIN_BL_VERSION..BL_VERSION`.
   - `ENABLED = true`.
2. `Shell`: expose `popularUids` next to `featuredUids`.
3. `InicioTab`
   - Featured: the carousel with every featured entry (already a pager); drop the fallback to a
     local bundled mod once the online list has at least one entry.
   - Popular: `popular.json` order, `POPULAR_COUNT = 10`, rank badge on each row. Without the
     file, fall back to sorting by `stats.json` as today.
4. `ServerSync`: the shop list follows `ENABLED`, nothing else to change.
5. Cache: the old repository's `cache/store/` entries vanish on the first refresh (uids not in
   the new index are deleted).

## Out of scope here

- `tools/mods/publish.py`, `download_stats.py` and the BL Catalog app (`catalog/`) still target
  Bunny-Loader-Mods. Publishing moves to the C# repository (see the prompt for that session);
  the catalog app is retargeted later.
- Compiled packages (`plano-mod-compilado.md`): Releases carry the format 3 zip until BLPK
  lands. That plan also decided sources go to a private repository, while
  Bunny-Loader-CSharp-Mods is public with sources. Settle that before the first release.

## Tests

1. Local: serve a fake `store/` with `python -m http.server`, `adb reverse`, point `BASE_URL`
   and `ALLOWED_HOSTS` at it (see the catalog memory), cover 0, 1 and 12 featured mods, popular
   with 3 and 10 entries, missing `featured.json`/`popular.json`, and a `blVersion: 2` entry
   (must not show). Undo the URL change.
2. Real: once the mods repository publishes its first mod, install from Explore on the MuMu
   16384 beta (`BL_PKG=com.bunnyloader.beta`) and check the mod loads in game.
