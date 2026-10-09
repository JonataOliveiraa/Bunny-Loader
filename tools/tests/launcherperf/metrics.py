"""Parse gfxinfo without assuming the Android-version-specific CSV column order."""
import re


def frame_stats(text):
    frames = []
    fields = None
    for line in text.splitlines():
        if line.startswith('Flags,') and 'IntendedVsync' in line:
            fields = line.rstrip(',').split(',')
        elif fields and re.match(r'^\d+,', line):
            row = dict(zip(fields, map(int, line.rstrip(',').split(','))))
            if row.get('Flags') == 0:
                duration = (row['FrameCompleted'] - row['IntendedVsync']) / 1e6
                if 0 < duration < 10_000:
                    frames.append(duration)
    frames.sort()

    def percentile(q):
        return frames[min(len(frames) - 1, int((len(frames) - 1) * q))] if frames else None

    def count(pattern):
        match = re.search(pattern, text)
        return int(match.group(1)) if match else None

    histogram = re.search(r'^HISTOGRAM:\s*(.+)$', text, re.MULTILINE)
    bins = [(int(ms), int(n)) for ms, n in re.findall(r'(\d+)ms=(\d+)', histogram.group(1))] if histogram else []
    nonempty = [(ms, n) for ms, n in bins if n]
    return {
        'totalRendered': count(r'Total frames rendered:\s+(\d+)'),
        'deadlineMissed': count(r'Number Frame deadline missed:\s+(\d+)'),
        'deadlineMissedLegacy': count(r'Number Frame deadline missed \(legacy\):\s+(\d+)'),
        'histogramOver50Ms': sum(n for ms, n in bins if ms >= 53),
        'histogramMaxBucketMs': max((ms for ms, n in nonempty), default=None),
        'histogram': nonempty,
        'n': len(frames),
        'meanMs': sum(frames) / len(frames) if frames else None,
        'p50Ms': percentile(.5), 'p95Ms': percentile(.95), 'p99Ms': percentile(.99),
        'maxMs': max(frames) if frames else None,
        'over50Ms': sum(t > 50 for t in frames),
    }
