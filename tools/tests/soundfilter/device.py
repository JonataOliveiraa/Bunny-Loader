"""A/B audio benchmark on save copies. Run prepare, run (...), then restore.

Uses ADB without screen-coordinate input. Restores preferences byte for byte and
checks original saves. Generated audio/images and APKs remain under build/.
"""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import re
import shlex
import shutil
import struct
import subprocess
import time
import wave
import xml.etree.ElementTree as ET
import zlib

ROOT = Path(__file__).resolve().parents[3]
FIXTURE = Path(__file__).resolve().parent
parser = argparse.ArgumentParser()
parser.add_argument('action', choices=['prepare', 'run', 'restore'])
parser.add_argument('--device', default='127.0.0.1:16384')
parser.add_argument('--variant', choices=['baseline', 'filtered', 'async'], default='filtered')
parser.add_argument('--suite', choices=['soundfilter', 'soundasync'], default='soundfilter')
parser.add_argument('--label', default='filtered-1')
parser.add_argument('--apk', type=Path)
parser.add_argument('--profile', action='store_true')
parser.add_argument('--lifecycle', action='store_true')
parser.add_argument('--normal-rate', type=int, choices=range(1, 13), default=1)
parser.add_argument('--only-normal', action='store_true')
args = parser.parse_args()
if args.only_normal and args.suite != 'soundasync':
    parser.error('--only-normal requires --suite soundasync')
if not re.fullmatch(r'[\w.:-]+', args.device) or not re.fullmatch(r'[\w-]+', args.label):
    parser.error('invalid device or label')

ADB = shutil.which('adb') or str(Path(os.environ['LOCALAPPDATA']) / 'Android/Sdk/platform-tools/adb.exe')
UID = json.loads((FIXTURE / 'manifest.json').read_text(encoding='utf-8'))['uid']
OUT = ROOT / ('build/' + args.suite) / args.device.replace(':', '-')
OUT.mkdir(parents=True, exist_ok=True)
BASE = '/sdcard/Android/data/com.bunnyloader'
PACK = BASE + '/bunny_packs/' + UID
STAGING = '/data/local/tmp/bl-soundfilter'
CATEGORIES = ['settings', 'mods', 'bunny_powers']
SAVE_NAME = 'BL_SoundFilter_Test'


def adb(*values):
    proc = subprocess.run([ADB, '-s', args.device, *map(str, values)], capture_output=True)
    if proc.returncode:
        raise RuntimeError(proc.stdout.decode(errors='replace') + proc.stderr.decode(errors='replace'))
    return proc.stdout


def shell(*values):
    return adb('shell', shlex.join(map(str, values)))


def root(*values):
    return shell('su', '0', *values)


def activity_root(component):
    stacks = root('am', 'stack', 'list').decode(errors='replace')
    for block in re.split(r'(?=RootTask id=)', stacks):
        if 'topActivity=ComponentInfo{' + component + '}' in block:
            match = re.search(r'RootTask id=(\d+).*?displayId=(\d+)', block)
            if match:
                return match.group(1), match.group(2)
    raise RuntimeError('Cannot locate the Activity root/display: ' + component)


def write_pref(category, data):
    local = OUT / (category + '.prepared.xml')
    local.write_bytes(data)
    adb('push', local, STAGING + '/' + category + '.xml')
    shell('run-as', 'com.bunnyloader', 'cp', STAGING + '/' + category + '.xml', 'shared_prefs/' + category + '.xml')


def save_hashes():
    result = {}
    for folder in ['Players', 'Worlds']:
        for name in shell('ls', BASE + '/' + folder).decode().splitlines():
            if name.startswith(SAVE_NAME):
                continue
            target = f'{BASE}/{folder}/{name}'
            # ls includes map directories; hash only ordinary files.
            output = root('sh', '-c', 'if [ -f ' + shlex.quote(target) + ' ]; then sha256sum ' + shlex.quote(target) + '; fi').decode()
            if output.strip():
                result[target] = output.split()[0]
    return result


