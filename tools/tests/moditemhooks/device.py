import argparse
import subprocess
import re
import xml.etree.ElementTree as ET
from pathlib import Path

UID = 'e56f2848-2169-4d5f-8898-1bcbfdb4b7c2'
ROOT = Path(__file__).resolve().parents[3]
OUTPUT = ROOT / 'build/moditemhooks'
parser = argparse.ArgumentParser()
parser.add_argument('action', choices=['prepare', 'cleanup'])
parser.add_argument('--device', default='127.0.0.1:16384')
parser.add_argument('--world')
parser.add_argument('--run-id')
args = parser.parse_args()
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
    if args.action == 'prepare':
        if not backup.exists():
            backup.write_bytes(ET.tostring(current, encoding='utf-8'))
        changes = [('quickStart', 'boolean', 'true')]
        if name == 'mods':
            changes = [(UID, 'boolean', 'true')]
        elif args.world:
            changes.append(('quickWorld', 'string', args.world))
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
        for key in ['quickStart', 'quickWorld'] if name == 'settings' else [UID]:
            for child in list(current):
                if child.get('name') == key:
                    current.remove(child)
            for child in original:
                if child.get('name') == key:
                    current.append(child)
    prepared = OUTPUT / (name + '.prepared.xml')
    prepared.write_bytes(ET.tostring(current, encoding='utf-8', xml_declaration=True))
    remote = '/data/local/tmp/moditemhooks-' + name + '.xml'
    adb('push', str(prepared), remote)
    adb('shell', 'run-as', 'com.bunnyloader', 'cp', remote, 'shared_prefs/' + name + '.xml')

target = '/storage/emulated/0/Android/data/com.bunnyloader/bunny_packs/' + UID
if args.action == 'prepare':
    staging = '/data/local/tmp/moditemhooks-fixture'
    adb('shell', 'mkdir', '-p', staging)
    fixture = ROOT / 'tools/tests/moditemhooks'
    adb('push', str(fixture / 'content'), staging + '/')
    adb('push', str(fixture / 'manifest.json'), staging + '/manifest.json')
    adb('shell', 'su', '0', 'mkdir', '-p', target)
    adb('shell', 'su', '0', 'cp', '-R', staging + '/content', target + '/')
    adb('shell', 'su', '0', 'cp', staging + '/manifest.json', target + '/manifest.json')
    adb('shell', 'su', '0', 'chmod', '-R', '755', target)
else:
    resolved = adb('shell', 'su', '0', 'readlink', '-f', target).decode().strip()
    if resolved != target:
        raise ValueError('Pasta do fixture inesperada: ' + resolved)
    adb('shell', 'su', '0', 'rm', '-rf', target)
