import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
JS = ROOT / "app/src/main/cpp/script/js"
PROXYGEN = ROOT / "app/src/main/csharp/ProxyGen"
NAME = r"(?:Terraria|Microsoft|ReLogic)(?:\.[A-Za-z_]\w*)+"
PATTERNS = [
    re.compile(r"(" + NAME + r")\s*\[\s*['\"`]"),
    re.compile(r"(" + NAME + r")\.new\s*\("),
]
OUTSIDE = ("System.", "UnityEngine.")
CLASS_OF = re.compile(r"bl\.classOf\(\s*['\"]([^'\"]*)['\"]\s*,\s*['\"]([^'\"]+)['\"]")


def listed(path):
    names = set()
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.split("#")[0].strip()
        if line:
            names.add(line.replace("/", "."))
    return names


def found():
    names = set()
    for file in sorted(JS.rglob("*.js")):
        text = file.read_text(encoding="utf-8")
        for pattern in PATTERNS:
            for match in pattern.finditer(text):
                name = match.group(1)
                if name.split(".")[-1][:1].isupper():
                    names.add(name)
        for match in CLASS_OF.finditer(text):
            ns, cls = match.group(1), match.group(2)
            names.add(f"{ns}.{cls}" if ns else cls)
    return names


def main():
    out = PROXYGEN / "types-js.txt"
    known = listed(PROXYGEN / "types.txt")
    names = sorted(n for n in found() - known if not n.startswith(OUTSIDE))
    out.write_text("".join(n + "\n" for n in names), encoding="utf-8")
    print(f"{len(names)} types used by the JS code and not in types.txt -> {out.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