def generate_assets():
    content = OUT / 'fixture/content'
    shutil.copytree(FIXTURE / 'content', content, dirs_exist_ok=True)
    for i in range(6):
        target = content / ('Assets/Music/Loop.wav' if i == 4 else 'Assets/Sounds/Long.wav' if i == 5 else f'Assets/Sounds/Pulse{i}.wav')
        target.parent.mkdir(parents=True, exist_ok=True)
        duration = 1.0 if i == 4 else 5.0 if i == 5 else 0.2
        samples = int(22050 * duration)
        pcm = bytearray()
        for n in range(samples):
            envelope = min(1.0, n / 200, (samples - n) / 200)
            value = int(2000 * envelope * math.sin(2 * math.pi * (220 + i * 110) * n / 22050))
            pcm.extend(struct.pack('<h', value))
        with wave.open(str(target), 'wb') as audio:
            audio.setnchannels(1)
            audio.setsampwidth(2)
            audio.setframerate(22050)
            audio.writeframes(pcm)

    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff)

    image = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', 16, 16, 8, 6, 0, 0, 0))
    image += chunk(b'IDAT', zlib.compress((b'\0' + bytes([70, 110, 180, 255]) * 16) * 16))
    image += chunk(b'IEND', b'')
    (content / 'Box.png').write_bytes(image)
    return content


if args.action == 'prepare':
    running = subprocess.run([ADB, '-s', args.device, 'shell', 'pidof', 'com.bunnyloader:game'], capture_output=True)
    if running.stdout.strip():
        raise RuntimeError('A game session is active. Save/close it before preparing this test.')
    if (OUT / 'prepared').exists():
        raise RuntimeError('A test is already prepared; restore it before preparing another.')

    shell('am', 'force-stop', 'com.bunnyloader')
    root('mkdir', '-p', STAGING)
    originals = {}
    for category in CATEGORIES:
        exists = shell('run-as', 'com.bunnyloader', 'sh', '-c', 'test -f shared_prefs/' + category + '.xml && echo yes || echo no').strip() == b'yes'
        data = adb('exec-out', 'run-as', 'com.bunnyloader', 'cat', 'shared_prefs/' + category + '.xml') if exists else b'<map />'
        originals[category] = {'exists': exists, 'sha256': hashlib.sha256(data).hexdigest()}
        (OUT / (category + '.original.xml')).write_bytes(data)

    for folder in ['Players', 'Worlds']:
        if any(name.startswith(SAVE_NAME) for name in shell('ls', BASE + '/' + folder).decode().splitlines()):
            raise RuntimeError('Test-save prefix already exists. Inspect it before reuse: ' + SAVE_NAME)

    (OUT / 'original-state.json').write_text(json.dumps({'preferences': originals, 'saves': save_hashes()}, indent=2), encoding='utf-8')
    # Mark before mutations so restore remains available after a partial setup.
    (OUT / 'restore.completed').unlink(missing_ok=True)
    (OUT / 'prepared').touch()
    for folder, extension, source in [('Players', 'plr', 'sda'), ('Worlds', 'wld', 'Pesadelo_Odioso')]:
        root('cp', f'{BASE}/{folder}/{source}.{extension}', f'{BASE}/{folder}/{SAVE_NAME}.{extension}')

    for category in CATEGORIES:
        tree = ET.fromstring((OUT / (category + '.original.xml')).read_bytes())
        if category == 'settings':
            values = {'quickStart': ('boolean', 'true'), 'quickPlayer': ('string', SAVE_NAME + '.plr'), 'quickWorld': ('string', SAVE_NAME + '.wld')}
        elif category == 'mods':
            values = {folder: ('boolean', 'false') for folder in shell('ls', BASE + '/bunny_packs').decode().splitlines()
                      if re.fullmatch(r'[0-9a-fA-F-]{36}', folder)}
            values.update({child.get('name'): ('boolean', 'false') for child in tree if child.tag == 'boolean'})
            values[UID] = ('boolean', 'true')
        else:
            tree = ET.Element('map')
            values = {}
        for key, (tag, value) in values.items():
            for child in list(tree):
                if child.get('name') == key:
                    tree.remove(child)
            child = ET.SubElement(tree, tag, name=key)
            if tag == 'boolean':
                child.set('value', value)
            else:
                child.text = value
        write_pref(category, ET.tostring(tree, encoding='utf-8', xml_declaration=True))

    adb('push', generate_assets(), STAGING + '/')
    adb('push', FIXTURE / 'manifest.json', STAGING + '/manifest.json')
    root('mkdir', '-p', PACK)
    root('cp', '-R', STAGING + '/content', PACK + '/')
    root('cp', STAGING + '/manifest.json', PACK + '/manifest.json')
    root('chmod', '-R', '755', PACK)
    print('Prepared isolated save copies and backed up preferences.', flush=True)
