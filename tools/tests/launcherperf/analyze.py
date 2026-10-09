"""Reparse preserved gfxinfo, including initial spikes outside the CSV ring buffer."""
import hashlib
import json
from pathlib import Path
from statistics import median
from metrics import frame_stats

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'build/launcherperf'
phases = ['explore-first', 'explore-scroll', 'config-first', 'detail-first', 'idle-detail']
raw = {}


def read_run(label):
    data = json.loads((OUT / (label + '.json')).read_text(encoding='utf-8'))
    data['label'] = label
    assert [r['phase'] for r in data['results']] == phases
    for row in data['results']:
        path = OUT / (label + '-' + row['phase'] + '.gfx.txt')
        row.update(frame_stats(path.read_text(encoding='utf-8')))
        row['gfxSha256'] = hashlib.sha256(path.read_bytes()).hexdigest()
        assert row['n'] > 0 and row['totalRendered'] > 0
    return data


for variant in ['before', 'after']:
    raw[variant] = []
    for suffix in ['1', '2', '3', 'stress']:
        raw[variant].append(read_run(variant + '-' + suffix))

summary = {}
for phase in phases:
    summary[phase] = {}
    for variant in raw:
        rows = [next(r for r in run['results'] if r['phase'] == phase) for run in raw[variant][:3]]
        metrics = ['pssKb', 'p95Ms', 'meanMs', 'histogramOver50Ms', 'histogramMaxBucketMs']
        summary[phase][variant] = {key: median(r[key] for r in rows) for key in metrics}
        summary[phase][variant]['pssRangeKb'] = [min(r['pssKb'] for r in rows), max(r['pssKb'] for r in rows)]
    print(phase, summary[phase])

result = {
    'date': '2026-10-07', 'device': '127.0.0.1:16384', 'resolution': '1600x900, 240 dpi',
    'buildType': 'debug',
    'apkSha256': {key: hashlib.file_digest((OUT / (name + '.apk')).open('rb'), 'sha256').hexdigest()
                  for key, name in [('before', 'baseline'), ('after', 'candidate')]},
    'mainScope': 'Three consecutive runs per APK on the same restored 19-mod storefront, HTTPS rejected during timed runs; offline cached behavior.',
    'stressScope': 'One separate diagnostic per APK: 40 synthetic mods, each with a 512x512 ten-frame GIF. Not a real catalog workload.',
    'method': 'Median of per-run values. CSV framestats is a last-120-frame ring buffer; aggregate histogram includes entry spikes. Histogram buckets are approximate, not exact maxima. PSS is sampled after each phase, not peak memory.',
    'tests': {'junit': 106, 'newCacheTests': 7, 'newGifTests': 4, 'newStoreObserverTests': 3, 'failures': 0},
    'summary': summary, 'raw': raw,
}
result['crossCheck'] = {
    'reason': 'Two candidate runs had worse CSV p95. Extra candidate-baseline-candidate runs investigate reproducibility; kept separate from the original medians.',
    'runs': [read_run(label) for label in ['after-check1', 'before-check', 'after-check2']
             if (OUT / (label + '.json')).exists()],
}
for key, filename in [('buildVerification', 'candidate-build.json'), ('junitVerification', 'junit.json'),
                      ('onlineValidation', 'online-validation.json'), ('restoration', 'restoration.json')]:
    metadata = OUT / filename
    if metadata.exists():
        result[key] = json.loads(metadata.read_text(encoding='utf-8'))
path = Path(__file__).with_name('measurements.json')
path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('Saved', path)
