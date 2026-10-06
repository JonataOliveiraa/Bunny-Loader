import json
import os
import re
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
output = Path("build/mpmodplayer")
run_id = os.environ.get("BL_RUN_ID", "")
if run_id:
    if not re.fullmatch(r"[A-Za-z0-9_-]+", run_id):
        raise ValueError("BL_RUN_ID invalido")
    output /= run_id
output.mkdir(parents=True, exist_ok=True)
uid = str(UUID(json.loads(Path("tools/tests/mpmodplayer/manifest.json").read_text(encoding="utf-8"))["uid"]))
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
    remote = "/data/local/tmp/mpmodplayer-" + category + ".xml"
    adb(device, "push", str(prepared), remote)
    adb(device, "shell", "run-as", "com.bunnyloader", "cp", remote, settings)
    adb(device, "shell", "rm", "-f", remote)
