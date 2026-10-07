"""Compare filtered/synchronous and bounded/async APKs after the build is idle."""
import argparse
import json
from pathlib import Path
from statistics import median

parser = argparse.ArgumentParser()
parser.add_argument('directory', type=Path)
parser.add_argument('--out', type=Path)
args = parser.parse_args()
variants = ['filtered', 'async']
phases = ['vanilla', 'mixed', 'custom-marker', 'custom-direct', 'custom-normal', 'custom-burst', 'custom-ignore']
raw = {}
sha = {}

for variant in variants:
    paths = sorted(args.directory.glob(variant + '-[123].json'))
    assert len(paths) == 3, (variant, len(paths))
    raw[variant] = [json.loads(file.read_text(encoding='utf-8')) for file in paths]
    hashes = {run['sha256'] for run in raw[variant]}
    assert len(hashes) == 1, hashes
    sha[variant] = hashes.pop()
    for run in raw[variant]:
        assert [row['phase'] for row in run['results']] == phases
        for row in run['results']:
            assert row['samples'] == row['batchMs']['n'] == row['updateMs']['n'] == 180
            assert row['activeNpcs'] == 32 and row['musicPlaying']
            assert row['js'] == row['requestedMarker']
            if variant == 'async':
                assert row['queueAfter']['pending'] <= 32
                assert row['queueAfter']['highWater'] <= 32
                assert row['queueAfter']['retained'] <= 256
                assert row['queueAfter']['backendErrors'] == 0

result = {'apkSha256': sha, 'scope': 'Three runs per APK, 180 update batches per phase. Dispatch wait excludes SoundPool call/device output latency.',
          'phases': {}, 'raw': raw}
for phase in phases:
    result['phases'][phase] = {}
    for variant in variants:
        rows = [next(row for row in run['results'] if row['phase'] == phase) for run in raw[variant]]
        summary = {}
        for key in ['batchMs', 'updateMs', 'frameGapMs']:
            summary[key] = {stat: median(row[key][stat] for row in rows) for stat in ['mean', 'p50', 'p95', 'p99']}
            summary[key]['meanRange'] = [min(row[key]['mean'] for row in rows), max(row[key]['mean'] for row in rows)]
        summary['jsMsPerUpdate'] = median(row['jsMs'] / 180 for row in rows)
        summary['requestedCustom'] = rows[0]['requestedMarker'] + rows[0]['requestedDirect']
        summary['elapsedMs'] = median(row['elapsedMs'] for row in rows)
        if variant == 'async':
            counters = ['accepted', 'rejected', 'started', 'failed', 'expired', 'cancelled', 'backendMs']
            summary['queue'] = {key: median(row['queueAfter'][key] - row['queueBefore'][key] for row in rows) for key in counters}
            bins = [sum(row['queueAfter']['waitBins'][i] - row['queueBefore']['waitBins'][i] for row in rows) for i in range(7)]
            summary['queue']['dispatchWaitBinsAllRuns'] = bins
            target = sum(bins) * .95
            cumulative = 0
            for bound, count in zip([1, 2, 5, 10, 20, 50, None], bins):
                cumulative += count
                if cumulative >= target:
                    summary['queue']['dispatchWaitP95UpperMs'] = bound if sum(bins) else 0
                    break
            summary['queue']['pendingAtEnd'] = [row['queueAfter']['pending'] for row in rows]
        result['phases'][phase][variant] = summary
        print(phase, variant, 'batch', round(summary['batchMs']['mean'], 4), 'p95', round(summary['batchMs']['p95'], 4),
              'update', round(summary['updateMs']['mean'], 4), 'draw', round(summary['frameGapMs']['mean'], 3), summary.get('queue', ''))

if args.out:
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
