"""Launcher A/B on a restored storefront cache, with gfxinfo and screenshots.

Prepare by saving shared_prefs.original.tar, cache-store.original.tar and
initial.xml in build/launcherperf (see README). Never starts GameActivity.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path
import re
import subprocess
import tarfile
import time
import uuid
import xml.etree.ElementTree as ET
from metrics import frame_stats

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'build/launcherperf'
PACKAGE = 'com.bunnyloader'
ADB = str(Path.home() / 'AppData/Local/Android/Sdk/platform-tools/adb.exe')
parser = argparse.ArgumentParser()
parser.add_argument('action', choices=['run', 'restore'])
parser.add_argument('--device', default='127.0.0.1:16384')
parser.add_argument('--label')
parser.add_argument('--stress', action='store_true')
args = parser.parse_args()


def adb(*parts, data=None, check=True):
    return subprocess.run([ADB, '-s', args.device, *parts], input=data,
                          capture_output=True, check=check, timeout=180).stdout


def shell(*parts, **kwargs):
    return adb('shell', *parts, **kwargs).decode().strip()


def restore_files():
    shell('am', 'force-stop', PACKAGE)
    # Fixed app-private cache targets only; originals are kept on the host.
    shell('run-as', PACKAGE, 'rm', '-rf', 'cache/store')
    for name in ['shared_prefs', 'cache-store']:
        adb('exec-in', 'run-as', PACKAGE, 'tar', '-xf', '-',
            data=(OUT / (name + '.original.tar')).read_bytes())


def deny_network(deny):
    marker = OUT / 'network.blocked.json'
    if marker.exists():
        for table in json.loads(marker.read_text(encoding='utf-8')):
            shell('su', '0', table, '-D', 'OUTPUT', '-p', 'tcp', '--dport', '443', '-j', 'REJECT')
        marker.unlink()
    if deny:
        applied = []
        for table in ['iptables', 'ip6tables']:
            proc = subprocess.run([ADB, '-s', args.device, 'shell', 'su', '0', table,
                                   '-I', 'OUTPUT', '-p', 'tcp', '--dport', '443', '-j', 'REJECT'], capture_output=True)
            if proc.returncode == 0:
                applied.append(table)
                marker.write_text(json.dumps(applied), encoding='utf-8')
        assert 'iptables' in applied, 'Cannot isolate HTTPS for this emulator test.'


def on_default_display():
    stacks = shell('am', 'stack', 'list')
    for section in re.split(r'(?=RootTask id=)', stacks):
        if 'topActivity=ComponentInfo{com.bunnyloader/dev.bunnyloader.LauncherActivity}' in section:
            root = re.search(r'RootTask id=(\d+)', section).group(1)
            shell('am', 'display', 'move-stack', root, '0')
            return
    raise RuntimeError('Launcher root task missing')


def center(bounds):
    x1, y1, x2, y2 = map(int, re.findall(r'\d+', bounds))
    return ((x1 + x2) // 2, (y1 + y2) // 2)


def navigation():
    tree = ET.parse(OUT / 'initial.xml')
    return {label: center(next(n for n in tree.iter('node') if n.get('text') == label).get('bounds'))
            for label in ['Início', 'Explorar', 'Pacotes', 'Config', 'Example Mod']}


def tap(point):
    shell('input', '-d', '0', 'tap', *map(str, point))


def stress_fixture():
    from PIL import Image
    template = json.loads((OUT / 'index.original.json').read_text(encoding='utf-8'))['mods'][0]
    frames = [Image.new('RGB', (512, 512), (30 + i * 15, 80, 140 - i * 8)) for i in range(10)]
    gif = io.BytesIO()
    frames[0].save(gif, format='GIF', save_all=True, append_images=frames[1:], duration=100, loop=0, optimize=False)
    archive = io.BytesIO()
    mods = []
    with tarfile.open(fileobj=archive, mode='w') as tar:
        def add(path, data):
            info = tarfile.TarInfo('cache/store/' + path)
            info.size = len(data)
            info.mtime = 1_800_000_000
            tar.addfile(info, io.BytesIO(data))
        for i in range(40):
            mod = json.loads(json.dumps(template))
            uid = str(uuid.uuid5(uuid.NAMESPACE_URL, 'bunny-loader/media-benchmark/' + str(i)))
            mod['uid'] = uid
            mod['files'] = ['manifest.json', 'icon.gif', 'description.md']
            mod['manifest'].update(uid=uid, id='media-probe-' + str(i), name=f'A{i:02d} Media probe',
                                   authors=[], description='Teste de carregamento de mídia.', summary='Fixture local de performance.')
            mods.append(mod)
            add(uid + '/.sha256', mod['download']['sha256'].lower().encode())
            add(uid + '/manifest.json', json.dumps(mod['manifest']).encode())
            add(uid + '/icon.gif', gif.getvalue())
            add(uid + '/description.md', ('# Teste\n\n' + 'Descrição com **texto** e transparência.\n\n' * 20).encode())
        add('index.json', json.dumps({'format': 1, 'mods': mods}).encode())
    adb('exec-in', 'run-as', PACKAGE, 'tar', '-xf', '-', data=archive.getvalue())


def capture(name, operation):
    shell('dumpsys', 'gfxinfo', PACKAGE, 'reset')
    operation()
    text = shell('dumpsys', 'gfxinfo', PACKAGE, 'framestats')
    (OUT / (args.label + '-' + name + '.gfx.txt')).write_text(text, encoding='utf-8')
    mem = shell('dumpsys', 'meminfo', PACKAGE)
    (OUT / (args.label + '-' + name + '.mem.txt')).write_text(mem, encoding='utf-8')
    result = frame_stats(text)
    result['phase'] = name
    match = re.search(r'TOTAL PSS:\s+(\d+)', mem)
    result['pssKb'] = int(match.group(1)) if match else None
    print(args.label, result, flush=True)
    return result


if args.action == 'restore':
    deny_network(False)
    restore_files()
    for name in ['shared_prefs', 'cache-store']:
        with tarfile.open(OUT / (name + '.original.tar')) as tar:
            for member in tar.getmembers():
                if not member.isfile():
                    continue
                expected = tar.extractfile(member).read()
                actual = adb('exec-out', 'run-as', PACKAGE, 'cat', member.name)
                assert hashlib.sha256(actual).digest() == hashlib.sha256(expected).digest(), member.name
    print('Original preferences and storefront files verified byte for byte.', flush=True)
else:
    assert args.label and re.fullmatch(r'[a-z0-9-]+', args.label)
    assert not shell('pidof', PACKAGE + ':game', check=False), 'Close the game first.'
    restore_files()
    deny_network(True)
    try:
        if args.stress:
            stress_fixture()
        adb('logcat', '-c')
        launch = shell('am', 'start', '-W', '-n', PACKAGE + '/dev.bunnyloader.LauncherActivity')
        on_default_display()
        nav = navigation()
        # Includes cold UI/decode work after the first draw; launch time kept separately.
        rows = [capture('explore-first', lambda: (tap(nav['Explorar']), time.sleep(4)))]
        def scroll():
            for _ in range(8):
                shell('input', '-d', '0', 'swipe', '900', '650', '900', '330', '250')
                time.sleep(.15)
            for _ in range(8):
                shell('input', '-d', '0', 'swipe', '900', '330', '900', '650', '250')
                time.sleep(.15)
        rows.append(capture('explore-scroll', scroll))
        rows.append(capture('config-first', lambda: (tap(nav['Config']), time.sleep(4))))
        tap(nav['Início'])
        time.sleep(2)
        rows.append(capture('detail-first', lambda: (tap(nav['Example Mod']), time.sleep(4))))
        rows.append(capture('idle-detail', lambda: time.sleep(4)))
        (OUT / (args.label + '.png')).write_bytes(adb('exec-out', 'screencap', '-p'))
        runtime = adb('logcat', '-d').decode(errors='replace')
        (OUT / (args.label + '.logcat.txt')).write_text(runtime, encoding='utf-8')
        assert 'FATAL EXCEPTION' not in runtime
        (OUT / (args.label + '.json')).write_text(json.dumps({'stress': args.stress, 'launch': launch, 'results': rows}, indent=2), encoding='utf-8')
    finally:
        deny_network(False)
        shell('am', 'force-stop', PACKAGE)
