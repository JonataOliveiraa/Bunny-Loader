import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def adb(device, *args):
    return subprocess.check_output(["adb", "-s", device, *args])


mode, device = sys.argv[1:]
backup = Path("build/mpmodplayer") / (device.replace(":", "-") + "-settings.xml")
settings = "shared_prefs/settings.xml"
if mode == "prepare":
    original = adb(device, "shell", "run-as", "com.bunnyloader", "cat", settings)
    if not backup.exists():
        backup.write_bytes(original)
    root = ET.fromstring(original)
    for name, tag, value in [("quickStart", "boolean", "true"), ("quickWorld", "string", "-")]:
        for element in list(root):
            if element.get("name") == name:
                root.remove(element)
        element = ET.SubElement(root, tag, name=name)
        if tag == "boolean":
            element.set("value", value)
        else:
            element.text = value
    output = backup.with_suffix(".prepared.xml")
    output.write_bytes(ET.tostring(root, encoding="utf-8", xml_declaration=True))
elif mode == "restore":
    if not backup.exists():
        sys.exit(0)
    output = backup
else:
    raise ValueError("prepare ou restore")
adb(device, "push", str(output), "/data/local/tmp/mpmodplayer-settings.xml")
adb(device, "shell", "run-as", "com.bunnyloader", "cp", "/data/local/tmp/mpmodplayer-settings.xml", settings)
if mode == "restore":
    backup.unlink()
