"""Isolate the native fixture; restore verifies the three original prefs byte for byte."""
import json
import shlex
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
STATE = ROOT / 'build/apiexpansion'
STATE.mkdir(parents=True, exist_ok=True)
UID = 'a3382261-70c8-4489-a374-439e86e57f69'
BASE = '/sdcard/Android/data/com.bunnyloader'
STAGE = '/data/local/tmp/apiexpansion'
PACK = BASE + '/bunny_packs/' + UID
EXAMPLE_UID = 'a3382261-70c8-4489-a374-439e86e57f70'

def adb(*args):
    return subprocess.check_output(['adb', '-s', '127.0.0.1:16384', *map(str, args)], stderr=subprocess.STDOUT)

def shell(*args):
    return adb('shell', shlex.join(map(str, args)))

def root(*args):
    return shell('su', '0', *args)

def prefs(name, data):
    local = STATE / (name + '.prepared.xml')
    local.write_bytes(data)
    adb('push', local, STAGE + '/' + name + '.xml')
    shell('run-as', 'com.bunnyloader', 'cp', STAGE + '/' + name + '.xml', 'shared_prefs/' + name + '.xml')

action = sys.argv[1]
if action == 'prepare':
    if (STATE / 'prepared').exists():
        raise RuntimeError('Restore the existing fixture first')
    root('mkdir', '-p', STAGE)
    for name in ['settings', 'mods', 'bunny_powers']:
        (STATE / (name + '.original.xml')).write_bytes(adb('exec-out', 'run-as', 'com.bunnyloader', 'cat', 'shared_prefs/' + name + '.xml'))
    (STATE / 'prepared').touch()
    shell('am', 'force-stop', 'com.bunnyloader')
    for name in ['settings', 'mods', 'bunny_powers']:
        tree = ET.fromstring((STATE / (name + '.original.xml')).read_bytes())
        if name == 'settings':
            values = {'quickStart': ('boolean', 'true'), 'quickPlayer': ('string', 'BL_ApiDiag.plr'), 'quickWorld': ('string', 'BL_ApiDiag.wld')}
        elif name == 'mods':
            values = {child.get('name'): ('boolean', 'false') for child in tree if child.tag == 'boolean'}
            values[UID] = ('boolean', 'true')
        else:
            tree = ET.Element('map'); values = {}
        for key, (tag, value) in values.items():
            for child in list(tree):
                if child.get('name') == key:
                    tree.remove(child)
            child = ET.SubElement(tree, tag, name=key)
            if tag == 'boolean': child.set('value', value)
            else: child.text = value
        prefs(name, ET.tostring(tree, encoding='utf-8', xml_declaration=True))
    for folder, ext, source in [('Players', 'plr', 'sda'), ('Worlds', 'wld', 'Pesadelo_Odioso')]:
        root('cp', f'{BASE}/{folder}/{source}.{ext}', f'{BASE}/{folder}/BL_ApiDiag.{ext}')
    adb('push', ROOT / 'tools/tests/apiexpansion/content', STAGE + '/')
    adb('push', ROOT / 'tools/tests/apiexpansion/manifest.json', STAGE + '/manifest.json')
    root('mkdir', '-p', PACK + '/content')
    root('cp', '-R', STAGE + '/content/.', PACK + '/content/')
    root('cp', STAGE + '/manifest.json', PACK + '/manifest.json')
    root('chmod', '-R', '755', PACK)
    print('Prepared isolated world and player; original prefs saved in build/apiexpansion')
elif action == 'run':
    shell('am', 'force-stop', 'com.bunnyloader')
    adb('push', ROOT / 'tools/tests/apiexpansion/content/main.js', STAGE + '/main.js')
    root('cp', STAGE + '/main.js', PACK + '/content/main.js')
    print(root('am', 'start', '-n', 'com.bunnyloader/dev.bunnyloader.GameActivity').decode())