elif args.action == 'run':
    if not (OUT / 'prepared').exists() or not args.apk:
        raise RuntimeError('Prepare first and specify --apk.')
    shell('am', 'force-stop', 'com.bunnyloader')
    print(adb('install', '-r', args.apk.resolve()).decode().strip(), flush=True)
    if args.lifecycle:
        long_sound = generate_assets() / 'Assets/Sounds/Long.wav'
        adb('push', long_sound, STAGING + '/Long.wav')
        root('cp', STAGING + '/Long.wav', PACK + '/content/Assets/Sounds/Long.wav')
    # Start every A/B run from identical original world/player bytes.
    for folder, extension, source in [('Players', 'plr', 'sda'), ('Worlds', 'wld', 'Pesadelo_Odioso')]:
        root('cp', f'{BASE}/{folder}/{source}.{extension}', f'{BASE}/{folder}/{SAVE_NAME}.{extension}')
    config = OUT / 'config.js'
    config.write_text('export const variant = ' + json.dumps(args.variant) + ';\nexport const profile = ' + str(args.profile).lower() + ';\nexport const extended = ' + str(args.suite == 'soundasync').lower() + ';\nexport const lifecycle = ' + str(args.lifecycle).lower() + ';\nexport const normalRate = ' + str(args.normal_rate) + ';\nexport const onlyNormal = ' + str(args.only_normal).lower() + ';\n', encoding='utf-8')
    adb('push', FIXTURE / 'content/main.js', STAGING + '/main.js')
    root('cp', STAGING + '/main.js', PACK + '/content/main.js')
    adb('push', FIXTURE / 'content/profile.js', STAGING + '/profile.js')
    root('cp', STAGING + '/profile.js', PACK + '/content/profile.js')
    adb('push', config, STAGING + '/config.js')
    root('cp', STAGING + '/config.js', PACK + '/content/config.js')
    adb('logcat', '-c')
    root('am', 'start', '-n', 'com.bunnyloader/dev.bunnyloader.GameActivity')
    deadline = time.monotonic() + 150
    log = ''
    paused = False
    while time.monotonic() < deadline:
        time.sleep(0.15 if args.lifecycle and not paused else 3)
        log = adb('logcat', '-d', '-s', 'BunnyLoader').decode('utf-8', errors='replace')
        if args.lifecycle and not paused and 'soundfilter LIFECYCLE_READY' in log:
            # This emulator keeps apps on virtual displays; HOME may target display 0.
            _, game_display = activity_root('com.bunnyloader/dev.bunnyloader.GameActivity')
            root('am', 'start', '-W', '-n', 'com.bunnyloader/dev.bunnyloader.LauncherActivity')
            launcher_stack, launcher_display = activity_root('com.bunnyloader/dev.bunnyloader.LauncherActivity')
            if launcher_display != game_display:
                root('am', 'display', 'move-stack', launcher_stack, game_display)
            activity = shell('dumpsys', 'activity', 'activities').decode(errors='replace')
            resumed = [line for line in activity.splitlines() if re.search(r'(ResumedActivity|topResumedActivity|Resumed:)', line)]
            if any('com.bunnyloader/dev.bunnyloader.GameActivity' in line for line in resumed):
                raise RuntimeError('GameActivity is still resumed; pause checkpoint is invalid.')
            time.sleep(6)
            root('am', 'start', '-n', 'com.bunnyloader/dev.bunnyloader.GameActivity')
            paused = True
        if 'soundfilter FIM variant=' in log:
            break
    (OUT / (args.label + '.logcat.log')).write_bytes(adb('logcat', '-d'))
    names = shell('ls', BASE + '/logs').decode().splitlines()
    name = sorted(name for name in names if name.startswith('bunny_'))[-1]
    runtime = shell('cat', BASE + '/logs/' + name).decode('utf-8', errors='replace')
    (OUT / (args.label + '.runtime.log')).write_text(runtime, encoding='utf-8')
    rows = []
    for line in runtime.splitlines():
        if 'soundfilter ' in line or 'error in ' in line:
            print(line, flush=True)
        if 'soundfilter RESULT ' in line:
            rows.append(json.loads(line.split('soundfilter RESULT ', 1)[1]))
    (OUT / (args.label + '.json')).write_text(json.dumps({'apk': str(args.apk.resolve()), 'sha256': hashlib.sha256(args.apk.read_bytes()).hexdigest(), 'results': rows}, indent=2), encoding='utf-8')
    expected = 1 if args.only_normal else 7 if args.suite == 'soundasync' else 4 + int(args.profile)
    if args.lifecycle and not paused:
        raise SystemExit('Lifecycle test did not reach the pause/resume checkpoint.')
    if not re.search(r'soundfilter FIM variant=' + args.variant + r' checks=[1-9]\d* falhas=0', runtime) or len(rows) != expected:
        raise SystemExit('Benchmark did not finish with zero failures; inspect the saved logs.')
