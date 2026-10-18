# Liga só os mods pedidos (BL_ENABLE, uids separados por vírgula) e ajusta o
# início rápido como o mpmodplayer; "restore" devolve as chaves mexidas. O original fica em
# BL_PREFS_DIR. Cópia do tools/tests/mpmodplayer/prefs.py com a lista de mods.
#   BL_PREFS_DIR=build/mpexample/prefs BL_ENABLE=<uid>,<uid> python prefs.py prepare|restore <aparelho>
import json
import os
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from uuid import UUID


def adb(device, *args):
    return subprocess.check_output(["adb", "-s", device, *args])


mode, device = sys.argv[1:]
if mode not in ["prepare", "restore"]:
    raise ValueError("prepare ou restore")
output = Path(os.environ["BL_PREFS_DIR"])
output.mkdir(parents=True, exist_ok=True)
uids = os.environ["BL_ENABLE"].split(",")
prefix = device.replace(":", "-")
for category in ["settings", "mods"]:
    settings = "shared_prefs/" + category + ".xml"
    backup = output / (prefix + "-" + category + ".xml")
    changed = output / (prefix + "-" + category + "-keys.json")
    if mode == "restore" and not backup.exists():
        continue
    current = ET.fromstring(adb(device, "shell", "run-as", "com.bunnyloader", "cat", settings))
    if mode == "prepare":
        if not backup.exists():
            backup.write_bytes(ET.tostring(current, encoding="utf-8"))
        values = {"quickStart": ("boolean", "true"), "quickWorld": ("string", "-")}
        if category == "mods":
            folders = adb(device, "shell", "ls", "/sdcard/Android/data/com.bunnyloader/bunny_packs").decode().split()
            values = {}
            for folder in folders:
                try:
                    values[str(UUID(folder))] = ("boolean", "false")
                except ValueError:
                    continue
            for uid in uids:
                values[uid] = ("boolean", "true")
        keys = set(values)
        if changed.exists():
            keys.update(json.loads(changed.read_text(encoding="utf-8")))
        changed.write_text(json.dumps(sorted(keys)), encoding="utf-8")
        for key, (tag, value) in values.items():
            for child in list(current):
                if child.get("name") == key:
                    current.remove(child)
            element = ET.SubElement(current, tag, name=key)
            if tag == "boolean":
                element.set("value", value)
            else:
                element.text = value
    else:
        original = ET.fromstring(backup.read_bytes())
        for key in json.loads(changed.read_text(encoding="utf-8")):
            for child in list(current):
                if child.get("name") == key:
                    current.remove(child)
            for child in original:
                if child.get("name") == key:
                    current.append(child)
    prepared = backup.with_suffix(".prepared.xml")
    prepared.write_bytes(ET.tostring(current, encoding="utf-8", xml_declaration=True))
    remote = "/data/local/tmp/mpexample-" + category + ".xml"
    adb(device, "push", str(prepared), remote)
    adb(device, "shell", "run-as", "com.bunnyloader", "cp", remote, settings)
    adb(device, "shell", "rm", "-f", remote)