elif action == 'example':
    if not (STATE / 'prepared').exists(): raise RuntimeError('Prepare the isolated fixture first')
    shell('am', 'force-stop', 'com.bunnyloader')
    manifest = json.loads((ROOT / 'samples/ExampleMod/manifest.json').read_text(encoding='utf-8'))
    manifest['uid'] = EXAMPLE_UID
    local = STATE / 'example-manifest.json'
    local.write_text(json.dumps(manifest), encoding='utf-8')
    (STATE / 'example-prepared').touch()
    root('mkdir', '-p', STAGE + '/example')
    adb('push', ROOT / 'samples/ExampleMod/content', STAGE + '/example/')
    adb('push', local, STAGE + '/example-manifest.json')
    target = BASE + '/bunny_packs/' + EXAMPLE_UID
    root('mkdir', '-p', target + '/content')
    root('cp', '-R', STAGE + '/example/content/.', target + '/content/')
    root('cp', STAGE + '/example-manifest.json', target + '/manifest.json')
    root('chmod', '-R', '755', target)
    tree = ET.fromstring(adb('exec-out', 'run-as', 'com.bunnyloader', 'cat', 'shared_prefs/mods.xml'))
    ET.SubElement(tree, 'boolean', name=EXAMPLE_UID, value='true')
    prefs('mods', ET.tostring(tree, encoding='utf-8', xml_declaration=True))
    # Force the new fixture after ExampleMod to exercise changed native IDs.
    test_manifest = json.loads((ROOT / 'tools/tests/apiexpansion/manifest.json').read_text(encoding='utf-8'))
    test_manifest['dependencies'] = ['examplemod']
    local = STATE / 'dependent-manifest.json'
    local.write_text(json.dumps(test_manifest), encoding='utf-8')
    adb('push', local, STAGE + '/dependent-manifest.json')
    root('cp', STAGE + '/dependent-manifest.json', PACK + '/manifest.json')
    print('Enabled isolated ExampleMod copy before the fixture; original installed pack preserved')
elif action == 'collect':
    names = shell('ls', BASE + '/logs').decode().splitlines()
    latest = sorted(name for name in names if name.startswith('bunny_'))[-1]
    data = shell('cat', BASE + '/logs/' + latest).decode('utf-8', errors='replace')
    (STATE / 'runtime.log').write_text(data, encoding='utf-8')
    print('\n'.join(line for line in data.splitlines() if any(key in line for key in ['apiexpansion', 'erro', 'ERROR', 'error', 'ItemDraw', 'Banners'])))
elif action == 'restore':
    if not (STATE / 'prepared').exists(): raise RuntimeError('No prepared test state')
    shell('am', 'force-stop', 'com.bunnyloader')
    for name in ['settings', 'mods', 'bunny_powers']:
        data = (STATE / (name + '.original.xml')).read_bytes()
        prefs(name, data)
        assert adb('exec-out', 'run-as', 'com.bunnyloader', 'cat', 'shared_prefs/' + name + '.xml') == data, name
    resolved = root('readlink', '-f', PACK).decode().strip()
    assert resolved == '/storage/emulated/0/Android/data/com.bunnyloader/bunny_packs/' + UID, resolved
    root('rm', '-rf', resolved)
    if (STATE / 'example-prepared').exists():
        target = BASE + '/bunny_packs/' + EXAMPLE_UID
        resolved = root('readlink', '-f', target).decode().strip()
        assert resolved == '/storage/emulated/0/Android/data/com.bunnyloader/bunny_packs/' + EXAMPLE_UID, resolved
        root('rm', '-rf', resolved)
        (STATE / 'example-prepared').unlink()
    for folder in ['Players', 'Worlds']:
        for name in shell('ls', BASE + '/' + folder).decode().splitlines():
            if name.startswith('BL_ApiDiag.'): root('rm', '-f', BASE + '/' + folder + '/' + name)
    resolved = root('readlink', '-f', STAGE).decode().strip()
    assert resolved == '/data/local/tmp/apiexpansion', resolved
    root('rm', '-rf', resolved)
    (STATE / 'prepared').unlink()
    print('Original settings, mods and powers restored and byte-verified; fixture files removed')
    print(root('am', 'start', '-n', 'com.bunnyloader/dev.bunnyloader.GameActivity').decode())
else:
    raise RuntimeError('Expected prepare, run, example, collect or restore')