elif args.action == 'restore':
    if not (OUT / 'prepared').exists():
        raise RuntimeError('No prepared state to restore.')
    shell('am', 'force-stop', 'com.bunnyloader')
    original = json.loads((OUT / 'original-state.json').read_text(encoding='utf-8'))
    for category, state in original['preferences'].items():
        if state['exists']:
            data = (OUT / (category + '.original.xml')).read_bytes()
            write_pref(category, data)
            restored = adb('exec-out', 'run-as', 'com.bunnyloader', 'cat', 'shared_prefs/' + category + '.xml')
            if restored != data:
                raise RuntimeError('Preference restore mismatch: ' + category)
        else:
            shell('run-as', 'com.bunnyloader', 'rm', '-f', 'shared_prefs/' + category + '.xml')
    if save_hashes() != original['saves']:
        raise RuntimeError('Original save hashes changed; inspect before cleanup.')
    resolved = root('readlink', '-f', PACK).decode().strip()
    if resolved != '/storage/emulated/0/Android/data/com.bunnyloader/bunny_packs/' + UID:
        raise RuntimeError('Unexpected fixture path: ' + resolved)
    root('rm', '-rf', resolved)
    for folder in ['Players', 'Worlds']:
        for name in shell('ls', BASE + '/' + folder).decode().splitlines():
            if name == SAVE_NAME or name.startswith(SAVE_NAME + '.'):
                resolved = root('readlink', '-f', f'{BASE}/{folder}/{name}').decode().strip()
                expected = f'/storage/emulated/0/Android/data/com.bunnyloader/{folder}/{name}'
                if resolved != expected:
                    raise RuntimeError('Unexpected test save path: ' + resolved)
                root('rm', '-rf', resolved)
    (OUT / 'prepared').unlink()
    (OUT / 'restore.completed').write_text('Preferences matched byte for byte; all original saves matched SHA-256. Temporary mod and save copies removed.\n', encoding='utf-8')
    shell('am', 'start', '-n', 'com.bunnyloader/dev.bunnyloader.LauncherActivity')
    print((OUT / 'restore.completed').read_text(), flush=True)
