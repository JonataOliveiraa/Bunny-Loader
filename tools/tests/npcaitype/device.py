"""Run AIType integration on copies of saves, preserving the user's preferences."""
import argparse
import json
import os
from pathlib import Path
import re
import shlex
import shutil
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
FIXTURE = Path(__file__).resolve().parent
UID = json.loads((FIXTURE / 'manifest.json').read_text(encoding='utf-8'))['uid']
parser = argparse.ArgumentParser()
parser.add_argument('action', choices=['prepare', 'launch', 'collect', 'restore'])
parser.add_argument('--device', default='127.0.0.1:16384')
parser.add_argument('--order', choices=['mod-first', 'global-first', 'local-only'], default='mod-first')
args = parser.parse_args()
if not re.fullmatch(r'[\w.:-]+', args.device):
    parser.error('invalid device serial')

ADB = shutil.which('adb') or str(Path(os.environ['LOCALAPPDATA']) / 'Android/Sdk/platform-tools/adb.exe')
OUT = ROOT / 'build/npcaitype' / args.device.replace(':', '-')
OUT.mkdir(parents=True, exist_ok=True)
BASE = '/sdcard/Android/data/com.bunnyloader'
PACK = BASE + '/bunny_packs/' + UID
STAGING = '/data/local/tmp/npcaitype'
CATEGORIES = ['settings', 'mods', 'bunny_powers']


def adb(*values):
    return subprocess.check_output([ADB, '-s', args.device, *map(str, values)])


def shell(*values):
    return adb('shell', shlex.join(map(str, values)))


def root(*values):
    return shell('su', '0', *values)


def prefs(category, data):
    local = OUT / (category + '.prepared.xml')
    local.write_bytes(data)
    adb('push', local, STAGING + '/' + category + '.xml')
    shell('run-as', 'com.bunnyloader', 'cp', STAGING + '/' + category + '.xml', 'shared_prefs/' + category + '.xml')


if args.action == 'prepare':
    running = subprocess.run([ADB, '-s', args.device, 'shell', 'pidof', 'com.bunnyloader:game'], capture_output=True)
    if running.stdout.strip():
        raise RuntimeError('Close the current game session before preparing the isolated test.')

    root('mkdir', '-p', STAGING)
    completed = OUT / 'restore.completed'
    fresh = completed.exists()
    for category in CATEGORIES:
        exists = shell('run-as', 'com.bunnyloader', 'sh', '-c', 'test -f shared_prefs/' + category + '.xml && echo yes || echo no').strip() == b'yes'
        backup = OUT / (category + '.original.xml')
        absent = OUT / (category + '.original.absent')
        original = adb('exec-out', 'run-as', 'com.bunnyloader', 'cat', 'shared_prefs/' + category + '.xml') if exists else b'<?xml version="1.0" encoding="utf-8"?><map />'
        if fresh or not backup.exists():
            backup.write_bytes(original)
            absent.unlink(missing_ok=True)
            if not exists:
                absent.touch()

        tree = ET.fromstring(original)
        if category == 'settings':
            values = {'quickStart': ('boolean', 'true'), 'quickPlayer': ('string', 'BL_NPCAIType_Test.plr'), 'quickWorld': ('string', 'BL_NPCAIType_Test.wld')}
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
        prefs(category, ET.tostring(tree, encoding='utf-8', xml_declaration=True))
    completed.unlink(missing_ok=True)

    for folder, ext, preferred in [('Players', 'plr', 'sda'), ('Worlds', 'wld', 'Pesadelo_Odioso')]:
        target = f'{BASE}/{folder}/BL_NPCAIType_Test.{ext}'
        names = shell('ls', BASE + '/' + folder).decode().splitlines()
        if Path(target).name not in names:
            source = preferred + '.' + ext
            if source not in names:
                source = next(name for name in names if name.endswith('.' + ext))
            root('cp', f'{BASE}/{folder}/{source}', target)

    adb('install', '-r', ROOT / 'app/build/outputs/apk/debug/app-debug.apk')
    adb('push', FIXTURE / 'content', STAGING + '/')
    config = OUT / 'config.js'
    config.write_text('export const order = ' + json.dumps(args.order) + ';\n', encoding='utf-8')
    adb('push', config, STAGING + '/content/config.js')
    adb('push', FIXTURE / 'manifest.json', STAGING + '/manifest.json')
    root('mkdir', '-p', PACK)
    root('cp', '-R', STAGING + '/content', PACK + '/')
    root('cp', STAGING + '/manifest.json', PACK + '/manifest.json')
    root('chmod', '-R', '755', PACK)
elif args.action == 'launch':
    shell('am', 'force-stop', 'com.bunnyloader')
    root('am', 'start', '-n', 'com.bunnyloader/dev.bunnyloader.GameActivity')
elif args.action == 'collect':
    (OUT / (args.order + '.logcat.log')).write_bytes(adb('logcat', '-d'))
    names = shell('ls', BASE + '/logs').decode().splitlines()
    name = sorted(name for name in names if name.startswith('bunny_'))[-1]
    runtime = shell('cat', BASE + '/logs/' + name).decode('utf-8', errors='replace')
    (OUT / (args.order + '.runtime.log')).write_text(runtime, encoding='utf-8')
    for line in runtime.splitlines():
        if 'npcaitype ' in line or 'error in ' in line:
            print(line)
    if not re.search(r'npcaitype FIM order=' + args.order + r' checks=[1-9]\d* falhas=0', runtime):
        raise SystemExit('Integration test did not complete with zero failures.')
elif args.action == 'restore':
    shell('am', 'force-stop', 'com.bunnyloader')
    for category in CATEGORIES:
        if (OUT / (category + '.original.absent')).exists():
            shell('run-as', 'com.bunnyloader', 'rm', '-f', 'shared_prefs/' + category + '.xml')
        else:
            prefs(category, (OUT / (category + '.original.xml')).read_bytes())
    resolved = root('readlink', '-f', PACK).decode().strip()
    expected = '/storage/emulated/0/Android/data/com.bunnyloader/bunny_packs/' + UID
    if resolved != expected:
        raise RuntimeError('Unexpected fixture directory: ' + resolved)
    root('rm', '-rf', resolved)
    (OUT / 'restore.completed').write_text('Preferences restored.\n', encoding='utf-8')
