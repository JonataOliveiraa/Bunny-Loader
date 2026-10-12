# Launcher loading and stutters

## Analysis

Report: a spike when opening Explore after updating the APK, plus stutters on
other screens. The review includes Explore, Home, the package page and
Settings. The phone model was not provided; Android measurements are from the
emulator, not a promise of identical times on the device.

Facts found in the previous code:

- The old index path downloaded the manifest **and the icon** of every old
  mod, even off screen. Only the manifest is needed to list them.
- Each downloaded file replaced the remote list and incremented a global
  version, invalidating search/sorting and other mods' pages.
- There was synchronous decoding/reading in avatars, previews, Markdown, the
  profile and the character/world list. The page's tabs were read and parsed
  during composition.
- GIFs could keep about 16 MB of pixels each, plus the copy in bitmaps; the
  static and animated caches had 24 MB each. Several GIFs could be decoded at
  the same time. Failures were not remembered.
- Computing the embedded package's size allocated each whole file. Remote
  entries walked the showcase recursively, even though the index already gave
  the package's size.

Hypothesis: limiting allocation/concurrency and removing I/O from composition
should reduce spikes. It is assessed through behavior tests and separate
measurements of the real cache and of synthetic GIFs. Long frames on entry
require looking at the aggregated histogram, besides the final window's
percentiles.

Cases covered: concurrent requests for the same image, mixed formats,
repeated failure, an exception in the loader, LRU, byte/entry budget, a
corrupted GIF, transparency/disposal/interlacing, too many frames, a file
update without a change in the paths, a change in the package's hash,
consumers of other uids and the quick start preference during loading.

## Design and implementation

| Alternative | Cost and maintenance |
|---|---|
| Just move each operation to IO | The UI stops waiting, but many jobs and allocations can still compete; does not fix the global invalidation |
| Replace the pipeline with an external library | Could solve caching/loading, but adds a dependency and requires adapting GIF/pixel art and the package's file rules |
| Limit and reuse the existing pipeline | Chosen: a testable production cache, a dispatcher with two tasks, per-uid state and asynchronous reads; keeps the existing formats and paths |

`MediaCache` shares 24 MB and at most 256 entries between images and
animations; the LRU lookup is O(1) amortized, and admission can evict entries
until it fits the budget. Requests for the same key share the decoding. Null
results are also kept; the key includes the file's revision and the intended
size. Values above the budget do not evict the useful cache.

`MediaDispatcher` admits two blocking media tasks at a time. That avoids
tying up an IO thread per image waiting on the cache's semaphore. The cache
also limits itself to two decodings in case it is called from another thread.
Bitmaps use bounds reading and inSampleSize; icons up to 256 px, avatars up to
192 px and page images up to 1,024 px on the longest side. The textures are
prepared before the result is published to the UI.

Icon GIFs accept up to 4 MB of input, output up to 256 px on the longest side,
a million retained pixels (~4 MB) and 256 frames. The source processing budget
stays at four million pixels and a side of 512. Composition/disposal happens on
the original canvas before sampling. Animations that exceed the budget may end
early; keeping every frame of arbitrarily long files is not promised.

`StoreMedia` uses an independent observable cell per uid. A single
SnapshotStateMap would still invalidate readers of other keys; the test
observes the real consumers with SnapshotStateObserver. The per-uid revision
changes even when only text/photo arrives and the icon and cover paths stay
the same. An old hash cannot replace the new entry. The catalog/search list is
not replaced when images arrive.

Reading pages, inline images, avatars and previews moved out of composition;
horizontal previews use LazyRow. The profile and the saves are loaded off the
UI. Quick start only fixes preferences after the list finishes loading. The
indicator's rotation reads the state in the graphics layer, without
recomposing every frame. Old metadata and visible files get two network slots
on each path; installing packages keeps its previous flow.

The cache budget does not bound the app's whole PSS: visible references,
temporary buffers, GPU textures, fonts and the runtime also consume memory. A
very long Markdown page can still cost composition and layout; removing
parsing/I/O does not eliminate every UI cost.

## JVM tests and build

```powershell
gradle :app:testDebugUnitTest :app:assembleDebug --console=plain
```

