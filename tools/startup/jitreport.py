import re, sys, collections

rows = []
for line in open(sys.argv[1], encoding="utf-8", errors="replace"):
    parts = line.rstrip("\n").split("\t")
    if len(parts) < 4:
        continue
    rows.append((float(parts[0]), float(parts[1]), parts[2], parts[3]))

total = sum(r[0] for r in rows)
print("methods", len(rows), "self ms", round(total))
tids = collections.Counter()
for r in rows:
    tids[r[2]] += r[0]
print("by thread", {k: round(v) for k, v in tids.items()})

kinds = collections.defaultdict(lambda: [0.0, 0])
classes = collections.defaultdict(lambda: [0.0, 0])
for self_ms, _, _, name in rows:
    wrapper = name.startswith("(wrapper")
    m = re.match(r"\(wrapper ([a-z\-]+)\)", name)
    kind = "wrapper " + m.group(1) if m else "normal"
    kinds[kind][0] += self_ms
    kinds[kind][1] += 1
    clean = re.sub(r"^\(wrapper [^)]*\) ", "", name)
    head = clean.split(":", 1)[0].strip()
    classes[head][0] += self_ms
    classes[head][1] += 1
print("\nby kind")
for k, v in sorted(kinds.items(), key=lambda p: -p[1][0]):
    print(f"{v[0]:9.0f} ms {v[1]:6d}  {k}")

groups = collections.defaultdict(lambda: [0.0, 0])
for k, v in classes.items():
    if k.startswith("System.") or k.startswith("Interop") or k.startswith("Internal.") or k.startswith("Mono."):
        g = "BCL"
    elif k.startswith("Microsoft.CodeAnalysis"):
        g = "Roslyn"
    elif k.startswith("ExampleMod"):
        g = "ExampleMod"
    elif k.startswith("BunnyLoader") or k.startswith("Terraria.ModLoader") or k.startswith("<"):
        g = "API"
    elif k.startswith("Terraria") or k.startswith("Microsoft.Xna") or k.startswith("On_") or k.startswith("ReLogic"):
        g = "proxies"
    else:
        g = "other"
    groups[g][0] += v[0]
    groups[g][1] += v[1]
print("\nby origin")
for k, v in sorted(groups.items(), key=lambda p: -p[1][0]):
    print(f"{v[0]:9.0f} ms {v[1]:6d}  {k}")

print("\ntop classes")
for k, v in sorted(classes.items(), key=lambda p: -p[1][0])[:45]:
    print(f"{v[0]:9.1f} ms {v[1]:5d}  {k[:110]}")
print("\ntop methods")
for r in sorted(rows, key=lambda r: -r[0])[:40]:
    print(f"{r[0]:9.1f} ms  {r[3][:130]}")
