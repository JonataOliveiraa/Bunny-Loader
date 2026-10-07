"""Native regression fixture. Uses copies of existing saves and restores prefs."""
import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
FIXTURE = Path(__file__).resolve().parent
UID = json.loads((FIXTURE / 'manifest.json').read_text(encoding='utf-8'))['uid']
parser = argparse.ArgumentParser()
parser.add_argument('action', choices=['prepare', 'launch', 'collect', 'restore'])
parser.add_argument('--device', default='127.0.0.1:16384')
parser.add_argument('--role', choices=['native', 'host', 'client'], default='native')
args = parser.parse_args()
if not re.fullmatch(r'[\w.:-]+', args.device):
    parser.error('invalid device')
ADB = shutil.which('adb') or str(Path(os.environ['LOCALAPPDATA']) / 'Android/Sdk/platform-tools/adb.exe')
OUT = ROOT / 'build/moditemdata' / args.device.replace(':', '-')
OUT.mkdir(parents=True, exist_ok=True)
BASE = '/sdcard/Android/data/com.bunnyloader'
PACK = BASE + '/bunny_packs/' + UID
STAGING = '/data/local/tmp/moditemdata'


def adb(*values):
    return subprocess.check_output([ADB, '-s', args.device, *map(str, values)])


def root(*values):
    return adb('shell', 'su', '0', *values)


def prefs(category, data):
    local = OUT / (category + '.prepared.xml')
    local.write_bytes(data)
    adb('push', local, STAGING + '/' + category + '.xml')
    adb('shell', 'run-as', 'com.bunnyloader', 'cp', STAGING + '/' + category + '.xml', 'shared_prefs/' + category + '.xml')


if args.action == 'prepare':
    running = subprocess.run([ADB, '-s', args.device, 'shell', 'pidof', 'com.bunnyloader:game'], capture_output=True)
    if running.stdout.strip():
        raise RuntimeError('Game is running. Close the game before preparing the isolated fixture.')
    completed = OUT / 'restore.completed'
    fresh = completed.exists()
    root('mkdir', '-p', STAGING)
    for category in ['settings', 'mods']:
        original = adb('exec-out', 'run-as', 'com.bunnyloader', 'cat', 'shared_prefs/' + category + '.xml')
        backup = OUT / (category + '.original.xml')
        if fresh or not backup.exists():
            backup.write_bytes(original)
        tree = ET.fromstring(original)
        if category == 'settings':
            values = {'quickStart': ('boolean', 'true'), 'quickPlayer': ('string', 'BL_ItemData_Test.plr'), 'quickWorld': ('string', 'BL_ItemData_Test.wld')}
        else:
            values = {UID: ('boolean', 'true')}
            for folder in adb('shell', 'ls', BASE + '/bunny_packs').decode().split():
                if re.fullmatch(r'[0-9a-fA-F-]{36}', folder):
                    values[folder] = ('boolean', 'false')
            for child in tree:
                if child.tag == 'boolean':
                    values[child.get('name')] = ('boolean', 'false')
            values[UID] = ('boolean', 'true')
        for key, (tag, value) in values.items():
            for child in list(tree):
                if child.get('name') == key:
                    tree.remove(child)
            child = ET.SubElement(tree, tag, name=key)
            if tag == 'boolean':
                child.set('value', value)
            else:
                child.text = value
        prefs(category, ET.tostring(tree, encoding='utf-8', xml_declaration=True))
    completed.unlink(missing_ok=True)
    for folder, ext, original in [('Players', 'plr', 'sda'), ('Worlds', 'wld', 'Pesadelo_Odioso')]:
        target = f'{BASE}/{folder}/BL_ItemData_Test.{ext}'
        if subprocess.run([ADB, '-s', args.device, 'shell', 'test', '-e', target], capture_output=True).returncode:
            candidates = adb('shell', 'ls', BASE + '/' + folder).decode().splitlines()
            source = next((name for name in candidates if name == original + '.' + ext), None)
            source = source or next(name for name in candidates if name.endswith('.' + ext))
            root('cp', f'{BASE}/{folder}/{source}', target)
    adb('install', '-r', ROOT / 'app/build/outputs/apk/debug/app-debug.apk')
    adb('push', FIXTURE / 'content', STAGING + '/')
    config = OUT / 'config.js'
    config.write_text('export const role = ' + json.dumps(args.role) + ';\n', encoding='utf-8')
    adb('push', config, STAGING + '/content/config.js')
    adb('push', FIXTURE / 'manifest.json', STAGING + '/manifest.json')
    root('mkdir', '-p', PACK)
    root('cp', '-R', STAGING + '/content', PACK + '/')
    root('cp', STAGING + '/manifest.json', PACK + '/manifest.json')
    root('chmod', '-R', '755', PACK)
elif args.action == 'launch':
    adb('shell', 'am', 'force-stop', 'com.bunnyloader')
    root('am', 'start', '-n', 'com.bunnyloader/dev.bunnyloader.GameActivity')
elif args.action == 'collect':
    log = adb('logcat', '-d').decode('utf-8', errors='replace')
    (OUT / 'logcat.log').write_text(log, encoding='utf-8')
    names = adb('shell', 'ls', BASE + '/logs').decode().split()
    name = sorted(name for name in names if name.startswith('bunny_'))[-1]
    runtime = adb('shell', 'cat', BASE + '/logs/' + name).decode('utf-8', errors='replace')
    (OUT / 'runtime.log').write_text(runtime, encoding='utf-8')
    for line in runtime.splitlines():
        if re.search(r'moditemdata |ModItem\.|hooks de dados de bau|Fatal signal|error in \[' + UID, line):
            print(line)
    if not re.search(r'moditemdata FIM role=' + args.role + r' checks=[1-9]\d* falhas=0', runtime):
        raise SystemExit('Native fixture has not completed successfully.')
elif args.action == 'restore':
    adb('shell', 'am', 'force-stop', 'com.bunnyloader')
    for category in ['settings', 'mods']:
        prefs(category, (OUT / (category + '.original.xml')).read_bytes())
    # Delete only this fixture's known directory; never derive a recursive
    # deletion from an unchecked path or an arbitrary caller-provided name.
    resolved = root('readlink', '-f', PACK).decode().strip()
    expected = '/storage/emulated/0/Android/data/com.bunnyloader/bunny_packs/' + UID
    if resolved != expected:
        raise RuntimeError('Unexpected fixture path: ' + resolved)
    root('rm', '-rf', resolved)
    (OUT / 'restore.completed').write_text('Preferences restored; next prepare starts a new backup.\n', encoding='utf-8')
