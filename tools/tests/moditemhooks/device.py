import argparse
import json
import subprocess
import re
import xml.etree.ElementTree as ET
from uuid import UUID
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
parser = argparse.ArgumentParser()
parser.add_argument('action', choices=['prepare', 'cleanup'])
parser.add_argument('--device', default='127.0.0.1:16384')
parser.add_argument('--world')
parser.add_argument('--run-id')
parser.add_argument('--fixture', choices=['moditemhooks', 'playerdrawcache', 'playerdrawlayers', 'modsystemhooks', 'hookperf'], default='moditemhooks')
parser.add_argument('--isolate', action='store_true')
parser.add_argument('--classes', type=int, choices=[0, 1, 8], default=8)
args = parser.parse_args()
fixture = ROOT / 'tools/tests' / args.fixture
UID = str(UUID(json.loads((fixture / 'manifest.json').read_text(encoding='utf-8'))['uid']))
OUTPUT = ROOT / 'build' / args.fixture
if args.run_id:
    if not re.fullmatch(r'[A-Za-z0-9_-]+', args.run_id):
        parser.error('--run-id deve conter apenas letras, numeros, _ ou -')
    OUTPUT = OUTPUT / args.run_id
OUTPUT.mkdir(parents=True, exist_ok=True)


def adb(*values):
    return subprocess.check_output(['adb', '-s', args.device, *values])


adb('shell', 'am', 'force-stop', 'com.bunnyloader')
for name in ['settings', 'mods']:
    current = ET.fromstring(adb('shell', 'run-as', 'com.bunnyloader', 'cat', 'shared_prefs/' + name + '.xml'))
    backup = OUTPUT / (name + '.original.xml')
    changed = OUTPUT / (name + '.changed.json')
    if args.action == 'prepare':
        if not backup.exists():
            backup.write_bytes(ET.tostring(current, encoding='utf-8'))
        changes = [('quickStart', 'boolean', 'true')]
        if name == 'mods':
            changes = [(UID, 'boolean', 'true')]
            if args.isolate:
                folders = adb('shell', 'ls', '/sdcard/Android/data/com.bunnyloader/bunny_packs').decode().split()
                changes = []
                for folder in folders:
                    try:
                        changes.append((str(UUID(folder)), 'boolean', 'false'))
                    except ValueError:
                        continue
                changes.append((UID, 'boolean', 'true'))
        elif args.world:
            changes.append(('quickWorld', 'string', args.world))
        keys = set(key for key, tag, value in changes)
        if changed.exists():
            keys.update(json.loads(changed.read_text(encoding='utf-8')))
        changed.write_text(json.dumps(sorted(keys)), encoding='utf-8')
        for key, tag, value in changes:
            for child in list(current):
                if child.get('name') == key:
                    current.remove(child)
            child = ET.SubElement(current, tag, name=key)
            if tag == 'boolean':
                child.set('value', value)
            else:
                child.text = value
    else:
        original = ET.fromstring(backup.read_bytes())
        keys = json.loads(changed.read_text(encoding='utf-8')) if changed.exists() else (['quickStart', 'quickWorld'] if name == 'settings' else [UID])
        for key in keys:
            for child in list(current):
                if child.get('name') == key:
                    current.remove(child)
            for child in original:
                if child.get('name') == key:
                    current.append(child)
    prepared = OUTPUT / (name + '.prepared.xml')
    prepared.write_bytes(ET.tostring(current, encoding='utf-8', xml_declaration=True))
    remote = '/data/local/tmp/' + args.fixture + '-' + name + '.xml'
    adb('push', str(prepared), remote)
    adb('shell', 'run-as', 'com.bunnyloader', 'cp', remote, 'shared_prefs/' + name + '.xml')
    adb('shell', 'rm', '-f', remote)

target = '/storage/emulated/0/Android/data/com.bunnyloader/bunny_packs/' + UID
staging = '/data/local/tmp/' + args.fixture + '-fixture'
if args.action == 'prepare':
    adb('shell', 'mkdir', '-p', staging)
    adb('push', str(fixture / 'content'), staging + '/')
    adb('push', str(fixture / 'manifest.json'), staging + '/manifest.json')
    if args.fixture == 'hookperf':
        config = OUTPUT / 'config.js'
        config.write_text('export const classCount = ' + str(args.classes) + ';\n', encoding='utf-8')
        adb('push', str(config), staging + '/content/config.js')
    adb('shell', 'su', '0', 'mkdir', '-p', target)
    adb('shell', 'su', '0', 'cp', '-R', staging + '/content', target + '/')
    adb('shell', 'su', '0', 'cp', staging + '/manifest.json', target + '/manifest.json')
    adb('shell', 'su', '0', 'chmod', '-R', '755', target)
else:
    resolved = adb('shell', 'su', '0', 'readlink', '-f', target).decode().strip()
    if resolved != target:
        raise ValueError('Pasta do fixture inesperada: ' + resolved)
    adb('shell', 'su', '0', 'rm', '-rf', target)
    resolved = adb('shell', 'su', '0', 'readlink', '-f', staging).decode().strip()
    if resolved != staging:
        raise ValueError('Pasta temporaria inesperada: ' + resolved)
    adb('shell', 'su', '0', 'rm', '-rf', staging)