106 tests: 92 existing and 14 new, seven for the cache, four for GIF and
three for per-uid observation. The cache tests use real threads/latches; the
GIF ones compare the reduced output with the composition already validated
against Pillow. They do not simulate Android's speed nor the GPU's graphics
output.

## Android measurement

`device.py` requires ADB/su, the launcher closed and preparation copies in
`build/launcherperf`. It does not start GameActivity. This run was prepared
with tar backups of `shared_prefs` and `cache/store`, the index in
`index.original.json` and a home screen hierarchy in `initial.xml`. The full
APKs are `baseline.apk` and `candidate.apk` in that folder. Do not replace the
backups while a preparation is in use.

The backups can be obtained with `adb exec-out run-as com.bunnyloader tar
-cf - shared_prefs` and the equivalent for `cache/store`, writing the binary
stdout directly, without text conversion by PowerShell. The XML comes from
`uiautomator dump`; it provides the controls' coordinates, instead of
assuming positions. On multiple displays, the runner moves the launcher's task
to display 0 before the gestures. This run's resolution was 1,600 × 900, at
240 dpi.

```powershell
# Install the baseline before these calls:
python tools/tests/launcherperf/device.py run --label before-1
python tools/tests/launcherperf/device.py run --label before-2
python tools/tests/launcherperf/device.py run --label before-3
python tools/tests/launcherperf/device.py run --label before-stress --stress
# Install the candidate and repeat with labels after-1/2/3/stress.
python tools/tests/launcherperf/analyze.py
python tools/tests/launcherperf/device.py restore
```

This run had an extra cross-check in the order candidate, previous,
candidate. Install the matching APK before each command:

```powershell
python tools/tests/launcherperf/device.py run --label after-check1
python tools/tests/launcherperf/device.py run --label before-check
python tools/tests/launcherperf/device.py run --label after-check2
```

`analyze.py` includes those rounds separately when present. The extra build,
JUnit, online check and restoration records in `build/launcherperf/*.json` are
optional; they document this run's checks and are not fabricated by the
analyzer. `RESULTADOS.md` holds the manual interpretation of the data; it is
not regenerated by the analyzer.

The rounds temporarily reject HTTPS on the emulator, restoring the rule in
`finally`; they do not change Wi-Fi/mobile data. This kernel does not offer
the iptables `owner` match, so the temporary port 443 block covers the whole
emulator. `network.blocked.json` allows undoing the rules after an
interruption. `restore` also removes that rule before restoring the files. Use
only one runner per emulator and run restore when done.

The main comparison uses the same 19-mod cache, with no network variation;
three consecutive rounds per APK, not alternated. Each launch restores the
preferences from before the installation, including the APK's mark, to
exercise the preparation after an update. Measured: entering Explore, 16
scroll gestures, Settings, the Example Mod page and idling on the page. Builds
did not run alongside valid measurements.

The separate diagnosis generates 40 local test mods with 512 × 512 GIFs and
ten frames; the packages are not installed. It does not represent the public
catalog and is not combined with the main median.

The online check was done after the measurements, with HTTPS allowed:
restore the backups, empty only `cache/store`, restore the index/ETag from the
tar, open Explore and inventory the downloaded files. Then open a remote page,
check text/cover/avatar, visit Settings, go back and pull to refresh.
Hierarchies, screenshots and inventories are in `build/launcherperf/online-*`;
the checked results are in `online-validation.json`. Always finish with
`device.py restore`.

Gfxinfo records a histogram and a ring buffer of up to 120 frames; the
ring buffer at the end may miss the initial stutter. `metrics.py` accepts the
CSV headers of different Android versions and keeps both results. The
histogram buckets are approximations, not exact maximums. PSS is sampled after
each phase; it is not a peak measurement. Do not use the emulator's invalid GPU
timestamps (`GpuCompleted=-1`) to promise physical FPS/latency.

Results and limitations: [RESULTADOS.md](RESULTADOS.md) and
[measurements.json](measurements.json). The gfxinfo, meminfo, logcat,
screenshot files and full backups live in `build/launcherperf`.

The decisions follow the primary documentation on
[bitmap optimization](https://developer.android.com/develop/ui/compose/graphics/images/optimization)
and [state reads in Compose](https://developer.android.com/develop/ui/compose/performance/bestpractices).
