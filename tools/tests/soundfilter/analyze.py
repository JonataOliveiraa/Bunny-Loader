"""Summarize repeated native A/B runs; do not treat frame timing as pure JS CPU."""
import argparse
import json
from pathlib import Path
import statistics

parser = argparse.ArgumentParser()
parser.add_argument('directory', type=Path)
parser.add_argument('--out', type=Path)
args = parser.parse_args()
groups = {}
apks = {}
for variant in ['baseline', 'filtered']:
    apks[variant] = set()
    paths = sorted(args.directory.glob(variant + '-[123].json'))
    if len(paths) != 3:
        raise SystemExit(f'Expected three completed {variant} runs; found {len(paths)}.')
    for file in paths:
        data = json.loads(file.read_text(encoding='utf-8'))
        apks[variant].add(data['sha256'])
        if len(data['results']) != 4:
            raise SystemExit('Incomplete result: ' + str(file))
        for row in data['results']:
            assert row['variant'] == variant
            assert row['samples'] == row['batchMs']['n'] == 180
            assert row['activeNpcs'] == 32 and row['musicPlaying']
            assert row['js'] == (row['requestedMarker'] if variant == 'filtered' else row['calls'])
            groups.setdefault((variant, row['phase']), []).append(row)
    if len(apks[variant]) != 1:
        raise SystemExit('Different APK bytes within variant: ' + variant)

result = {'apkSha256': {variant: list(values)[0] for variant, values in apks.items()},
          'scope': 'Median of three runs per variant; each phase contains 180 update batches. jsMs includes callback JNI/platform wait, not pure QuickJS CPU.',
          'phases': {}}

for phase in ['vanilla', 'mixed', 'custom-marker', 'custom-direct']:
    result['phases'][phase] = {}
    for variant in ['baseline', 'filtered']:
        rows = groups[(variant, phase)]
        summary = {}
        for key in ['calls', 'js', 'jsMs']:
            values = [row[key] for row in rows]
            summary[key] = {'median': statistics.median(values), 'min': min(values), 'max': max(values)}
        summary['hookMsPerUpdate'] = summary['jsMs']['median'] / 180
        for metric in ['batchMs', 'updateMs', 'frameGapMs']:
            summary[metric] = {key: statistics.median(row[metric][key] for row in rows)
                               for key in ['mean', 'p50', 'p95', 'p99']}
        result['phases'][phase][variant] = summary
        print(f"{phase:15s} {variant:8s} js={summary['js']['median']:5.0f} "
              f"hook/update={summary['hookMsPerUpdate']:.4f} ms "
              f"batch mean={summary['batchMs']['mean']:.4f} ms "
              f"p95={summary['batchMs']['p95']:.4f} ms "
              f"frame mean={summary['frameGapMs']['mean']:.3f} ms")

if args.out:
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(result, indent=2), encoding='utf-8')
