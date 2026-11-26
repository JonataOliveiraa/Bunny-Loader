import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
DUMP = ROOT / "refs/dump.cs"
OUT = ROOT / "app/src/main/csharp/ProxyGen/generic-instances.txt"

NAMESPACE = re.compile(r"^// Namespace: ?(.*)$")
IMAGE = re.compile(r"^// Image \d+: (\S+) - (\d+)$")
TYPE = re.compile(r"^(?:[a-z]+ )*(?:class|struct|enum|interface) ([^\s:<]+)(?:<[^>]*>)?[^/]*// TypeDefIndex: (\d+)")
METHOD = re.compile(r"^\t(?:(?:public|private|protected|internal|static|virtual|override|abstract|sealed|extern|unsafe|new) )+.*?\b(\w+)<([\w\s,]*)>\((.*)\) \{ \}$")

KEYWORDS = {
    "object": "System.Object", "bool": "System.Boolean", "byte": "System.Byte", "sbyte": "System.SByte",
    "short": "System.Int16", "ushort": "System.UInt16", "int": "System.Int32", "uint": "System.UInt32",
    "long": "System.Int64", "ulong": "System.UInt64", "float": "System.Single", "double": "System.Double",
    "char": "System.Char", "string": "System.String",
}


def split_top(text):
    parts, depth, start = [], 0, 0
    for i, c in enumerate(text):
        if c in "<([":
            depth += 1
        elif c in ">)]":
            depth -= 1
        elif c == "," and depth == 0:
            parts.append(text[start:i].strip())
            start = i + 1
    tail = text[start:].strip()
    if tail:
        parts.append(tail)
    return parts


def param_names(params):
    names = []
    for p in split_top(params):
        p = p.split("=")[0].strip()
        names.append(p.split()[-1] if p else "")
    return names


def main():
    if not DUMP.exists():
        print(f"{DUMP} not found (run tools/dump.sh)", file=sys.stderr)
        return 1

    lines = DUMP.read_text(encoding="utf-8", errors="replace").splitlines()
    starts = sorted(int(m.group(2)) for m in map(IMAGE.match, lines[:64]) if m)
    game_end = next((s for s in starts if s > 0), 1 << 30)

    types = []
    ns = ""
    for line in lines:
        m = NAMESPACE.match(line)
        if m:
            ns = m.group(1).strip()
            continue
        m = TYPE.match(line)
        if m:
            types.append((ns, m.group(1), int(m.group(2))))

    top_ns = {}
    for ns, dotted, index in types:
        if index < game_end and ns and "." not in dotted:
            top_ns.setdefault(dotted, set()).add(ns)

    full_of = {}
    by_short = {}
    for ns, dotted, index in types:
        game = index < game_end
        if not ns and "." in dotted and game:
            found = top_ns.get(dotted.split(".")[0], set())
            ns = next(iter(found)) if len(found) == 1 else ""
        full = (ns + "." if ns else "") + dotted.replace(".", "+")
        full_of[index] = full
        by_short.setdefault(dotted, []).append((game, full))

    def resolve(name):
        if name in KEYWORDS:
            return KEYWORDS[name]
        found = by_short.get(name, [])
        game = [f for g, f in found if g]
        pick = game if game else [f for _, f in found]
        return pick[0] if len(set(pick)) == 1 else "?" + name

    rows, seen = [], set()
    owner, pending, in_block = None, None, False
    for line in lines:
        m = TYPE.match(line)
        if m:
            index = int(m.group(2))
            owner = full_of[index] if index < game_end else None
            pending = None
            continue
        s = line.strip()
        if s.startswith("/* GenericInstMethod"):
            in_block = True
            continue
        if in_block:
            if s.startswith("*/"):
                in_block = False
                pending = None
            elif s.startswith("|-") and not s.startswith("|-RVA") and pending:
                inst = s[2:]
                at = inst.rfind("." + pending[1] + "<")
                if at < 0 or not inst.endswith(">"):
                    continue
                args = ",".join(resolve(a) for a in split_top(inst[at + len(pending[1]) + 2:-1]))
                row = (pending[0], pending[1], pending[2], args)
                if row not in seen:
                    seen.add(row)
                    rows.append(row)
            continue
        m = METHOD.match(line)
        if m and owner:
            pending = (owner, m.group(1), ",".join(param_names(m.group(3))))
        elif s.startswith(("public", "private", "protected", "internal")):
            pending = None

    rows.sort()
    with OUT.open("w", encoding="utf-8", newline="\n") as out:
        for row in rows:
            out.write("\t".join(row) + "\n")
    print(f"{OUT.relative_to(ROOT)}: {len(rows)} instances")
    return 0


if __name__ == "__main__":
    sys.exit(main())
