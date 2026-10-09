"""Provision an isolated multiplayer fixture, retaining original saves/prefs/cache.

Only this fixture uid is installed. Original save directories are temporarily
kept in a journalled sibling directory; the game sees copies of one save each.
Run restore for each prepared device even after an interrupted test.
"""
import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import re
import shlex
import subprocess
import tarfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
TEST = Path(__file__).resolve().parent
UID = json.loads((TEST / 'manifest.json').read_text(encoding='utf-8'))['uid']
BASE = '/sdcard/Android/data/com.bunnyloader'
PACKAGE = 'com.bunnyloader'
BACKUP = BASE + '/.serversync-backup'
PACK = BASE + '/bunny_packs/' + UID
ADB = str(Path(os.environ['LOCALAPPDATA']) / 'Android/Sdk/platform-tools/adb.exe')
parser = argparse.ArgumentParser()
parser.add_argument('action', choices=['prepare', 'version', 'launch', 'collect', 'restore'])
parser.add_argument('--device', required=True)
parser.add_argument('--version', default='1.1.0')
parser.add_argument('--legacy', action='store_true')
parser.add_argument('--label', default='runtime')
args = parser.parse_args()
assert re.fullmatch(r'[\w.:-]+', args.device)
assert re.fullmatch(r'[\w.-]+', args.label)
OUT = ROOT / 'build/serversync' / args.device.replace(':', '-')
OUT.mkdir(parents=True, exist_ok=True)
JOURNAL = OUT / 'prepared.json'


def adb(*parts, data=None, check=True):
    return subprocess.run([ADB, '-s', args.device, *map(str, parts)], input=data,
                          capture_output=True, check=check, timeout=180).stdout


def shell(*parts, check=True):
    return adb('shell', ' '.join(shlex.quote(str(p)) for p in parts), check=check).decode('utf-8').strip()


def root(*parts):
    return shell('su', '0', *parts)


def exists(path):
    return subprocess.run(
        [ADB, '-s', args.device, 'shell', 'test', '-e', path], capture_output=True).returncode == 0


def save_journal(data):
    JOURNAL.write_text(json.dumps(data, indent=2), encoding='utf-8')


def save_hashes():
    # Fixed directories; shell quotes inside find protect filenames with spaces.
    command = f"find {BASE}/Players {BASE}/Worlds -type f -exec sha256sum {{}} ';'"
    return root('sh', '-c', command).splitlines()


def write_tar(files):
    output = io.BytesIO()
    with tarfile.open(fileobj=output, mode='w') as tar:
        for name, data in files.items():
            info = tarfile.TarInfo(name)
            info.size = len(data)
            tar.addfile(info, io.BytesIO(data))
    adb('exec-in', 'run-as', PACKAGE, 'tar', '-xf', '-', data=output.getvalue())


def version():
    assert JOURNAL.exists(), 'Prepare first.'
    manifest = json.loads((TEST / 'manifest.json').read_text(encoding='utf-8'))
    manifest['version'] = args.version
    path = OUT / 'manifest.prepared.json'
    path.write_text(json.dumps(manifest), encoding='utf-8')
    stage = '/data/local/tmp/bunny-serversync-' + args.device.replace(':', '-')
    root('mkdir', '-p', stage, PACK)
    adb('push', TEST / 'content', stage + '/')
    root('cp', '-R', stage + '/content', PACK + '/')
    adb('push', path, stage + '/manifest.json')
    root('rm', '-f', PACK + '/manifest.json', PACK + '/mod.json')
    root('cp', stage + '/manifest.json', PACK + ('/mod.json' if args.legacy else '/manifest.json'))
    root('chmod', '-R', '755', PACK)


