"""Desassembla um metodo do libil2cpp.so pelo nome (Classe$$Metodo) com anotacoes.

uso: python dis.py "Terraria.Main$$StartRain" [max_instr]
"""
import json, re, subprocess, sys, bisect, pickle, os

REFS = r"C:\Scripts\Bunny Loader\refs"
OBJ = r"C:\Users\nadek\AppData\Local\Android\Sdk\ndk\30.0.16248370\toolchains\llvm\prebuilt\windows-x86_64\bin\llvm-objdump.exe"
CACHE = os.path.join(os.path.dirname(__file__), "script.pkl")

if os.path.exists(CACHE):
    methods, meta = pickle.load(open(CACHE, "rb"))
else:
    d = json.load(open(os.path.join(REFS, "script.json"), encoding="utf-8"))
    methods = sorted((m["Address"], m["Name"]) for m in d["ScriptMethod"])
    meta = {}
    for m in d["ScriptMetadata"]:
        meta[m["Address"]] = m["Name"]
    for m in d["ScriptMetadataMethod"]:
        meta[m["Address"]] = m["Name"]
    for m in d["ScriptString"]:
        meta[m["Address"]] = "str:" + repr(m["Value"])[:60]
    pickle.dump((methods, meta), open(CACHE, "wb"))

GOT = pickle.load(open(os.path.join(os.path.dirname(__file__), "got.pkl"), "rb"))
addrs = [a for a, _ in methods]
by_addr = {}
for a, n in methods:
    by_addr.setdefault(a, n)

def name_at(a):
    return by_addr.get(a)

def find(q):
    return [(a, n) for a, n in methods if n == q or n.endswith(q)]

q = sys.argv[1]
maxn = int(sys.argv[2]) if len(sys.argv) > 2 else 400
hits = find(q)
if not hits:
    hits = [(a, n) for a, n in methods if q in n]
if len(hits) != 1:
    for a, n in hits[:40]:
        print(hex(a), n)
    if len(hits) != 1 and not (len(hits) > 1 and sys.argv[-1] == "first"):
        sys.exit(0)
start, nm = hits[0]
i = bisect.bisect_right(addrs, start)
end = addrs[i] if i < len(addrs) else start + 0x400
end = min(end, start + maxn * 4)
print(f"== {nm} @ {start:#x}..{end:#x}")
out = subprocess.run([OBJ, "-d", "--no-show-raw-insn", f"--start-address={start:#x}",
                      f"--stop-address={end:#x}", os.path.join(REFS, "libil2cpp.so")],
                     capture_output=True, text=True).stdout
adrp = {}
for line in out.splitlines():
    m = re.match(r"\s*([0-9a-f]+):\s+(\S+)\s*(.*)", line)
    if not m:
        continue
    pc, op, args = int(m.group(1), 16), m.group(2), m.group(3)
    note = ""
    if op == "adrp":
        r, v = args.split(",")[0].strip(), re.search(r"0x([0-9a-f]+)", args)
        if v:
            adrp[r] = int(v.group(1), 16)
    elif op in ("ldr", "add") and adrp:
        mm = re.match(r"(\w+),\s*\[?(\w+)(?:,\s*#(0x[0-9a-f]+|\d+))?", args)
        if mm and mm.group(2) in adrp:
            off = int(mm.group(3), 0) if mm.group(3) else 0
            t = adrp[mm.group(2)] + off
            if t in GOT and GOT[t] in meta:
                note = "  ; " + meta[GOT[t]]
            elif t in GOT and GOT[t] in by_addr:
                note = "  ; &" + by_addr[GOT[t]]
            elif t in meta:
                note = "  ; " + meta[t]
            elif t in by_addr:
                note = "  ; &" + by_addr[t]
    if op != "adrp":
        dst = args.split(",")[0].strip()
        if dst in adrp and not (op in ("ldr", "add") and note == "" and False):
            if not (op.startswith("st") or op.startswith("cb") or op.startswith("tb") or op == "cmp"):
                del adrp[dst]
    if op in ("bl", "b"):
        v = re.search(r"0x([0-9a-f]+)", args)
        if v:
            t = int(v.group(1), 16)
            n = name_at(t)
            if n:
                note = "  ; " + n
    print(f"{pc:8x}: {op:8s} {args}{note}")
