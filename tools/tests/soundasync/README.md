# Bounded dispatch of custom effects

## Analysis

Input: SoundStyle requests, including UseSound/HitSound/DeathSound and the
direct API. Output: a queryable identifier, a start confirmed by Android's
return or a terminal state, without waiting for SoundPool on the game thread.

Facts measured in the previous step: the filter eliminates the vanilla entries
in QuickJS; in a diagnostic round, about 90–93% of the custom batches' time
was inside bl.sounds.play. That interval includes JNI and waiting on Android;
it does not measure pure QuickJS CPU.

Hypotheses checked in this step: removing that wait should reduce the time of
the batches and the updates; a queue needs limits and must drop old effects so
as not to turn stutters into late audio.

Cases considered: loading/file unavailable, a full queue, backend 0 or an
exception, invalid pitch, distinct groups, ReplaceOldest, IgnoreNew,
cancelling a pending or in-progress request, a concurrent pause, resuming
after a long pause, the global stream limit and retaining thousands of
requests.

## Design

| Alternative | Time and space | Assessment |
|---|---|---|
| Limit instances in JS and stay synchronous | Per-request work proportional to the style's instances; the backend still blocks the caller | Reduces calls, but changes how many sounds each mod asks for and keeps the bottleneck |
| An executor with a task/cleanup per request | The queue and timers grow with the number of requests; needs to synchronize tokens and lifecycle | The original suggestion had failure, pause and task buildup regressions |
| A single worker with bounded history and pending requests | Active lookup O(L), L ≤ 64 requests/streams in progress; history H ≤ 256; up to 32 pending including the native call; O(H + L) memory | Implemented in SfxQueue, separate from Android for deterministic tests |

The worker calls play/stop/autoPause/autoResume outside the producer's lock.
It runs no JS. The limits count pending requests, the duration starts after
play's positive return and uses a monotonic clock frozen during the applied
pause. A cancellation during play keeps the record until the returned stream
can be stopped. The requests in progress are separate from the history:
IgnoreNew does not scan 256 finished records on each refusal. There is no
ScheduledFuture per effect nor an unbounded generic queue.

Requests that waited more than 50 ms are dropped before the Android call. That
bounds the wait for dispatch, not SoundPool.play's duration nor the speaker's
physical latency. The backend keeps 32 streams. Under saturation there are
refusals, drops and preemption; the results record those costs. The end of a
stream is still an estimate from duration/pitch, because SoundPool offers no
per-stream position/end query.

SoundEngine's contract was updated explicitly: PlaySound returns an accepted
request, GetSoundState tells pending/started/failed apart, StopSound also
cancels pending requests and FindActiveSound excludes pending/failed/paused
ones. The low-level bl.sounds.play/stop stays synchronous and uses Android
streams; do not mix those streams up with SoundEngine's requests. Using it
directly stays outside the queue's limit control. Music keeps the previous
path.

## Tests

```powershell
node tools/tests/soundfilter/check.mjs
# With the project's JDK/Gradle:
gradle :app:testDebugUnitTest :app:assembleDebug --console=plain
```

SfxQueueTest tests the production code with a controlled clock and backend.
It includes a real worker blocked while another producer makes a thousand
requests and cancels. The doubles check states and ordering, without claiming
to measure Android audio or device latency.

## Android A/B

The shared runner uses --suite soundasync to keep the results separate from
the filter step. It requires ADB with su/run-as and a closed game session on
prepare. It uses copies of the saves and restores the preferences and the
original files' hashes, as in the soundfilter README.

Before the build, keep the APK with only the native filter at
build/soundasync/baseline.apk. Build the full candidate and do not run heavy
builds/renders during the main rounds.

```powershell
python tools/tests/soundfilter/device.py prepare --suite soundasync
python tools/tests/soundfilter/device.py run --suite soundasync --variant filtered --label filtered-1 --apk build/soundasync/baseline.apk
python tools/tests/soundfilter/device.py run --suite soundasync --variant async --label async-1 --apk app/build/outputs/apk/debug/app-debug.apk
# Complete three rounds per variant; compare.py uses A/B/B/A/A/B.
python tools/tests/soundasync/analyze.py build/soundasync/127.0.0.1-16384 --out build/soundasync/summary.json
# Extra diagnostics, with separate labels:
python tools/tests/soundfilter/device.py run --suite soundasync --variant async --label lifecycle-async --lifecycle --apk app/build/outputs/apk/debug/app-debug.apk
python tools/tests/soundfilter/device.py restore --suite soundasync
```

Each round reuses 32 harmless NPCs, custom music, four 200 ms WAVs and the
previous step's four phases. It adds custom-normal (one request/update),
custom-burst (48 requests every 30 updates) and custom-ignore (12
requests/update, MaxInstances 1/IgnoreNew). --profile adds the timing
wrappers. --lifecycle covers the game Activity for six seconds during a
five-second effect, resumes the same Activity and checks the identifier and
the pause duration recorded by the worker.

On emulators with multiple displays, HOME may hit another display and leave
the game running. The runner identifies the game's root task/display, opens
the launcher and, if needed, moves its root task to the same display. It
checks that GameActivity left the resumed state before counting the pause. It
does not change the production lifecycle to make the test possible.

`compare.py --diagnostics` automates three rounds per APK and the
diagnostics, restoring the environment in `finally`. `--prepared --resume`
continues an existing preparation, checking each APK's hash in the reused
rounds.

To repeat only the four-requests-per-update diagnostic:

```powershell
python tools/tests/soundasync/compare.py --moderate-only --candidate build/soundasync/candidate.apk
```

That option prepares new copies, runs one A/B pair with `--profile
--normal-rate 4 --only-normal` and restores the environment. `--normal-rate`
accepts 1 to 12 requests; `--only-normal` runs only that phase and requires
`--suite soundasync`. Four requests per update still exceed 32 voices if every
200 ms effect is kept at 60 updates per second. The recorded pair is
diagnostic, without repeating the main rounds. The full profiles recorded in
this run used one request per update in the normal phase; new `--diagnostics`
runs use four.

The summary shows medians of three rounds, ranges, queue counters and a
histogram of the wait before dispatch. Started/failed/expired can cross the
phase boundaries: use pendingAtEnd to assess work still in progress. Do not
confuse SoundPool.play's confirmation with measuring physical output. In the
profile, `failed` counts the transport's zero returns: in the synchronous
variant they are play failures; in the asynchronous one they are admission
refusals. The asynchronous start failure is `queue.failed`. See RESULTADOS.md
and measurements.json for the run.
