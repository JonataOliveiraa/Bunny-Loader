"""Compara rodadas do tools/bench: media de cada grupo, lado a lado.

  python tools/bench/compare.py base=docs/dados/otimizacao/passo0-[cd].txt passo1=docs/dados/otimizacao/passo1-*.txt

Cada argumento e nome=glob. A primeira coluna e a referencia do "delta".
"""
import glob, re, sys

groups = []
for arg in sys.argv[1:]:
    name, pat = arg.split("=", 1)
    runs = []
    for f in sorted(glob.glob(pat)):
        vals = {}
        for line in open(f, encoding="utf-8", errors="replace"):
            m = re.match(r"bench (\w+) = (-?\d+) ns/op", line)
            if m:
                vals[m.group(1)] = float(m.group(2))
                continue
            m = re.match(r"bench (hook .*?) = \+(-?\d+) ns", line)
            if m:
                vals[m.group(1)] = float(m.group(2))
        if vals:
            runs.append(vals)
    if not runs:
        sys.exit(f"{name}: nenhuma rodada em {pat}")
    keys = runs[0].keys()
    groups.append((name, len(runs), {k: sum(r.get(k, 0) for r in runs) / len(runs) for k in keys}))

ref = groups[0][2]
header = f"{'item':44}" + "".join(f"{g[0] + ' (' + str(g[1]) + ')':>16}" for g in groups)
print(header)
for k in ref:
    row = f"{k:44}"
    for i, (_, _, avg) in enumerate(groups):
        v = avg.get(k)
        if v is None:
            row += f"{'-':>16}"
        elif i == 0:
            row += f"{v:>16.0f}"
        else:
            d = (v - ref[k]) / ref[k] * 100 if ref[k] else 0
            row += f"{v:>9.0f} {d:+5.0f}%"
    print(row)
