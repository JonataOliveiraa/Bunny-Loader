"""Run a balanced A/B sequence and always restore isolated Android test state."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[3]
parser = argparse.ArgumentParser()
parser.add_argument('--baseline', type=Path, default=ROOT / 'build/soundasync/baseline.apk')
parser.add_argument('--candidate', type=Path, default=ROOT / 'app/build/outputs/apk/debug/app-debug.apk')
parser.add_argument('--device', default='127.0.0.1:16384')
parser.add_argument('--prepared', action='store_true')
parser.add_argument('--resume', action='store_true')
parser.add_argument('--diagnostics', action='store_true')
parser.add_argument('--moderate-only', action='store_true')
args = parser.parse_args()
out = ROOT / 'build/soundasync' / args.device.replace(':', '-')
runner = ROOT / 'tools/tests/soundfilter/device.py'
apks = {'filtered': args.baseline.resolve(), 'async': args.candidate.resolve()}
hashes = {name: hashlib.sha256(apk.read_bytes()).hexdigest() for name, apk in apks.items()}


def run(action, label=None, extra=()):
    cmd = [sys.executable, str(runner), action, '--suite', 'soundasync', '--device', args.device, *extra]
    if label:
        print('Starting ' + label, flush=True)
        with (out / (label + '.runner.log')).open('w', encoding='utf-8') as stream:
            proc = subprocess.run(cmd, cwd=ROOT, stdout=stream, stderr=subprocess.STDOUT)
        log = (out / (label + '.runner.log')).read_text(encoding='utf-8')
        print('\n'.join(line for line in log.splitlines() if ' FIM ' in line or 'FAIL ' in line), flush=True)
        if proc.returncode:
            raise RuntimeError('Run failed; inspect ' + str(out / (label + '.runner.log')))
    else:
        subprocess.run(cmd, cwd=ROOT, check=True)


try:
    if not args.prepared:
        run('prepare')
    if args.moderate_only:
        for variant in ['filtered', 'async']:
            label = 'moderate-' + variant
            run('run', label, ['--variant', variant, '--label', label, '--apk', str(apks[variant]), '--profile', '--normal-rate', '4', '--only-normal'])
    else:
        for variant, n in [('async', 1), ('filtered', 1), ('filtered', 2), ('async', 2), ('async', 3), ('filtered', 3)]:
            label = f'{variant}-{n}'
            previous = out / (label + '.json')
            if args.resume and previous.exists():
                data = json.loads(previous.read_text(encoding='utf-8'))
                assert data['sha256'] == hashes[variant] and len(data['results']) == 7
                runtime = (out / (label + '.runtime.log')).read_text(encoding='utf-8')
                assert re.search(r'FIM variant=' + variant + r' checks=[1-9]\d* falhas=0', runtime), label
                print('Using completed ' + label, flush=True)
                continue
            run('run', label, ['--variant', variant, '--label', label, '--apk', str(apks[variant])])
        subprocess.run([sys.executable, str(Path(__file__).with_name('analyze.py')), str(out), '--out', str(out.parent / 'summary.json')], cwd=ROOT, check=True)
        if args.diagnostics:
            for variant in ['filtered', 'async']:
                label = 'profile-' + variant
                run('run', label, ['--variant', variant, '--label', label, '--apk', str(apks[variant]), '--profile', '--normal-rate', '4'])
            run('run', 'lifecycle-async', ['--variant', 'async', '--label', 'lifecycle-async', '--apk', str(apks['async']), '--lifecycle'])
finally:
    if (out / 'prepared').exists():
        try:
            run('restore')
        except subprocess.CalledProcessError:
            print('Restore failed; retrying the idempotent restore/verification once.', flush=True)
            run('restore')