if args.action == 'prepare':
    assert not JOURNAL.exists(), 'Restore the previous preparation first.'
    assert not adb('shell', 'pidof', PACKAGE + ':game', check=False).strip(), 'Close the game before preparing.'
    shell('am', 'force-stop', PACKAGE)
    assert not exists(BACKUP) and not exists(PACK), 'Fixture paths already exist; refusing to overwrite.'
    original = adb('exec-out', 'run-as', PACKAGE, 'tar', '-cf', '-', 'shared_prefs')
    (OUT / 'prefs.original.tar').write_bytes(original)
    store = adb('exec-out', 'run-as', PACKAGE, 'tar', '-cf', '-', 'cache/store')
    (OUT / 'store.original.tar').write_bytes(store)
    session = adb('exec-out', 'run-as', PACKAGE, 'cat', 'files/server_session.json', check=False)
    (OUT / 'session.original.json').write_bytes(session)
    journal = {'moved': [], 'saveHashes': sorted(save_hashes()), 'sessionExisted': bool(session)}
    save_journal(journal)
    root('mkdir', '-p', BACKUP)
    for directory, extension in [('Players', '.plr'), ('Worlds', '.wld')]:
        names = shell('ls', BASE + '/' + directory).splitlines()
        source = next(name for name in names if name.endswith(extension))
        root('cp', BASE + '/' + directory + '/' + source, BACKUP + '/copy' + extension)
        root('mv', BASE + '/' + directory, BACKUP + '/' + directory)
        journal['moved'].append(directory)
        save_journal(journal)
        root('mkdir', '-p', BASE + '/' + directory)
        root('cp', BACKUP + '/copy' + extension, BASE + '/' + directory + '/ServerSyncTest' + extension)
        # Root creates the copies; the app must also be able to autosave them.
        root('chmod', '777', BASE + '/' + directory)
        root('chmod', '666', BASE + '/' + directory + '/ServerSyncTest' + extension)
    with tarfile.open(fileobj=io.BytesIO(original)) as tar:
        prepared = {}
        for category in ['settings', 'mods']:
            name = 'shared_prefs/' + category + '.xml'
            data = tar.extractfile(name).read().decode('utf-8')
            data = re.sub(r'&#(\d+);', lambda m: m.group(0) if int(m.group(1)) >= 32 or int(m.group(1)) in (9, 10, 13) else '', data)
            tree = ET.fromstring(data)
            if category == 'settings':
                values = {'quickStart': ('boolean', 'true'), 'quickPlayer': ('string', ''),
                          'quickWorld': ('string', '-'), 'devChannel': ('boolean', 'true')}
            else:
                values = {n.get('name'): ('boolean', 'false') for n in tree if n.tag == 'boolean'}
                for name_in_dir in shell('ls', BASE + '/bunny_packs').splitlines():
                    if re.fullmatch(r'[0-9a-f-]{36}', name_in_dir):
                        values[name_in_dir] = ('boolean', 'false')
                values[UID] = ('boolean', 'true')
            for key, (tag, value) in values.items():
                for child in list(tree):
                    if child.get('name') == key:
                        tree.remove(child)
                node = ET.SubElement(tree, tag, name=key)
                if tag == 'boolean':
                    node.set('value', value)
                else:
                    node.text = value
            prepared['shared_prefs/' + category + '.xml'] = ET.tostring(tree, encoding='utf-8', xml_declaration=True)
    write_tar(prepared)
    shell('run-as', PACKAGE, 'rm', '-f', 'files/server_session.json')
    version()
    print('Prepared', args.device, 'version', args.version, 'legacy', args.legacy)
elif args.action == 'version':
    shell('am', 'force-stop', PACKAGE)
    version()
elif args.action == 'launch':
    assert JOURNAL.exists()
    shell('am', 'force-stop', PACKAGE)
    adb('logcat', '-c')
    print(root('am', 'start', '-W', '-n', PACKAGE + '/dev.bunnyloader.GameActivity'))
elif args.action == 'collect':
    for section in re.split(r'(?=RootTask id=)', shell('am', 'stack', 'list')):
        if 'topActivity=ComponentInfo{com.bunnyloader/dev.bunnyloader.GameActivity}' in section and 'displayId=0' not in section.splitlines()[0]:
            task = re.search(r'RootTask id=(\d+)', section).group(1)
            shell('am', 'display', 'move-stack', task, '0')
    text = adb('logcat', '-d').decode('utf-8', errors='replace')
    (OUT / (args.label + '.logcat.txt')).write_text(text, encoding='utf-8')
    logs = sorted(n for n in shell('ls', BASE + '/logs').splitlines() if n.startswith('bunny_'))
    native = root('cat', BASE + '/logs/' + logs[-1])
    (OUT / (args.label + '.native.txt')).write_text(native, encoding='utf-8')
    (OUT / (args.label + '.png')).write_bytes(adb('exec-out', 'screencap', '-p'))
    for line in (text + '\n' + native).splitlines():
        if 'serversync-probe' in line or 'sincronizar mods:' in line or 'servidor:' in line:
            print(line)
    assert 'FATAL EXCEPTION' not in text and 'Fatal signal' not in text
elif args.action == 'restore':
    journal = json.loads(JOURNAL.read_text(encoding='utf-8'))
    shell('am', 'force-stop', PACKAGE)
    resolved = root('readlink', '-f', PACK)
    expected = '/storage/emulated/0/Android/data/com.bunnyloader/bunny_packs/' + UID
    assert resolved == expected, resolved
    root('rm', '-rf', PACK)
    for directory in journal['moved']:
        assert exists(BACKUP + '/' + directory), 'Original save directory missing.'
        root('rm', '-rf', BASE + '/' + directory)
        root('mv', BACKUP + '/' + directory, BASE + '/' + directory)
    assert sorted(save_hashes()) == journal['saveHashes'], 'Original save content changed.'
    root('rm', '-rf', BACKUP)
    root('rm', '-rf', '/data/local/tmp/bunny-serversync-' + args.device.replace(':', '-'))
    shell('run-as', PACKAGE, 'rm', '-rf', 'cache/store')
    shell('run-as', PACKAGE, 'rm', '-rf', 'shared_prefs')
    for name, target in [('prefs.original.tar', 'prefs'), ('store.original.tar', 'store')]:
        blob = (OUT / name).read_bytes()
        adb('exec-in', 'run-as', PACKAGE, 'tar', '-xf', '-', data=blob)
        with tarfile.open(fileobj=io.BytesIO(blob)) as tar:
            for member in tar.getmembers():
                if member.isfile():
                    expected = tar.extractfile(member).read()
                    actual = adb('exec-out', 'run-as', PACKAGE, 'cat', member.name)
                    assert hashlib.sha256(actual).digest() == hashlib.sha256(expected).digest(), member.name
    if journal['sessionExisted']:
        write_tar({'files/server_session.json': (OUT / 'session.original.json').read_bytes()})
    else:
        shell('run-as', PACKAGE, 'rm', '-f', 'files/server_session.json')
    JOURNAL.rename(OUT / 'restored.json')
    print('Restored', args.device, 'original saves, preferences and store verified.')
